using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using M_A_G_A.Models;

namespace M_A_G_A.Helpers
{
    /// <summary>
    /// Persists chat history per contact as JSON files in the local AppData directory.
    /// </summary>
    public static class HistoryHelper
    {
        private static readonly string HistoryDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MAGA", "history");

        static HistoryHelper()
        {
            Directory.CreateDirectory(HistoryDir);
        }

        // ─── DTO stored on disk ──────────────────────────────────────────
        [DataContract]
        private class MessageDto
        {
            [DataMember] public string Id         { get; set; }
            [DataMember] public string SenderId   { get; set; }
            [DataMember] public string SenderName { get; set; }
            [DataMember] public string Type       { get; set; }
            [DataMember] public string Content    { get; set; }   // text or base64 audio
            [DataMember] public string ImageB64   { get; set; }   // for IMAGE
            [DataMember] public string FileB64    { get; set; }   // for FILE
            [DataMember] public string FileName   { get; set; }
            [DataMember] public string Timestamp  { get; set; }
            [DataMember] public bool   IsSentByMe { get; set; }
        }

        [DataContract]
        private class HistoryFile
        {
            [DataMember] public List<MessageDto> Messages { get; set; } = new List<MessageDto>();
        }

        // ─── Public API ──────────────────────────────────────────────────

        public static List<ChatMessage> LoadHistory(string contactId)
        {
            var result = new List<ChatMessage>();
            var path = GetPath(contactId);
            if (!File.Exists(path)) return result;
            try
            {
                var hf = Deserialize<HistoryFile>(File.ReadAllText(path, Encoding.UTF8));
                foreach (var d in hf.Messages)
                    result.Add(DtoToMessage(d));
            }
            catch { /* corrupted file – return empty */ }
            return result;
        }

        public static void SaveHistory(string contactId, IEnumerable<ChatMessage> messages)
        {
            try
            {
                var hf = new HistoryFile();
                foreach (var m in messages)
                    hf.Messages.Add(MessageToDto(m));
                File.WriteAllText(GetPath(contactId), Serialize(hf), Encoding.UTF8);
            }
            catch { }
        }

        public static IEnumerable<string> GetSavedContactIds()
        {
            foreach (var f in Directory.GetFiles(HistoryDir, "*.json"))
                yield return Path.GetFileNameWithoutExtension(f);
        }

        /// <summary>Export all history files as a dictionary {contactId → json string}.</summary>
        public static Dictionary<string, string> ExportAll()
        {
            var result = new Dictionary<string, string>();
            foreach (var f in Directory.GetFiles(HistoryDir, "*.json"))
                result[Path.GetFileNameWithoutExtension(f)] = File.ReadAllText(f, Encoding.UTF8);
            return result;
        }

        /// <summary>Import history; merges messages (deduplicated by Id).</summary>
        public static void ImportHistory(string contactId, string json)
        {
            try
            {
                var incoming = Deserialize<HistoryFile>(json);
                var existing = LoadHistory(contactId);
                var ids = new HashSet<string>();
                foreach (var m in existing) ids.Add(m.Id);
                foreach (var dto in incoming.Messages)
                {
                    if (!ids.Contains(dto.Id))
                    {
                        existing.Add(DtoToMessage(dto));
                        ids.Add(dto.Id);
                    }
                }
                // Sort by time
                existing.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                SaveHistory(contactId, existing);
            }
            catch { }
        }

        // ─── Private helpers ─────────────────────────────────────────────

        private static string GetPath(string contactId)
            => Path.Combine(HistoryDir, contactId + ".json");

        private static MessageDto MessageToDto(ChatMessage m) => new MessageDto
        {
            Id         = m.Id,
            SenderId   = m.SenderId,
            SenderName = m.SenderName,
            Type       = m.Type.ToString(),
            Content    = m.Content,
            ImageB64   = m.ImageBytes != null ? Convert.ToBase64String(m.ImageBytes) : null,
            FileB64    = m.FileBytes  != null ? Convert.ToBase64String(m.FileBytes)  : null,
            FileName   = m.FileName,
            Timestamp  = m.Timestamp.ToString("o"),
            IsSentByMe = m.IsSentByMe
        };

        private static ChatMessage DtoToMessage(MessageDto d)
        {
            var type = MessageType.Text;
            Enum.TryParse(d.Type, out type);
            return new ChatMessage
            {
                Id         = d.Id ?? Guid.NewGuid().ToString(),
                SenderId   = d.SenderId,
                SenderName = d.SenderName,
                Type       = type,
                Content    = d.Content,
                ImageBytes = !string.IsNullOrEmpty(d.ImageB64) ? Convert.FromBase64String(d.ImageB64) : null,
                FileBytes  = !string.IsNullOrEmpty(d.FileB64)  ? Convert.FromBase64String(d.FileB64)  : null,
                FileName   = d.FileName,
                Timestamp  = DateTime.TryParse(d.Timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : DateTime.Now,
                IsSentByMe = d.IsSentByMe
            };
        }

        private static string Serialize<T>(T obj)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(ms, obj);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private static T Deserialize<T>(string json)
        {
            using (var ms = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(json)))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(ms);
        }
    }
}
