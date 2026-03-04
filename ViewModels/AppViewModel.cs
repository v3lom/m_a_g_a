using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private bool   _notificationsEnabled = true;
        private bool   _stealthMode;
        private string _manualIpInput;
        private bool   _isEmojiPickerOpen;
        private bool   _isLightTheme;
        private byte[] _globalChatBackground;
        private string _selectedFolderFilter;  // null = show all
        private string _myBio;
        private bool   _isSelectionMode;
        private bool   _showAttachMenu;
        private ChatMessage _editingMessage;

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
        public ObservableCollection<ChatFolder>  Folders         { get; } = new ObservableCollection<ChatFolder>();
        public ObservableCollection<string>      FolderFilters   { get; } = new ObservableCollection<string>();

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
            set { _messageInput = value; OnPropChanged(); OnPropChanged(nameof(MessageLengthInfo)); }
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
        public bool NotificationsEnabled
        {
            get => _notificationsEnabled;
            set { _notificationsEnabled = value; OnPropChanged(); SaveSettings(); }
        }
        public bool StealthMode
        {
            get => _stealthMode;
            set
            {
                _stealthMode = value;
                OnPropChanged();
                _discovery.SetStealth(value);
                SaveSettings();
            }
        }
        public string ManualIpInput
        {
            get => _manualIpInput;
            set { _manualIpInput = value; OnPropChanged(); }
        }
        public string MessageLengthInfo => $"{_messageInput?.Length ?? 0}/16000";

        public bool IsEmojiPickerOpen
        {
            get => _isEmojiPickerOpen;
            set { _isEmojiPickerOpen = value; OnPropChanged(); }
        }
        public bool IsLightTheme
        {
            get => _isLightTheme;
            set { _isLightTheme = value; OnPropChanged(); SaveSettings(); }
        }
        public byte[] GlobalChatBackground
        {
            get => _globalChatBackground;
            set { _globalChatBackground = value; OnPropChanged(); OnPropChanged(nameof(ActiveChatBackground)); SaveSettings(); }
        }
        /// <summary>Per-chat background if set, otherwise global background.</summary>
        public byte[] ActiveChatBackground
            => (_selectedContact?.ChatBackground?.Length > 0)
               ? _selectedContact.ChatBackground
               : _globalChatBackground;

        public string SelectedFolderFilter
        {
            get => _selectedFolderFilter;
            set { _selectedFolderFilter = value; OnPropChanged(); ApplySearch(); }
        }
        public bool HasPassword => !string.IsNullOrEmpty(_settings?.PasswordHash);

        public string MyBio
        {
            get => _myBio;
            set
            {
                var trimmed = value?.Length > User.MaxBioLength ? value.Substring(0, User.MaxBioLength) : value;
                _myBio = trimmed;
                OnPropChanged();
            }
        }

        public bool IsSelectionMode
        {
            get => _isSelectionMode;
            set { _isSelectionMode = value; OnPropChanged(); if (!value) ClearSelection(); }
        }

        public bool ShowAttachMenu
        {
            get => _showAttachMenu;
            set { _showAttachMenu = value; OnPropChanged(); }
        }

        public ChatMessage EditingMessage
        {
            get => _editingMessage;
            set { _editingMessage = value; OnPropChanged(); OnPropChanged(nameof(IsEditing)); }
        }

        public bool IsEditing => _editingMessage != null;

        /// <summary>Total unread messages across all contacts.</summary>
        public int TotalUnread => Contacts.Sum(c => c.UnreadCount);

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
                OnPropChanged(nameof(ActiveChatBackground));
                if (value != null) value.UnreadCount = 0;
                IsSelectionMode  = false;
                EditingMessage   = null;
                ShowAttachMenu   = false;
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
        public ICommand CloseContactCommand   { get; }  // ESC to close chat
        public ICommand ConnectByIpCommand    { get; }  // manual IP connect
        // ─── New commands ────────────────────────────────────────────
        public ICommand ToggleEmojiPickerCommand        { get; }
        public ICommand InsertEmojiCommand              { get; }
        public ICommand PickGlobalBackgroundCommand     { get; }
        public ICommand ClearGlobalBackgroundCommand    { get; }
        public ICommand PickContactBackgroundCommand    { get; }
        public ICommand ClearContactBackgroundCommand   { get; }
        public ICommand CreateFolderCommand             { get; }
        public ICommand DeleteFolderCommand             { get; }
        public ICommand SetContactFolderCommand         { get; }
        public ICommand SelectFolderFilterCommand       { get; }
        public ICommand SetPasswordCommand              { get; }
        public ICommand ClearPasswordCommand            { get; }
        public ICommand ToggleThemeCommand              { get; }
        // ─── Message management commands ─────────────────────────────
        public ICommand EditMessageCommand              { get; }
        public ICommand ConfirmEditCommand              { get; }
        public ICommand CancelEditCommand               { get; }
        public ICommand DeleteMessageCommand            { get; }
        public ICommand AddReactionCommand              { get; }
        public ICommand ToggleSelectModeCommand         { get; }
        public ICommand ToggleMessageSelectCommand      { get; }
        public ICommand DeleteSelectedCommand           { get; }
        // ─── Attachment menu ─────────────────────────────────────────
        public ICommand ToggleAttachMenuCommand         { get; }
        public ICommand SendVideoCommand                { get; }
        // ─── Profile broadcast ───────────────────────────────────────
        public ICommand BroadcastProfileCommand         { get; }

        // ─── Events ────────────────────────────────────────────────
        /// <summary>Raised when an incoming message deserves a desktop notification.</summary>
        public event Action<string, string> NotificationRequired; // (title, body)

        // ─── Cached settings reference ───────────────────────────────
        private AppSettings _settings;

        public AppViewModel()
        {
            _myId = NetworkHelper.GetStableId();

            // Pre-load saved profile so the setup screen shows the existing name
            PreloadProfile();

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
            CloseContactCommand   = new RelayCommand(_ => SelectedContact = null,  _ => SelectedContact != null);
            ConnectByIpCommand    = new RelayCommand(_ => ConnectByIp(),           _ => !string.IsNullOrWhiteSpace(ManualIpInput));

            ToggleEmojiPickerCommand     = new RelayCommand(_ => IsEmojiPickerOpen = !IsEmojiPickerOpen);
            InsertEmojiCommand           = new RelayCommand(e => InsertEmoji(e as string));
            PickGlobalBackgroundCommand  = new RelayCommand(_ => PickGlobalBackground());
            ClearGlobalBackgroundCommand = new RelayCommand(_ => GlobalChatBackground = null, _ => _globalChatBackground != null);
            PickContactBackgroundCommand = new RelayCommand(_ => PickContactBackground(), _ => SelectedContact != null);
            ClearContactBackgroundCommand= new RelayCommand(_ => ClearContactBackground(), _ => SelectedContact?.HasChatBackground == true);
            CreateFolderCommand          = new RelayCommand(_ => CreateFolder());
            DeleteFolderCommand          = new RelayCommand(f => DeleteFolder(f as ChatFolder), f => f is ChatFolder);
            SetContactFolderCommand      = new RelayCommand(f => SetContactFolder(f as string), _ => SelectedContact != null);
            SelectFolderFilterCommand    = new RelayCommand(f => SelectedFolderFilter = f as string);
            SetPasswordCommand           = new RelayCommand(_ => SetPassword());
            ClearPasswordCommand         = new RelayCommand(_ => ClearPassword(), _ => HasPassword);
            ToggleThemeCommand           = new RelayCommand(_ => IsLightTheme = !IsLightTheme);

            EditMessageCommand           = new RelayCommand(msg => StartEdit(msg as ChatMessage), msg => msg is ChatMessage m && m.IsSentByMe);
            ConfirmEditCommand           = new RelayCommand(_ => ConfirmEdit(),     _ => IsEditing && !string.IsNullOrWhiteSpace(MessageInput));
            CancelEditCommand            = new RelayCommand(_ => CancelEdit(),      _ => IsEditing);
            DeleteMessageCommand         = new RelayCommand(msg => DeleteMessage(msg as ChatMessage), msg => msg is ChatMessage);
            AddReactionCommand           = new RelayCommand(p => DoReact(p as object[]), _ => SelectedContact != null);
            ToggleSelectModeCommand      = new RelayCommand(_ => IsSelectionMode = !IsSelectionMode);
            ToggleMessageSelectCommand   = new RelayCommand(msg => ToggleSelect(msg as ChatMessage));
            DeleteSelectedCommand        = new RelayCommand(_ => DeleteSelected(), _ => IsSelectionMode);
            ToggleAttachMenuCommand      = new RelayCommand(_ => ShowAttachMenu = !ShowAttachMenu);
            SendVideoCommand             = new RelayCommand(_ => SendVideo(),        _ => SelectedContact != null);
            BroadcastProfileCommand      = new RelayCommand(_ => BroadcastProfile());

            _autoStart = AutoStartHelper.IsEnabled();
            LoadSettings();

            // Heartbeat checker
            new Timer(_ => CheckHeartbeats(), null, 5000, 5000);
        }

        private void PreloadProfile()
        {
            var cfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MAGA");
            Directory.CreateDirectory(cfg);
            var namePath   = Path.Combine(cfg, "name.txt");
            var avatarPath = Path.Combine(cfg, "avatar.png");
            var bioPath    = Path.Combine(cfg, "bio.txt");
            if (File.Exists(namePath))   { var n = File.ReadAllText(namePath).Trim(); if (!string.IsNullOrEmpty(n)) _myName = n; }
            if (File.Exists(avatarPath)) _myAvatar = File.ReadAllBytes(avatarPath);
            if (File.Exists(bioPath))    { var b = File.ReadAllText(bioPath, System.Text.Encoding.UTF8).Trim(); if (!string.IsNullOrEmpty(b)) _myBio = b.Length > User.MaxBioLength ? b.Substring(0, User.MaxBioLength) : b; }
        }

        // ─── Settings (uses AppSettingsStore) ──────────────────────
        private void LoadSettings()
        {
            _settings             = AppSettingsStore.Load();
            _notificationsEnabled = _settings.NotificationsEnabled;
            _stealthMode          = _settings.StealthMode;
            _isLightTheme         = _settings.IsLightTheme;

            // Global background
            if (!string.IsNullOrEmpty(_settings.GlobalBackgroundB64))
            {
                try { _globalChatBackground = Convert.FromBase64String(_settings.GlobalBackgroundB64); }
                catch { }
            }

            // Folders
            Folders.Clear();
            if (_settings.Folders != null)
                foreach (var f in _settings.Folders)
                    Folders.Add(f);
            RebuildFolderFilters();
        }

        private void SaveSettings()
        {
            if (_settings == null) _settings = new AppSettings();
            _settings.NotificationsEnabled = _notificationsEnabled;
            _settings.StealthMode          = _stealthMode;
            _settings.IsLightTheme         = _isLightTheme;
            _settings.GlobalBackgroundB64  = _globalChatBackground != null
                ? Convert.ToBase64String(_globalChatBackground) : null;
            _settings.Folders = new List<ChatFolder>(Folders);

            // Save per-contact backgrounds
            _settings.ContactBackgrounds = new List<ContactBgEntry>();
            foreach (var c in Contacts)
            {
                if (c.ChatBackground?.Length > 0)
                    _settings.ContactBackgrounds.Add(new ContactBgEntry
                    {
                        ContactId    = c.Id,
                        BackgroundB64 = Convert.ToBase64String(c.ChatBackground)
                    });
            }
            AppSettingsStore.Save(_settings);
        }

        // Keep old method name for Dispose() reference
        private void SaveExtendedSettings() => SaveSettings();

        private void RestoreContactSettings(User user)
        {
            if (_settings == null) return;
            // Restore background
            if (_settings.ContactBackgrounds != null)
            {
                var entry = _settings.ContactBackgrounds.FirstOrDefault(e => e.ContactId == user.Id);
                if (entry != null && !string.IsNullOrEmpty(entry.BackgroundB64))
                {
                    try { user.ChatBackground = Convert.FromBase64String(entry.BackgroundB64); }
                    catch { }
                }
            }
            // Restore folder assignment
            if (_settings.Folders != null)
            {
                foreach (var folder in _settings.Folders)
                {
                    if (folder.ContactIds?.Contains(user.Id) == true)
                    {
                        user.FolderName = folder.Name;
                        break;
                    }
                }
            }
        }

        // ─── Setup ─────────────────────────────────────────────────
        private void StartApp()
        {
            if (string.IsNullOrWhiteSpace(MyName)) return;
            LoadSavedProfile();
            AppLogger.Info("App started");
            _server.MessageReceived += OnMessageReceived;
            _server.Start();
            _discovery.PeerDiscovered    += OnPeerDiscovered;
            _discovery.PeerDisconnected  += OnPeerDisconnected;
            _discovery.Start(_myId, _myName, _myAvatar != null ? Convert.ToBase64String(_myAvatar) : "", _server.Port);
            _discovery.UpdateBio(_myBio);
            if (_stealthMode) _discovery.SetStealth(true);
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
            if (!string.IsNullOrWhiteSpace(MyBio))
                File.WriteAllText(Path.Combine(cfg, "bio.txt"), MyBio, System.Text.Encoding.UTF8);
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
            BroadcastProfile();
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
                        AvatarBytes = avatar,
                        Bio         = string.IsNullOrEmpty(packet.SenderBio) ? null :
                                      (packet.SenderBio.Length > User.MaxBioLength ? packet.SenderBio.Substring(0, User.MaxBioLength) : packet.SenderBio)
                    };
                    // Restore per-contact background + folder from saved settings
                    RestoreContactSettings(user);
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
                    if (!string.IsNullOrEmpty(packet.SenderBio))
                        existing.Bio = packet.SenderBio.Length > User.MaxBioLength ? packet.SenderBio.Substring(0, User.MaxBioLength) : packet.SenderBio;
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
                // ── Handle control packets that don't require a visible chat message ─
                switch (packet.PacketType)
                {
                    case "ACK":
                        // Mark the message as delivered
                        foreach (var kv in _chatHistory)
                        {
                            var m = kv.Value.FirstOrDefault(x => x.Id == packet.MessageId);
                            if (m != null) { m.IsDelivered = true; return; }
                        }
                        return;

                    case "READ":
                        // Mark message as read
                        foreach (var kv in _chatHistory)
                        {
                            var m = kv.Value.FirstOrDefault(x => x.Id == packet.MessageId);
                            if (m != null) { m.IsDelivered = true; m.IsRead = true; return; }
                        }
                        return;

                    case "EDIT":
                        foreach (var kv in _chatHistory)
                        {
                            var m = kv.Value.FirstOrDefault(x => x.Id == packet.MessageId);
                            if (m != null)
                            {
                                m.Content  = packet.Content;
                                m.IsEdited = true;
                                SaveContactHistory(kv.Key);
                                return;
                            }
                        }
                        return;

                    case "DELETE":
                        foreach (var kv in _chatHistory)
                        {
                            var m = kv.Value.FirstOrDefault(x => x.Id == packet.MessageId);
                            if (m != null)
                            {
                                kv.Value.Remove(m);
                                if (_selectedContact?.Id == kv.Key)
                                    CurrentMessages.Remove(m);
                                SaveContactHistory(kv.Key);
                                return;
                            }
                        }
                        return;

                    case "REACT":
                        foreach (var kv in _chatHistory)
                        {
                            var m = kv.Value.FirstOrDefault(x => x.Id == packet.MessageId);
                            if (m != null)
                            {
                                m.AddReaction(packet.Content, packet.Extra ?? packet.SenderId);
                                return;
                            }
                        }
                        return;

                    case "PROFILE":
                    {
                        var u = Contacts.FirstOrDefault(c => c.Id == packet.SenderId);
                        if (u != null)
                        {
                            if (!string.IsNullOrEmpty(packet.SenderName)) u.Username = packet.SenderName;
                            if (!string.IsNullOrEmpty(packet.SenderAvatar))
                                try { u.AvatarBytes = Convert.FromBase64String(packet.SenderAvatar); } catch { }
                            if (!string.IsNullOrEmpty(packet.SenderBio))
                                u.Bio = packet.SenderBio.Length > User.MaxBioLength ? packet.SenderBio.Substring(0, User.MaxBioLength) : packet.SenderBio;
                        }
                        return;
                    }
                }

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
                        try { sender.AvatarBytes = Convert.FromBase64String(packet.SenderAvatar); } catch { }
                    if (!string.IsNullOrEmpty(packet.SenderBio))
                        sender.Bio = packet.SenderBio.Length > User.MaxBioLength ? packet.SenderBio.Substring(0, User.MaxBioLength) : packet.SenderBio;
                    Contacts.Add(sender);
                    ApplySearch();
                }
                else
                {
                    sender.IsOnline  = true;
                    sender.LastSeen  = DateTime.Now;
                    sender.IpAddress = !string.IsNullOrEmpty(packet.IPv4) ? packet.IPv4 : senderIp;
                    if (!string.IsNullOrEmpty(packet.SenderAvatar))
                        try { sender.AvatarBytes = Convert.FromBase64String(packet.SenderAvatar); } catch { }
                    if (!string.IsNullOrEmpty(packet.SenderBio))
                        sender.Bio = packet.SenderBio.Length > User.MaxBioLength ? packet.SenderBio.Substring(0, User.MaxBioLength) : packet.SenderBio;
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
                            Id         = packet.MessageId ?? Guid.NewGuid().ToString(),
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
                            Id         = packet.MessageId ?? Guid.NewGuid().ToString(),
                            SenderId   = packet.SenderId,
                            SenderName = packet.SenderName,
                            Type       = MessageType.File,
                            FileBytes  = fileBytes,
                            FileName   = packet.FileName ?? "file",
                            Timestamp  = DateTime.Now,
                            IsSentByMe = false
                        };
                        break;

                    case "VIDEO":
                        byte[] vidBytes = null;
                        try { vidBytes = Convert.FromBase64String(packet.Content ?? ""); } catch { }
                        msg = new ChatMessage
                        {
                            Id         = packet.MessageId ?? Guid.NewGuid().ToString(),
                            SenderId   = packet.SenderId,
                            SenderName = packet.SenderName,
                            Type       = MessageType.Video,
                            FileBytes  = vidBytes,
                            FileName   = packet.FileName ?? "video",
                            Timestamp  = DateTime.Now,
                            IsSentByMe = false
                        };
                        break;

                    case "VOICE":
                        msg = new ChatMessage
                        {
                            Id         = packet.MessageId ?? Guid.NewGuid().ToString(),
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
                            Id         = packet.MessageId ?? Guid.NewGuid().ToString(),
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
                {
                    CurrentMessages.Add(msg);
                    // Send READ ACK immediately since chat is open
                    SendAck(sender, packet.MessageId, "READ");
                }
                else
                {
                    // Send delivery ACK
                    SendAck(sender, packet.MessageId, "ACK");
                    // Increment unread counter
                    sender.UnreadCount++;
                    OnPropChanged(nameof(TotalUnread));
                }

                // ── Desktop notification ──────────────────────────────
                if (_notificationsEnabled && _selectedContact?.Id != packet.SenderId)
                {
                    var title   = packet.SenderName ?? "Новое сообщение";
                    var content = msg.Content ?? "";
                    var body    = msg.Type == MessageType.Text
                        ? (content.Length > 80 ? content.Substring(0, 77) + "…" : content)
                        : msg.Type == MessageType.Image ? "📷 Изображение"
                        : msg.Type == MessageType.File  ? $"📎 {msg.FileName}"
                        : msg.Type == MessageType.Video ? $"🎬 {msg.FileName}"
                        : "🎙 Голосовое сообщение";
                    NotificationRequired?.Invoke(title, body);
                }

                AppLogger.Info($"Message received type={packet.PacketType} from peer");
            });
        }

        private void SendAck(User to, string messageId, string type)
        {
            if (to == null || string.IsNullOrEmpty(messageId)) return;
            var ack = new NetworkPacket
            {
                PacketType = type,
                MessageId  = messageId,
                SenderId   = _myId,
                SenderName = _myName,
                IPv4       = NetworkHelper.GetIPv4(),
                TcpPort    = _server.Port,
                Timestamp  = DateTime.Now.ToString("o")
            };
            Task.Run(() => TcpChatClient.Send(to.IpAddress, to.TcpPort, ack));
        }

        // ─── Messaging ─────────────────────────────────────────────
        private void SendText()
        {
            if (string.IsNullOrWhiteSpace(MessageInput) || _selectedContact == null) return;
            if (MessageInput.Length > 16000)
            {
                System.Windows.MessageBox.Show("Сообщение слишком длинное. Максимальный размер — 16 000 символов.",
                    "Ограничение", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Check if editing an existing message
            if (_editingMessage != null)
            {
                ConfirmEdit();
                return;
            }

            var text = MessageInput;
            MessageInput = "";
            var msg    = new ChatMessage { Type = MessageType.Text, Content = text };
            var packet = BuildPacket("TEXT");
            packet.MessageId = msg.Id = Guid.NewGuid().ToString();
            packet.Content   = text;
            AddMyMessage(msg);
            Task.Run(() =>
            {
                var ok = TcpChatClient.Send(_selectedContact.IpAddress, _selectedContact.TcpPort, packet);
                if (!ok)
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        msg.HasSendError = true;
                        AppLogger.Warn($"Failed to send TEXT message to {_selectedContact.Username}");
                    });
            });
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

        private async void SendImage()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp",
                Title  = "Отправить изображение"
            };
            if (dlg.ShowDialog() != true) return;
            var contact = _selectedContact;
            if (contact == null) return;
            ShowAttachMenu = false;
            byte[] bytes = null;
            try { bytes = await Task.Run(() => File.ReadAllBytes(dlg.FileName)); }
            catch (Exception ex)
            {
                AppLogger.Error("SendImage read", ex);
                AddErrorMessage($"Ошибка чтения файла: {ex.Message}");
                return;
            }
            var b64    = Convert.ToBase64String(bytes);
            var msg    = new ChatMessage { Type = MessageType.Image, ImageBytes = bytes, FileName = Path.GetFileName(dlg.FileName) };
            var packet = BuildPacket("IMAGE");
            packet.MessageId = msg.Id = Guid.NewGuid().ToString();
            packet.Content   = b64;
            packet.FileName  = msg.FileName;
            AddMyMessage(msg);
            bool ok = await Task.Run(() => TcpChatClient.Send(contact.IpAddress, contact.TcpPort, packet));
            if (!ok) { msg.HasSendError = true; AppLogger.Warn($"Failed to send IMAGE to {contact.Username}"); }
        }

        private async void SendFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Отправить файл" };
            if (dlg.ShowDialog() != true) return;
            var contact = _selectedContact;
            if (contact == null) return;
            ShowAttachMenu = false;
            byte[] bytes = null;
            try { bytes = await Task.Run(() => File.ReadAllBytes(dlg.FileName)); }
            catch (Exception ex)
            {
                AppLogger.Error("SendFile read", ex);
                AddErrorMessage($"Ошибка чтения файла: {ex.Message}");
                return;
            }
            var b64    = Convert.ToBase64String(bytes);
            var msg    = new ChatMessage { Type = MessageType.File, FileBytes = bytes, FileName = Path.GetFileName(dlg.FileName) };
            var packet = BuildPacket("FILE");
            packet.MessageId = msg.Id = Guid.NewGuid().ToString();
            packet.Content   = b64;
            packet.FileName  = msg.FileName;
            AddMyMessage(msg);
            bool ok = await Task.Run(() => TcpChatClient.Send(contact.IpAddress, contact.TcpPort, packet));
            if (!ok) { msg.HasSendError = true; AppLogger.Warn($"Failed to send FILE to {contact.Username}"); }
        }

        private async void SendVideo()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Video|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.webm",
                Title  = "Отправить видео"
            };
            if (dlg.ShowDialog() != true) return;
            var contact = _selectedContact;
            if (contact == null) return;
            ShowAttachMenu = false;
            byte[] bytes = null;
            try { bytes = await Task.Run(() => File.ReadAllBytes(dlg.FileName)); }
            catch (Exception ex)
            {
                AppLogger.Error("SendVideo read", ex);
                AddErrorMessage($"Ошибка чтения файла: {ex.Message}");
                return;
            }
            var b64    = Convert.ToBase64String(bytes);
            var msg    = new ChatMessage { Type = MessageType.Video, FileBytes = bytes, FileName = Path.GetFileName(dlg.FileName) };
            var packet = BuildPacket("VIDEO");
            packet.MessageId = msg.Id = Guid.NewGuid().ToString();
            packet.Content   = b64;
            packet.FileName  = msg.FileName;
            AddMyMessage(msg);
            bool ok = await Task.Run(() => TcpChatClient.Send(contact.IpAddress, contact.TcpPort, packet));
            if (!ok) { msg.HasSendError = true; AppLogger.Warn($"Failed to send VIDEO to {contact.Username}"); }
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
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Ошибка",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
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

        // ─── Manual IP connect ─────────────────────────────────────
        private void ConnectByIp()
        {
            var ip = ManualIpInput?.Trim();
            if (string.IsNullOrEmpty(ip)) return;
            // Send a discovery packet directly to that IP so they show up in the contact list
            var packet = new NetworkPacket
            {
                PacketType   = "DISCOVER",
                SenderId     = _myId,
                SenderName   = _myName,
                SenderAvatar = _myAvatar != null ? Convert.ToBase64String(_myAvatar) : "",
                MacAddress   = NetworkHelper.GetMacAddress(),
                Hostname     = NetworkHelper.GetHostname(),
                IPv4         = NetworkHelper.GetIPv4(),
                IPv6         = NetworkHelper.GetIPv6(),
                TcpPort      = _server.Port,
                Timestamp    = DateTime.Now.ToString("o")
            };
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var data = System.Text.Encoding.UTF8.GetBytes(JsonHelper.Serialize(packet));
                    using (var udp = new System.Net.Sockets.UdpClient())
                        udp.Send(data, data.Length, ip, 45678);
                }
                catch { }
            });
            ManualIpInput = "";
        }

        // ─── Emoji picker ──────────────────────────────────────────
        private void InsertEmoji(string emoji)
        {
            if (string.IsNullOrEmpty(emoji)) return;
            MessageInput = (MessageInput ?? "") + emoji;
            IsEmojiPickerOpen = false;
        }

        // ─── Chat backgrounds ─────────────────────────────────────
        private void PickGlobalBackground()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp",
                Title  = "Выберите обои чата (глобально)"
            };
            if (dlg.ShowDialog() != true) return;
            try { GlobalChatBackground = File.ReadAllBytes(dlg.FileName); }
            catch { }
        }

        private void PickContactBackground()
        {
            if (_selectedContact == null) return;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp",
                Title  = "Выберите обои для этого чата"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _selectedContact.ChatBackground = File.ReadAllBytes(dlg.FileName);
                OnPropChanged(nameof(ActiveChatBackground));
                SaveSettings();
            }
            catch { }
        }

        private void ClearContactBackground()
        {
            if (_selectedContact == null) return;
            _selectedContact.ChatBackground = null;
            OnPropChanged(nameof(ActiveChatBackground));
            SaveSettings();
        }

        // ─── Folders ──────────────────────────────────────────────
        private void CreateFolder()
        {
            var dlg = new M_A_G_A.Views.PasswordDialog("Введите название новой папки:");
            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Password)) return;
            var folder = new ChatFolder { Name = dlg.Password.Trim() };
            Folders.Add(folder);
            RebuildFolderFilters();
            SaveSettings();
        }

        private void DeleteFolder(ChatFolder folder)
        {
            if (folder == null) return;
            // Unassign all contacts in this folder
            foreach (var c in Contacts.Where(c => c.FolderName == folder.Name))
                c.FolderName = null;
            Folders.Remove(folder);
            if (SelectedFolderFilter == folder.Name) SelectedFolderFilter = null;
            RebuildFolderFilters();
            SaveSettings();
        }

        private void SetContactFolder(string folderName)
        {
            if (_selectedContact == null) return;
            _selectedContact.FolderName = string.IsNullOrEmpty(folderName) ? null : folderName;
            // Sync folder.ContactIds for persistence
            foreach (var f in Folders)
            {
                if (!f.ContactIds.Contains(_selectedContact.Id) && f.Name == folderName)
                    f.ContactIds.Add(_selectedContact.Id);
                else if (f.ContactIds.Contains(_selectedContact.Id) && f.Name != folderName)
                    f.ContactIds.Remove(_selectedContact.Id);
            }
            ApplySearch();
            SaveSettings();
        }

        private void RebuildFolderFilters()
        {
            FolderFilters.Clear();
            FolderFilters.Add(null);   // "All"
            foreach (var f in Folders)
                FolderFilters.Add(f.Name);
        }

        // ─── Password ─────────────────────────────────────────────
        private void SetPassword()
        {
            var dlg = new M_A_G_A.Views.PasswordDialog("Введите новый пароль (пустой — убрать защиту):");
            if (dlg.ShowDialog() != true) return;
            if (_settings == null) _settings = new AppSettings();
            var pwd = dlg.Password;
            _settings.PasswordHash = string.IsNullOrEmpty(pwd)
                ? null
                : EncryptionHelper.HashPassword(pwd);
            AppSettingsStore.Save(_settings);
            OnPropChanged(nameof(HasPassword));
        }

        private void ClearPassword()
        {
            var ans = System.Windows.MessageBox.Show("Убрать защиту паролем?", "Подтверждение",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (ans != System.Windows.MessageBoxResult.Yes) return;
            if (_settings == null) _settings = new AppSettings();
            _settings.PasswordHash = null;
            AppSettingsStore.Save(_settings);
            OnPropChanged(nameof(HasPassword));
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
                System.Windows.MessageBox.Show(ex.Message, "Ошибка",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
                System.Windows.MessageBox.Show("История успешно импортирована.", "Готово");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Ошибка",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        // ─── Message Edit / Delete / React ─────────────────────────
        private void StartEdit(ChatMessage msg)
        {
            if (msg == null || !msg.IsSentByMe) return;
            EditingMessage = msg;
            MessageInput   = msg.Content ?? "";
        }

        private void ConfirmEdit()
        {
            if (_editingMessage == null || _selectedContact == null) return;
            var newText = MessageInput;
            MessageInput    = "";
            _editingMessage.Content  = newText;
            _editingMessage.IsEdited = true;
            var id  = _editingMessage.Id;
            EditingMessage  = null;
            var packet = BuildPacket("EDIT");
            packet.MessageId = id;
            packet.Content   = newText;
            var contact = _selectedContact;
            Task.Run(() => TcpChatClient.Send(contact.IpAddress, contact.TcpPort, packet));
            SaveContactHistory(_selectedContact.Id);
        }

        private void CancelEdit()
        {
            EditingMessage = null;
            MessageInput   = "";
        }

        private void DeleteMessage(ChatMessage msg)
        {
            if (msg == null || _selectedContact == null) return;
            var history = GetHistory(_selectedContact.Id);
            history.Remove(msg);
            CurrentMessages.Remove(msg);
            SaveContactHistory(_selectedContact.Id);
            if (msg.IsSentByMe)
            {
                var packet = BuildPacket("DELETE");
                packet.MessageId = msg.Id;
                var contact = _selectedContact;
                Task.Run(() => TcpChatClient.Send(contact.IpAddress, contact.TcpPort, packet));
            }
        }

        private void DoReact(object[] args)
        {
            // args[0] = ChatMessage, args[1] = emoji string
            if (args == null || args.Length < 2) return;
            var msg   = args[0] as ChatMessage;
            var emoji = args[1] as string;
            if (msg == null || string.IsNullOrEmpty(emoji) || _selectedContact == null) return;
            msg.AddReaction(emoji, _myId);
            var packet = BuildPacket("REACT");
            packet.MessageId = msg.Id;
            packet.Content   = emoji;
            packet.Extra     = _myId;
            var contact = _selectedContact;
            Task.Run(() => TcpChatClient.Send(contact.IpAddress, contact.TcpPort, packet));
        }

        // ─── Selection ─────────────────────────────────────────────
        private void ToggleSelect(ChatMessage msg)
        {
            if (msg == null) return;
            msg.IsSelected = !msg.IsSelected;
        }

        private void ClearSelection()
        {
            foreach (var m in CurrentMessages)
                m.IsSelected = false;
        }

        private void DeleteSelected()
        {
            var toDelete = CurrentMessages.Where(m => m.IsSelected).ToList();
            foreach (var m in toDelete)
                DeleteMessage(m);
            IsSelectionMode = false;
        }

        // ─── Profile broadcast ─────────────────────────────────────
        private void BroadcastProfile()
        {
            if (!IsSetupDone) return;
            SaveProfile();
            var packet = BuildPacket("PROFILE");
            packet.SenderBio = _myBio;
            foreach (var c in Contacts.Where(c => c.IsOnline).ToList())
            {
                var contact = c;
                Task.Run(() => TcpChatClient.Send(contact.IpAddress, contact.TcpPort, packet));
            }
        }

        // ─── Error message helper ──────────────────────────────────
        private void AddErrorMessage(string error)
        {
            if (_selectedContact == null) return;
            var msg = new ChatMessage
            {
                Id          = Guid.NewGuid().ToString(),
                SenderId    = _myId,
                SenderName  = _myName,
                Type        = MessageType.Text,
                Content     = $"⚠️ {error}",
                Timestamp   = DateTime.Now,
                IsSentByMe  = true,
                HasSendError= true
            };
            CurrentMessages.Add(msg);
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
            if (string.IsNullOrEmpty(msg.Id)) msg.Id = Guid.NewGuid().ToString();
            msg.SenderId   = _myId;
            msg.SenderName = _myName;
            if (msg.Timestamp == default) msg.Timestamp = DateTime.Now;
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
            SenderBio    = _myBio,
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
            {
                // folder filter
                if (_selectedFolderFilter != null && c.FolderName != _selectedFolderFilter)
                    return false;
                // text filter
                return string.IsNullOrEmpty(q)
                    || c.Username.ToLower().Contains(q)
                    || (c.Hostname ?? "").ToLower().Contains(q)
                    || (c.IpAddress ?? "").Contains(q);
            }))
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
            SaveSettings();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
