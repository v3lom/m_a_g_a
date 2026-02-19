using System;
using System.ComponentModel;

namespace M_A_G_A.Models
{
    public class User : INotifyPropertyChanged
    {
        private bool _isOnline;
        private DateTime _lastSeen;
        private byte[] _avatarBytes;

        public string Id { get; set; }
        public string Username { get; set; }
        public string IpAddress { get; set; }
        public int TcpPort { get; set; }

        public byte[] AvatarBytes
        {
            get => _avatarBytes;
            set { _avatarBytes = value; OnPropertyChanged(nameof(AvatarBytes)); }
        }

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
