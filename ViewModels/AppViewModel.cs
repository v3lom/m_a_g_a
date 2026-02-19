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
using M_A_G_A.Models;
using M_A_G_A.Network;

namespace M_A_G_A.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;
        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null) { _execute = execute; _canExecute = canExecute; }
        public bool CanExecute(object p) => _canExecute == null || _canExecute(p);
        public void Execute(object p) => _execute(p);
        public event EventHandler CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    }

    public class AppViewModel : INotifyPropertyChanged, IDisposable
    {
        // ─── Current user ───────────────────────────────────────────
        private string _myId = Guid.NewGuid().ToString();
        private string _myName;
        private byte[] _myAvatar;

        // ─── State ──────────────────────────────────────────────────
        private bool _isSetupDone;
        private User _selectedContact;
        private string _messageInput;
        private string _searchQuery;
        private bool _isRecording;
        private string _currentView = "Setup"; // Setup | Main

        // ─── Network ────────────────────────────────────────────────
        private readonly NetworkDiscovery _discovery = new NetworkDiscovery();
        private readonly TcpChatServer _server = new TcpChatServer();
        private readonly AudioRecorder _audio = new AudioRecorder();

        // ─── Data ────────────────────────────────────────────────────
        public ObservableCollection<User> Contacts { get; } = new ObservableCollection<User>();
        public ObservableCollection<User> FilteredContacts { get; } = new ObservableCollection<User>();
        private readonly Dictionary<string, ObservableCollection<ChatMessage>> _chatHistory = new Dictionary<string, ObservableCollection<ChatMessage>>();
        private readonly Dictionary<string, DateTime> _lastHeartbeat = new Dictionary<string, DateTime>();

        public ObservableCollection<ChatMessage> CurrentMessages { get; } = new ObservableCollection<ChatMessage>();

        // ─── Properties ──────────────────────────────────────────────
        public string MyName { get => _myName; set { _myName = value; OnPropChanged(); } }
        public byte[] MyAvatar { get => _myAvatar; set { _myAvatar = value; OnPropChanged(); } }
        public bool IsSetupDone { get => _isSetupDone; set { _isSetupDone = value; OnPropChanged(); } }
        public string CurrentView { get => _currentView; set { _currentView = value; OnPropChanged(); } }
        public string MessageInput { get => _messageInput; set { _messageInput = value; OnPropChanged(); } }
        public bool IsRecording { get => _isRecording; set { _isRecording = value; OnPropChanged(); } }

        public User SelectedContact
        {
            get => _selectedContact;
            set
            {
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
        public ICommand StartAppCommand { get; }
        public ICommand SendTextCommand { get; }
        public ICommand StartVoiceCommand { get; }
        public ICommand StopVoiceCommand { get; }
        public ICommand PlayVoiceCommand { get; }
        public ICommand SelectContactCommand { get; }
        public ICommand PickAvatarCommand { get; }
        public ICommand SendVoiceFileCommand { get; }

        public AppViewModel()
        {
            StartAppCommand = new RelayCommand(_ => StartApp(), _ => !string.IsNullOrWhiteSpace(MyName));
            SendTextCommand = new RelayCommand(_ => SendText(), _ => !string.IsNullOrWhiteSpace(MessageInput) && SelectedContact != null);
            StartVoiceCommand = new RelayCommand(_ => StartVoiceRecording(), _ => SelectedContact != null && !IsRecording);
            StopVoiceCommand = new RelayCommand(_ => StopAndSendVoice(), _ => IsRecording);
            PlayVoiceCommand = new RelayCommand(msg => PlayVoiceMessage(msg as ChatMessage));
            SelectContactCommand = new RelayCommand(u => SelectedContact = u as User);
            PickAvatarCommand = new RelayCommand(_ => PickAvatar());
            SendVoiceFileCommand = new RelayCommand(_ => SendVoiceFile(), _ => SelectedContact != null);

            // Periodically check for stale peers
            var timer = new Timer(_ => CheckHeartbeats(), null, 5000, 5000);
        }

        // ─── Setup ─────────────────────────────────────────────────
        private void StartApp()
        {
            if (string.IsNullOrWhiteSpace(MyName)) return;
            LoadSavedProfile();
            _server.MessageReceived += OnMessageReceived;
            _server.Start();
            _discovery.PeerDiscovered += OnPeerDiscovered;
            _discovery.PeerDisconnected += OnPeerDisconnected;
            _discovery.Start(_myId, _myName, MyAvatar != null ? Convert.ToBase64String(MyAvatar) : "", _server.Port);
            IsSetupDone = true;
            CurrentView = "Main";
        }

        private void LoadSavedProfile()
        {
            var cfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MAGA");
            Directory.CreateDirectory(cfg);
            var avatarPath = Path.Combine(cfg, "avatar.png");
            if (MyAvatar == null && File.Exists(avatarPath))
                MyAvatar = File.ReadAllBytes(avatarPath);
        }

        private void PickAvatar()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                Title = "Выберите аватарку"
            };
            if (dlg.ShowDialog() == true)
            {
                MyAvatar = ResizeImage(File.ReadAllBytes(dlg.FileName));
                var cfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MAGA");
                Directory.CreateDirectory(cfg);
                File.WriteAllBytes(Path.Combine(cfg, "avatar.png"), MyAvatar);
            }
        }

        private byte[] ResizeImage(byte[] src)
        {
            try
            {
                using (var ms = new MemoryStream(src))
                {
                    var bmp = new System.Drawing.Bitmap(ms);
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
                if (existing == null)
                {
                    var user = new User
                    {
                        Id = packet.SenderId,
                        Username = packet.SenderName,
                        IpAddress = ip,
                        TcpPort = packet.TcpPort,
                        IsOnline = true,
                        LastSeen = DateTime.Now,
                        AvatarBytes = !string.IsNullOrEmpty(packet.SenderAvatar)
                            ? Convert.FromBase64String(packet.SenderAvatar) : null
                    };
                    Contacts.Add(user);
                    ApplySearch();
                }
                else
                {
                    existing.IsOnline = true;
                    existing.LastSeen = DateTime.Now;
                    existing.IpAddress = ip;
                    existing.TcpPort = packet.TcpPort;
                    if (!string.IsNullOrEmpty(packet.SenderAvatar) && existing.AvatarBytes == null)
                        existing.AvatarBytes = Convert.FromBase64String(packet.SenderAvatar);
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
                var sender = Contacts.FirstOrDefault(c => c.Id == packet.SenderId);
                if (sender == null)
                {
                    sender = new User
                    {
                        Id = packet.SenderId,
                        Username = packet.SenderName,
                        IpAddress = senderIp,
                        IsOnline = true,
                        LastSeen = DateTime.Now
                    };
                    Contacts.Add(sender);
                    ApplySearch();
                }

                sender.IsOnline = true;
                sender.LastSeen = DateTime.Now;

                var history = GetHistory(packet.SenderId);
                var msg = new ChatMessage
                {
                    SenderId = packet.SenderId,
                    SenderName = packet.SenderName,
                    Type = packet.PacketType == "VOICE" ? MessageType.Voice : MessageType.Text,
                    Content = packet.Content,
                    Timestamp = DateTime.Now,
                    IsSentByMe = false
                };
                history.Add(msg);

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
            AddMyMessage(MessageType.Text, text);
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
            AddMyMessage(MessageType.Voice, b64);
        }

        private void SendVoiceFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Audio|*.wav;*.mp3", Title = "Выберите аудиофайл" };
            if (dlg.ShowDialog() != true) return;
            var bytes = File.ReadAllBytes(dlg.FileName);
            var b64 = Convert.ToBase64String(bytes);
            var packet = BuildPacket("VOICE");
            packet.Content = b64;
            SendToContact(packet);
            AddMyMessage(MessageType.Voice, b64);
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

        private void SendToContact(NetworkPacket packet)
        {
            if (_selectedContact == null) return;
            var contact = _selectedContact;
            System.Threading.Tasks.Task.Run(() => TcpChatClient.Send(contact.IpAddress, contact.TcpPort, packet));
        }

        private void AddMyMessage(MessageType type, string content)
        {
            var msg = new ChatMessage
            {
                SenderId = _myId,
                SenderName = _myName,
                Type = type,
                Content = content,
                Timestamp = DateTime.Now,
                IsSentByMe = true
            };
            GetHistory(_selectedContact.Id).Add(msg);
            CurrentMessages.Add(msg);
        }

        private NetworkPacket BuildPacket(string type) => new NetworkPacket
        {
            PacketType = type,
            SenderId = _myId,
            SenderName = _myName,
            SenderAvatar = _myAvatar != null ? Convert.ToBase64String(_myAvatar) : "",
            Timestamp = DateTime.Now.ToString("o"),
            TcpPort = _server.Port
        };

        // ─── Helpers ───────────────────────────────────────────────
        private void LoadMessages(string contactId)
        {
            CurrentMessages.Clear();
            if (contactId == null) return;
            foreach (var m in GetHistory(contactId))
                CurrentMessages.Add(m);
        }

        private ObservableCollection<ChatMessage> GetHistory(string id)
        {
            if (!_chatHistory.ContainsKey(id))
                _chatHistory[id] = new ObservableCollection<ChatMessage>();
            return _chatHistory[id];
        }

        private void ApplySearch()
        {
            FilteredContacts.Clear();
            var q = _searchQuery?.Trim().ToLower() ?? "";
            foreach (var c in Contacts.Where(c => string.IsNullOrEmpty(q) || c.Username.ToLower().Contains(q)))
                FilteredContacts.Add(c);
        }

        public void Dispose()
        {
            _discovery.SendBye();
            _discovery.Dispose();
            _server.Dispose();
            _audio.Dispose();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
