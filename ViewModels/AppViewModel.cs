using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using M_A_G_A.Audio;
using M_A_G_A.Helpers;
using M_A_G_A.Models;
using M_A_G_A.Network;

namespace M_A_G_A.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;
        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        { _execute = execute; _canExecute = canExecute; }
        public bool CanExecute(object p) => _canExecute == null || _canExecute(p);
        public void Execute(object p) => _execute(p);
        public event EventHandler CanExecuteChanged
        {
            add    => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    public class AppViewModel : INotifyPropertyChanged, IDisposable
    {
        // ─── Identity (stable across sessions via MAC+hostname) ──────
        private readonly string _myId;   // deterministic GUID from MAC+hostname
        private string _myName;
        private byte[] _myAvatar;

        // ─── State ──────────────────────────────────────────────────
        private bool   _isSetupDone;
        private User   _selectedContact;
        private string _messageInput;
        private string _searchQuery;
        private bool   _isRecording;
        private bool   _autoStart;
        private bool   _showSettings;

        // ─── Info exposed for the UI ─────────────────────────────────
        public string MyMacAddress  => NetworkHelper.GetMacAddress();
        public string MyHostname    => NetworkHelper.GetHostname();
        public string MyIPv4        => NetworkHelper.GetIPv4();
        public string MyIPv6        => NetworkHelper.GetIPv6();

        // ─── Network ────────────────────────────────────────────────
        private readonly NetworkDiscovery _discovery = new NetworkDiscovery();
        private readonly TcpChatServer    _server    = new TcpChatServer();
        private readonly AudioRecorder    _audio     = new AudioRecorder();

        // ─── Data ────────────────────────────────────────────────────
        public ObservableCollection<User>        Contacts        { get; } = new ObservableCollection<User>();
        public ObservableCollection<User>        FilteredContacts{ get; } = new ObservableCollection<User>();
        public ObservableCollection<ChatMessage> CurrentMessages { get; } = new ObservableCollection<ChatMessage>();

        private readonly Dictionary<string, ObservableCollection<ChatMessage>> _chatHistory
            = new Dictionary<string, ObservableCollection<ChatMessage>>();
        private readonly Dictionary<string, DateTime> _lastHeartbeat = new Dictionary<string, DateTime>();

        // ─── Properties ──────────────────────────────────────────────
        public string MyName
        {
            get => _myName;
            set { _myName = value; OnPropChanged(); }
        }
        public byte[] MyAvatar
        {
            get => _myAvatar;
            set { _myAvatar = value; OnPropChanged(); }
        }
        public bool IsSetupDone
        {
            get => _isSetupDone;
            set { _isSetupDone = value; OnPropChanged(); }
        }
        public string MessageInput
        {
            get => _messageInput;
            set { _messageInput = value; OnPropChanged(); }
        }
        public bool IsRecording
        {
            get => _isRecording;
            set { _isRecording = value; OnPropChanged(); }
        }
        public bool AutoStart
        {
            get => _autoStart;
            set
            {
                _autoStart = value;
                OnPropChanged();
                AutoStartHelper.SetEnabled(value);
            }
        }
        public bool ShowSettings
        {
            get => _showSettings;
            set { _showSettings = value; OnPropChanged(); }
        }

        public User SelectedContact
        {
            get => _selectedContact;
            set
            {
                if (_selectedContact != null)
                    SaveContactHistory(_selectedContact.Id);
                _selectedContact = value;
                OnPropChanged();
                OnPropChanged(nameof(HasSelectedContact));
                LoadMessages(value?.Id);
            }
        }
        public bool HasSelectedContact => _selectedContact != null;

        public string SearchQuery
        {
            get => _searchQuery;
            set { _searchQuery = value; OnPropChanged(); ApplySearch(); }
        }

        // ─── Commands ──────────────────────────────────────────────
        public ICommand StartAppCommand       { get; }
        public ICommand SendTextCommand       { get; }
        public ICommand StartVoiceCommand     { get; }
        public ICommand StopVoiceCommand      { get; }
        public ICommand PlayVoiceCommand      { get; }
        public ICommand SelectContactCommand  { get; }
        public ICommand PickAvatarCommand     { get; }
        public ICommand SendVoiceFileCommand  { get; }
        public ICommand SendImageCommand      { get; }  // NEW
        public ICommand SendFileCommand       { get; }  // NEW
        public ICommand SaveFileCommand       { get; }  // NEW – save received file
        public ICommand ToggleSettingsCommand { get; }  // NEW
        public ICommand ExportHistoryCommand  { get; }  // NEW
        public ICommand ImportHistoryCommand  { get; }  // NEW

        public AppViewModel()
        {
            _myId = NetworkHelper.GetStableId();

            StartAppCommand       = new RelayCommand(_ => StartApp(),              _ => !string.IsNullOrWhiteSpace(MyName));
            SendTextCommand       = new RelayCommand(_ => SendText(),              _ => !string.IsNullOrWhiteSpace(MessageInput) && SelectedContact != null);
            StartVoiceCommand     = new RelayCommand(_ => StartVoiceRecording(),   _ => SelectedContact != null && !IsRecording);
            StopVoiceCommand      = new RelayCommand(_ => StopAndSendVoice(),      _ => IsRecording);
            PlayVoiceCommand      = new RelayCommand(msg => PlayVoiceMessage(msg as ChatMessage));
            SelectContactCommand  = new RelayCommand(u => SelectedContact = u as User);
            PickAvatarCommand     = new RelayCommand(_ => PickAvatar());
            SendVoiceFileCommand  = new RelayCommand(_ => SendVoiceFile(),         _ => SelectedContact != null);
            SendImageCommand      = new RelayCommand(_ => SendImage(),             _ => SelectedContact != null);
            SendFileCommand       = new RelayCommand(_ => SendFile(),              _ => SelectedContact != null);
            SaveFileCommand       = new RelayCommand(msg => SaveReceivedFile(msg as ChatMessage));
            ToggleSettingsCommand = new RelayCommand(_ => ShowSettings = !ShowSettings);
            ExportHistoryCommand  = new RelayCommand(_ => ExportHistory());
            ImportHistoryCommand  = new RelayCommand(_ => ImportHistory(),         _ => SelectedContact != null);

            _autoStart = AutoStartHelper.IsEnabled();

            // Heartbeat checker
            new Timer(_ => CheckHeartbeats(), null, 5000, 5000);
        }

        // ─── Setup ─────────────────────────────────────────────────
        private void StartApp()
        {
            if (string.IsNullOrWhiteSpace(MyName)) return;
            LoadSavedProfile();
            _server.MessageReceived += OnMessageReceived;
            _server.Start();
            _discovery.PeerDiscovered    += OnPeerDiscovered;
            _discovery.PeerDisconnected  += OnPeerDisconnected;
            _discovery.Start(_myId, _myName, _myAvatar != null ? Convert.ToBase64String(_myAvatar) : "", _server.Port);
            IsSetupDone = true;
        }

        private void LoadSavedProfile()
        {
            var cfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MAGA");
            Directory.CreateDirectory(cfg);
            var avatarPath = Path.Combine(cfg, "avatar.png");
            if (MyAvatar == null && File.Exists(avatarPath))
                MyAvatar = File.ReadAllBytes(avatarPath);
            var namePath = Path.Combine(cfg, "name.txt");
            if (string.IsNullOrWhiteSpace(MyName) && File.Exists(namePath))
                MyName = File.ReadAllText(namePath).Trim();
        }

        public void SaveProfile()
        {
            var cfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MAGA");
            Directory.CreateDirectory(cfg);
            if (!string.IsNullOrWhiteSpace(MyName))
                File.WriteAllText(Path.Combine(cfg, "name.txt"), MyName);
            if (MyAvatar != null)
                File.WriteAllBytes(Path.Combine(cfg, "avatar.png"), MyAvatar);
        }

        private void PickAvatar()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                Title  = "Выберите аватарку"
            };
            if (dlg.ShowDialog() != true) return;
            MyAvatar = ResizeImage(File.ReadAllBytes(dlg.FileName));
            SaveProfile();
            _discovery.UpdateAvatar(MyAvatar != null ? Convert.ToBase64String(MyAvatar) : "");
        }

        private byte[] ResizeImage(byte[] src)
        {
            try
            {
                using (var ms = new MemoryStream(src))
                {
                    var bmp     = new System.Drawing.Bitmap(ms);
                    var resized = new System.Drawing.Bitmap(bmp, new System.Drawing.Size(128, 128));
                    using (var ms2 = new MemoryStream())
                    {
                        resized.Save(ms2, System.Drawing.Imaging.ImageFormat.Png);
                        return ms2.ToArray();
                    }
                }
            }
            catch { return src; }
        }

        // ─── Network events ────────────────────────────────────────
        private void OnPeerDiscovered(NetworkPacket packet, string ip)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _lastHeartbeat[packet.SenderId] = DateTime.Now;
                var existing = Contacts.FirstOrDefault(c => c.Id == packet.SenderId);

                byte[] avatar = null;
                if (!string.IsNullOrEmpty(packet.SenderAvatar))
                {
                    try { avatar = Convert.FromBase64String(packet.SenderAvatar); } catch { }
                }

                if (existing == null)
                {
                    var user = new User
                    {
                        Id          = packet.SenderId,
                        Username    = packet.SenderName,
                        MacAddress  = packet.MacAddress ?? "",
                        Hostname    = packet.Hostname   ?? "",
                        IpAddress   = !string.IsNullOrEmpty(packet.IPv4) ? packet.IPv4 : ip,
                        IPv6        = packet.IPv6 ?? "",
                        TcpPort     = packet.TcpPort,
                        IsOnline    = true,
                        LastSeen    = DateTime.Now,
                        AvatarBytes = avatar
                    };
                    Contacts.Add(user);
                    ApplySearch();
                }
                else
                {
                    existing.IsOnline   = true;
                    existing.LastSeen   = DateTime.Now;
                    existing.IpAddress  = !string.IsNullOrEmpty(packet.IPv4) ? packet.IPv4 : ip;
                    existing.IPv6       = packet.IPv6 ?? existing.IPv6;
                    existing.MacAddress = packet.MacAddress ?? existing.MacAddress;
                    existing.Hostname   = packet.Hostname   ?? existing.Hostname;
                    existing.TcpPort    = packet.TcpPort;
                    // ← ALWAYS update avatar (fix for stale avatars)
                    if (avatar != null)
                        existing.AvatarBytes = avatar;
                    ApplySearch();
                }
            });
        }

        private void OnPeerDisconnected(string peerId)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var user = Contacts.FirstOrDefault(c => c.Id == peerId);
                if (user != null) { user.IsOnline = false; user.LastSeen = DateTime.Now; }
            });
        }

        private void CheckHeartbeats()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var now = DateTime.Now;
                foreach (var user in Contacts)
                {
                    if (_lastHeartbeat.TryGetValue(user.Id, out var last))
                        user.IsOnline = (now - last).TotalSeconds < 10;
                }
            });
        }

        private void OnMessageReceived(NetworkPacket packet, string senderIp)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // ── Ensure sender exists in contacts ──────────────────
                var sender = Contacts.FirstOrDefault(c => c.Id == packet.SenderId);
                if (sender == null)
                {
                    sender = new User
                    {
                        Id         = packet.SenderId,
                        Username   = packet.SenderName,
                        MacAddress = packet.MacAddress ?? "",
                        Hostname   = packet.Hostname   ?? "",
                        IpAddress  = !string.IsNullOrEmpty(packet.IPv4) ? packet.IPv4 : senderIp,
                        IPv6       = packet.IPv6 ?? "",
                        TcpPort    = packet.TcpPort,
                        IsOnline   = true,
                        LastSeen   = DateTime.Now
                    };
                    if (!string.IsNullOrEmpty(packet.SenderAvatar))
                    {
                        try { sender.AvatarBytes = Convert.FromBase64String(packet.SenderAvatar); } catch { }
                    }
                    Contacts.Add(sender);
                    ApplySearch();
                }
                else
                {
                    sender.IsOnline  = true;
                    sender.LastSeen  = DateTime.Now;
                    sender.IpAddress = !string.IsNullOrEmpty(packet.IPv4) ? packet.IPv4 : senderIp;
                    if (!string.IsNullOrEmpty(packet.SenderAvatar))
                    {
                        try { sender.AvatarBytes = Convert.FromBase64String(packet.SenderAvatar); } catch { }
                    }
                }
                _lastHeartbeat[packet.SenderId] = DateTime.Now;

                // ── Build chat message ────────────────────────────────
                ChatMessage msg;
                switch (packet.PacketType)
                {
                    case "IMAGE":
                        byte[] imgBytes = null;
                        try { imgBytes = Convert.FromBase64String(packet.Content ?? ""); } catch { }
                        msg = new ChatMessage
                        {
                            SenderId   = packet.SenderId,
                            SenderName = packet.SenderName,
                            Type       = MessageType.Image,
                            ImageBytes = imgBytes,
                            FileName   = packet.FileName,
                            Timestamp  = DateTime.Now,
                            IsSentByMe = false
                        };
                        break;

                    case "FILE":
                        byte[] fileBytes = null;
                        try { fileBytes = Convert.FromBase64String(packet.Content ?? ""); } catch { }
                        msg = new ChatMessage
                        {
                            SenderId   = packet.SenderId,
                            SenderName = packet.SenderName,
                            Type       = MessageType.File,
                            FileBytes  = fileBytes,
                            FileName   = packet.FileName ?? "file",
                            Timestamp  = DateTime.Now,
                            IsSentByMe = false
                        };
                        break;

                    case "VOICE":
                        msg = new ChatMessage
                        {
                            SenderId   = packet.SenderId,
                            SenderName = packet.SenderName,
                            Type       = MessageType.Voice,
                            Content    = packet.Content,
                            Timestamp  = DateTime.Now,
                            IsSentByMe = false
                        };
                        break;

                    default: // TEXT (may contain markdown)
                        msg = new ChatMessage
                        {
                            SenderId   = packet.SenderId,
                            SenderName = packet.SenderName,
                            Type       = MessageType.Text,
                            Content    = packet.Content,
                            Timestamp  = DateTime.Now,
                            IsSentByMe = false
                        };
                        break;
                }

                var history = GetHistory(packet.SenderId);
                history.Add(msg);
                SaveContactHistory(packet.SenderId);

                if (_selectedContact?.Id == packet.SenderId)
                    CurrentMessages.Add(msg);
            });
        }

        // ─── Messaging ─────────────────────────────────────────────
        private void SendText()
        {
            if (string.IsNullOrWhiteSpace(MessageInput) || _selectedContact == null) return;
            var text = MessageInput;
            MessageInput = "";
            var packet = BuildPacket("TEXT");
            packet.Content = text;
            SendToContact(packet);
            AddMyMessage(new ChatMessage { Type = MessageType.Text, Content = text });
        }

        private void StartVoiceRecording()
        {
            _audio.StartRecording();
            IsRecording = true;
        }

        private void StopAndSendVoice()
        {
            IsRecording = false;
            var bytes = _audio.StopRecording();
            if (bytes == null || bytes.Length == 0) return;
            var b64 = Convert.ToBase64String(bytes);
            var packet = BuildPacket("VOICE");
            packet.Content = b64;
            SendToContact(packet);
            AddMyMessage(new ChatMessage { Type = MessageType.Voice, Content = b64 });
        }

        private void SendVoiceFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Audio|*.wav;*.mp3", Title = "Отправить аудиофайл" };
            if (dlg.ShowDialog() != true) return;
            var bytes = File.ReadAllBytes(dlg.FileName);
            var b64   = Convert.ToBase64String(bytes);
            var packet = BuildPacket("VOICE");
            packet.Content  = b64;
            packet.FileName = Path.GetFileName(dlg.FileName);
            SendToContact(packet);
            AddMyMessage(new ChatMessage { Type = MessageType.Voice, Content = b64, FileName = packet.FileName });
        }

        private void SendImage()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp",
                Title  = "Отправить изображение"
            };
            if (dlg.ShowDialog() != true) return;
            var bytes    = File.ReadAllBytes(dlg.FileName);
            var b64      = Convert.ToBase64String(bytes);
            var packet   = BuildPacket("IMAGE");
            packet.Content  = b64;
            packet.FileName = Path.GetFileName(dlg.FileName);
            SendToContact(packet);
            AddMyMessage(new ChatMessage
            {
                Type       = MessageType.Image,
                ImageBytes = bytes,
                FileName   = packet.FileName
            });
        }

        private void SendFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Отправить файл" };
            if (dlg.ShowDialog() != true) return;
            var bytes    = File.ReadAllBytes(dlg.FileName);
            var b64      = Convert.ToBase64String(bytes);
            var packet   = BuildPacket("FILE");
            packet.Content  = b64;
            packet.FileName = Path.GetFileName(dlg.FileName);
            SendToContact(packet);
            AddMyMessage(new ChatMessage
            {
                Type      = MessageType.File,
                FileBytes = bytes,
                FileName  = packet.FileName
            });
        }

        private void SaveReceivedFile(ChatMessage msg)
        {
            if (msg == null || msg.FileBytes == null) return;
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = msg.FileName ?? "file",
                Title    = "Сохранить файл"
            };
            if (dlg.ShowDialog() != true) return;
            try { File.WriteAllBytes(dlg.FileName, msg.FileBytes); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void PlayVoiceMessage(ChatMessage msg)
        {
            if (msg == null) return;
            if (msg.IsPlaying) { _audio.StopPlayback(); msg.IsPlaying = false; return; }
            try
            {
                var bytes = Convert.FromBase64String(msg.Content);
                msg.IsPlaying = true;
                _audio.PlayAudio(bytes, () => Application.Current.Dispatcher.Invoke(() => msg.IsPlaying = false));
            }
            catch { }
        }

        // ─── History export/import ─────────────────────────────────
        private void ExportHistory()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter   = "ZIP Archive|*.zip",
                FileName = "maga_history.zip",
                Title    = "Экспорт истории"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var all = HistoryHelper.ExportAll();
                using (var zip = System.IO.Compression.ZipFile.Open(dlg.FileName,
                    System.IO.Compression.ZipArchiveMode.Create))
                {
                    foreach (var kv in all)
                    {
                        var entry = zip.CreateEntry(kv.Key + ".json");
                        using (var sw = new StreamWriter(entry.Open()))
                            sw.Write(kv.Value);
                    }
                }
                MessageBox.Show("История успешно экспортирована.", "Готово");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportHistory()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "ZIP Archive|*.zip",
                Title  = "Импорт истории"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                using (var zip = System.IO.Compression.ZipFile.OpenRead(dlg.FileName))
                {
                    foreach (var entry in zip.Entries)
                    {
                        var contactId = Path.GetFileNameWithoutExtension(entry.Name);
                        using (var sr = new StreamReader(entry.Open()))
                            HistoryHelper.ImportHistory(contactId, sr.ReadToEnd());
                    }
                }
                // Reload visible messages
                if (_selectedContact != null)
                {
                    _chatHistory.Remove(_selectedContact.Id);
                    LoadMessages(_selectedContact.Id);
                }
                MessageBox.Show("История успешно импортирована.", "Готово");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Transport ─────────────────────────────────────────────
        private void SendToContact(NetworkPacket packet)
        {
            if (_selectedContact == null) return;
            var contact = _selectedContact;
            System.Threading.Tasks.Task.Run(() =>
                TcpChatClient.Send(contact.IpAddress, contact.TcpPort, packet));
        }

        private void AddMyMessage(ChatMessage msg)
        {
            msg.Id         = Guid.NewGuid().ToString();
            msg.SenderId   = _myId;
            msg.SenderName = _myName;
            msg.Timestamp  = DateTime.Now;
            msg.IsSentByMe = true;
            var history = GetHistory(_selectedContact.Id);
            history.Add(msg);
            CurrentMessages.Add(msg);
            SaveContactHistory(_selectedContact.Id);
        }

        private NetworkPacket BuildPacket(string type) => new NetworkPacket
        {
            PacketType   = type,
            MessageId    = Guid.NewGuid().ToString(),
            SenderId     = _myId,
            SenderName   = _myName,
            SenderAvatar = _myAvatar != null ? Convert.ToBase64String(_myAvatar) : "",
            MacAddress   = NetworkHelper.GetMacAddress(),
            Hostname     = NetworkHelper.GetHostname(),
            IPv4         = NetworkHelper.GetIPv4(),
            IPv6         = NetworkHelper.GetIPv6(),
            Timestamp    = DateTime.Now.ToString("o"),
            TcpPort      = _server.Port
        };

        // ─── History helpers ───────────────────────────────────────
        private void LoadMessages(string contactId)
        {
            CurrentMessages.Clear();
            if (contactId == null) return;
            var history = GetHistory(contactId);
            foreach (var m in history)
                CurrentMessages.Add(m);
        }

        private ObservableCollection<ChatMessage> GetHistory(string id)
        {
            if (!_chatHistory.ContainsKey(id))
            {
                var list = HistoryHelper.LoadHistory(id);
                var oc   = new ObservableCollection<ChatMessage>(list);
                _chatHistory[id] = oc;
            }
            return _chatHistory[id];
        }

        private void SaveContactHistory(string id)
        {
            if (!_chatHistory.ContainsKey(id)) return;
            HistoryHelper.SaveHistory(id, _chatHistory[id]);
        }

        // ─── Search ────────────────────────────────────────────────
        private void ApplySearch()
        {
            FilteredContacts.Clear();
            var q = _searchQuery?.Trim().ToLower() ?? "";
            foreach (var c in Contacts.Where(c =>
                string.IsNullOrEmpty(q)
                || c.Username.ToLower().Contains(q)
                || (c.Hostname ?? "").ToLower().Contains(q)
                || (c.IpAddress ?? "").Contains(q)))
                FilteredContacts.Add(c);
        }

        // ─── Cleanup ────────────────────────────────────────────────
        public void Dispose()
        {
            _discovery.SendBye();
            _discovery.Dispose();
            _server.Dispose();
            _audio.Dispose();

            // Persist all open histories
            foreach (var kv in _chatHistory)
                HistoryHelper.SaveHistory(kv.Key, kv.Value);
            SaveProfile();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
