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
    /// Centralised persistent settings store (AppData/MAGA/settings.json).
    /// Covers: notifications, stealth, theme, folders, global background,
    ///         storage mode (SQLite/JSON), language (ru/en), per-chat encryption keys.
    /// </summary>
    public static class AppSettingsStore
    {
        private static readonly string Path =
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MAGA", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(Path)) return new AppSettings();
                var json = File.ReadAllText(Path, Encoding.UTF8);
                return Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch { return new AppSettings(); }
        }

        public static void Save(AppSettings s)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                File.WriteAllText(Path, Serialize(s), Encoding.UTF8);
            }
            catch { }
        }

        private static string Serialize<T>(T obj)
        {
            using (var ms = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(ms, obj);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private static T Deserialize<T>(string json) where T : class
        {
            try
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(ms);
            }
            catch { return null; }
        }
    }

    [DataContract]
    public class AppSettings
    {
        [DataMember] public bool   NotificationsEnabled  { get; set; } = true;
        [DataMember] public bool   StealthMode           { get; set; } = false;
        [DataMember] public bool   IsLightTheme          { get; set; } = false;
        [DataMember] public string GlobalBackgroundB64   { get; set; } = null;
        [DataMember] public List<ChatFolder> Folders     { get; set; } = new List<ChatFolder>();
        [DataMember] public List<ContactBgEntry> ContactBackgrounds { get; set; } = new List<ContactBgEntry>();
        [DataMember] public bool   AutoStartConfigured   { get; set; } = false;

        /// <summary>When true, uses JSON files instead of the default SQLite database.</summary>
        [DataMember] public bool   UseJsonStorage        { get; set; } = false;

        /// <summary>UI language: "ru" (default) or "en".</summary>
        [DataMember] public string AppLanguage           { get; set; } = "ru";

        /// <summary>
        /// Per-contact encryption password hashes.
        /// Key = contactId, Value = AES password (stored as a random base64 key).
        /// </summary>
        [DataMember] public List<ContactEncryptionEntry> EncryptionKeys { get; set; } = new List<ContactEncryptionEntry>();
    }

    [DataContract]
    public class ContactBgEntry
    {
        [DataMember] public string ContactId   { get; set; }
        [DataMember] public string BackgroundB64 { get; set; }
    }

    [DataContract]
    public class ContactEncryptionEntry
    {
        [DataMember] public string ContactId      { get; set; }
        /// <summary>Base64-encoded 32-byte AES key for this dialog.</summary>
        [DataMember] public string EncryptionKey  { get; set; }
    }
}
