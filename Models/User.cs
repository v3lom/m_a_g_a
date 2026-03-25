using System;
using System.ComponentModel;

namespace M_A_G_A.Models
{
    public class User : INotifyPropertyChanged
    {
        private bool _isOnline;
        private DateTime _lastSeen;
        private byte[] _avatarBytes;
        private byte[] _chatBackground;
        private string _folderName;
        private string _bio;
        private int _unreadCount;

        public string Id { get; set; }          // deterministic MAC+hostname hash
        public string Username { get; set; }
        public string MacAddress { get; set; }  // physical MAC
        public string Hostname { get; set; }    // machine hostname
        public string IpAddress { get; set; }   // v4
        public string IPv6 { get; set; }        // v6 (may be empty)
        public int TcpPort { get; set; }

        /// <summary>User bio / status text (max 1024 chars).</summary>
        public const int MaxBioLength = 1024;

        // ─── Per-dialog encryption ────────────────────────────────────
        /// <summary>When true, messages to/from this contact are AES-encrypted.</summary>
        public bool EncryptionEnabled { get; set; }
        /// <summary>Base64-encoded 32-byte AES key used for this dialog. Null = no encryption.</summary>
        public string ChatEncryptionKey { get; set; }

        public string Bio
        {
            get => _bio;
            set
            {
                var trimmed = value?.Length > MaxBioLength ? value.Substring(0, MaxBioLength) : value;
                _bio = trimmed;
                OnPropertyChanged(nameof(Bio));
                OnPropertyChanged(nameof(HasBio));
            }
        }
        public bool HasBio => !string.IsNullOrWhiteSpace(_bio);

        public byte[] AvatarBytes
        {
            get => _avatarBytes;
            set { _avatarBytes = value; OnPropertyChanged(nameof(AvatarBytes)); }
        }

        /// <summary>Local-only per-chat background image (not sent over network).</summary>
        public byte[] ChatBackground
        {
            get => _chatBackground;
            set { _chatBackground = value; OnPropertyChanged(nameof(ChatBackground)); OnPropertyChanged(nameof(HasChatBackground)); }
        }
        public bool HasChatBackground => _chatBackground != null && _chatBackground.Length > 0;

        /// <summary>Name of the folder this contact is placed in (null = uncategorized).</summary>
        public string FolderName
        {
            get => _folderName;
            set { _folderName = value; OnPropertyChanged(nameof(FolderName)); }
        }

        /// <summary>Number of unread messages from this contact.</summary>
        public int UnreadCount
        {
            get => _unreadCount;
            set
            {
                _unreadCount = value < 0 ? 0 : value;
                OnPropertyChanged(nameof(UnreadCount));
                OnPropertyChanged(nameof(HasUnread));
            }
        }
        public bool HasUnread => _unreadCount > 0;

        public bool IsOnline
        {
            get => _isOnline;
            set { _isOnline = value; OnPropertyChanged(nameof(IsOnline)); OnPropertyChanged(nameof(StatusText)); }
        }

        public DateTime LastSeen
        {
            get => _lastSeen;
            set { _lastSeen = value; OnPropertyChanged(nameof(LastSeen)); OnPropertyChanged(nameof(StatusText)); }
        }

        public string StatusText => IsOnline ? "онлайн" : $"был(а) {FormatLastSeen()}";

        private string FormatLastSeen()
        {
            if (_lastSeen == DateTime.MinValue) return "давно";
            var diff = DateTime.Now - _lastSeen;
            if (diff.TotalMinutes < 1) return "только что";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} мин назад";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} ч назад";
            return _lastSeen.ToString("dd MMM");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
