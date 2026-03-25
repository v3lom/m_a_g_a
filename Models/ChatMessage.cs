using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;

namespace M_A_G_A.Models
{
    public enum MessageType { Text, Voice, Image, File, Video }

    public class ChatMessage : INotifyPropertyChanged
    {
        private bool _isPlaying;
        private bool _isDelivered;
        private bool _isRead;
        private bool _isEdited;
        private bool _isSelected;
        private bool _hasSendError;
        private bool _isSending;
        private int  _sendProgress;   // 0–100
        private string _content;

        public string Id          { get; set; } = Guid.NewGuid().ToString();
        public string SenderId    { get; set; }
        public string SenderName  { get; set; }
        public MessageType Type   { get; set; }

        public string Content
        {
            get => _content;
            set { _content = value; OnPropertyChanged(nameof(Content)); }
        }

        public byte[] ImageBytes  { get; set; }   // decoded image for IMAGE messages
        public byte[] FileBytes   { get; set; }   // decoded bytes for FILE/VIDEO messages
        public string FileName    { get; set; }   // original filename
        public DateTime Timestamp { get; set; }
        public bool IsSentByMe   { get; set; }

        // ─── Delivery / read status ───────────────────────────────────
        public bool IsDelivered
        {
            get => _isDelivered;
            set { _isDelivered = value; OnPropertyChanged(nameof(IsDelivered)); OnPropertyChanged(nameof(StatusGlyph)); }
        }
        public bool IsRead
        {
            get => _isRead;
            set { _isRead = value; OnPropertyChanged(nameof(IsRead)); OnPropertyChanged(nameof(StatusGlyph)); }
        }

        // ─── Send progress ───────────────────────────────────────────
        /// <summary>True while the message / file is being sent over the network.</summary>
        public bool IsSending
        {
            get => _isSending;
            set { _isSending = value; OnPropertyChanged(nameof(IsSending)); OnPropertyChanged(nameof(StatusGlyph)); }
        }
        /// <summary>Upload progress 0–100 for file/image/video messages.</summary>
        public int SendProgress
        {
            get => _sendProgress;
            set { _sendProgress = value; OnPropertyChanged(nameof(SendProgress)); OnPropertyChanged(nameof(SendProgressText)); }
        }
        public string SendProgressText => _isSending ? $"{_sendProgress}%" : "";

        // ─── Editing ─────────────────────────────────────────────────
        public bool IsEdited
        {
            get => _isEdited;
            set { _isEdited = value; OnPropertyChanged(nameof(IsEdited)); }
        }

        // ─── Selection ───────────────────────────────────────────────
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        // ─── Send error ──────────────────────────────────────────────
        public bool HasSendError
        {
            get => _hasSendError;
            set { _hasSendError = value; OnPropertyChanged(nameof(HasSendError)); }
        }

        // ─── Reactions {emoji -> [userId, ...]} ─────────────────────
        private ObservableCollection<ReactionGroup> _reactions;
        public ObservableCollection<ReactionGroup> Reactions
        {
            get => _reactions ?? (_reactions = new ObservableCollection<ReactionGroup>());
        }
        public bool HasReactions => _reactions != null && _reactions.Count > 0;

        public void AddReaction(string emoji, string userId)
        {
            var group = Reactions.FirstOrDefault(r => r.Emoji == emoji);
            if (group == null)
            {
                Reactions.Add(new ReactionGroup(emoji, userId));
            }
            else if (!group.UserIds.Contains(userId))
            {
                group.UserIds.Add(userId);
                group.OnCountChanged();
            }
            OnPropertyChanged(nameof(HasReactions));
        }

        public void RemoveReaction(string emoji, string userId)
        {
            var group = Reactions.FirstOrDefault(r => r.Emoji == emoji);
            if (group == null) return;
            group.UserIds.Remove(userId);
            if (group.UserIds.Count == 0) Reactions.Remove(group);
            else group.OnCountChanged();
            OnPropertyChanged(nameof(HasReactions));
        }

        // ─── File text preview (first 40 lines) ──────────────────────
        private string _filePreview;
        private bool _filePreviewComputed;

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
        public bool IsVideo => Type == MessageType.Video;

        public string TimeFormatted => Timestamp.ToString("HH:mm");

        /// <summary>Delivery checkmark glyph shown on sent messages.</summary>
        public string StatusGlyph
        {
            get
            {
                if (!IsSentByMe) return "";
                if (_isSending)  return "⏳";
                if (IsRead)      return "✓✓";
                if (IsDelivered) return "✓";
                return "○";
            }
        }

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

    /// <summary>One emoji + the users who reacted with it.</summary>
    public class ReactionGroup : INotifyPropertyChanged
    {
        public string Emoji { get; }
        public List<string> UserIds { get; } = new List<string>();
        public int Count => UserIds.Count;

        public ReactionGroup(string emoji, string firstUserId)
        {
            Emoji = emoji;
            UserIds.Add(firstUserId);
        }

        internal void OnCountChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    // Allow FirstOrDefault on ObservableCollection<ReactionGroup> without pulling in extra using
    internal static class ReactionGroupExtensions
    {
        public static ReactionGroup FirstOrDefault(
            this System.Collections.ObjectModel.ObservableCollection<ReactionGroup> col,
            Func<ReactionGroup, bool> predicate)
        {
            foreach (var r in col)
                if (predicate(r)) return r;
            return null;
        }
    }
}

