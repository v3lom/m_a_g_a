using System;
using System.ComponentModel;

namespace M_A_G_A.Models
{
    public enum MessageType { Text, Voice }

    public class ChatMessage : INotifyPropertyChanged
    {
        private bool _isPlaying;

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public MessageType Type { get; set; }
        public string Content { get; set; }       // text or base64 audio
        public DateTime Timestamp { get; set; }
        public bool IsSentByMe { get; set; }

        public bool IsVoice => Type == MessageType.Voice;
        public bool IsText => Type == MessageType.Text;

        public string TimeFormatted => Timestamp.ToString("HH:mm");

        public bool IsPlaying
        {
            get => _isPlaying;
            set { _isPlaying = value; OnPropertyChanged(nameof(IsPlaying)); OnPropertyChanged(nameof(PlayButtonLabel)); }
        }

        public string PlayButtonLabel => _isPlaying ? "⏹" : "▶";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
