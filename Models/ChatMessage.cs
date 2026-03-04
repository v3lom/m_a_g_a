using System;
using System.ComponentModel;
using System.Text;

namespace M_A_G_A.Models
{
    public enum MessageType { Text, Voice, Image, File }

    public class ChatMessage : INotifyPropertyChanged
    {
        private bool _isPlaying;

        public string Id          { get; set; } = Guid.NewGuid().ToString();
        public string SenderId    { get; set; }
        public string SenderName  { get; set; }
        public MessageType Type   { get; set; }
        public string Content     { get; set; }   // text (may be markdown) or base64 audio
        public byte[] ImageBytes  { get; set; }   // decoded image for IMAGE messages
        public byte[] FileBytes   { get; set; }   // decoded bytes for FILE messages
        public string FileName    { get; set; }   // original filename
        public DateTime Timestamp { get; set; }
        public bool IsSentByMe   { get; set; }

        // ─── File text preview (first 40 lines) ──────────────────────
        private string _filePreview;
        private bool _filePreviewComputed;

        /// <summary>
        /// For FILE messages: the first 40 lines of the file if it is a plain-text file;
        /// otherwise null.  Download and open the full file to see the rest.
        /// </summary>
        public string FilePreview
        {
            get
            {
                if (_filePreviewComputed) return _filePreview;
                _filePreviewComputed = true;
                if (Type != MessageType.File || FileBytes == null || FileBytes.Length == 0)
                    return _filePreview = null;
                try
                {
                    // Heuristic: treat as text if fewer than 1 in 32 bytes is non-printable
                    int nonPrintable = 0;
                    int sample = Math.Min(FileBytes.Length, 512);
                    for (int i = 0; i < sample; i++)
                    {
                        byte b = FileBytes[i];
                        if (b != 9 && b != 10 && b != 13 && (b < 32 || b > 126))
                            nonPrintable++;
                    }
                    if (nonPrintable * 32 > sample)
                        return _filePreview = null;

                    var text = Encoding.UTF8.GetString(FileBytes);
                    var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    int take = Math.Min(lines.Length, 40);
                    _filePreview = string.Join("\n", lines, 0, take);
                    if (lines.Length > 40)
                        _filePreview += "\n…";
                }
                catch { }
                return _filePreview;
            }
        }

        public bool HasFilePreview => FilePreview != null;

        // Type helpers
        public bool IsText  => Type == MessageType.Text;
        public bool IsVoice => Type == MessageType.Voice;
        public bool IsImage => Type == MessageType.Image;
        public bool IsFile  => Type == MessageType.File;

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
