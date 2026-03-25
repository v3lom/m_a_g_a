using System.Collections.Generic;
using System.ComponentModel;

namespace M_A_G_A.Helpers
{
    /// <summary>
    /// Simple EN/RU localization. All UI strings are accessed via L["key"].
    /// The singleton raises PropertyChanged so data-bound controls update automatically.
    /// </summary>
    public class LocalizationManager : INotifyPropertyChanged
    {
        public static readonly LocalizationManager Instance = new LocalizationManager();

        private string _lang = "ru";

        private LocalizationManager() { }

        public string Language
        {
            get => _lang;
            set
            {
                if (_lang == value) return;
                _lang = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            }
        }

        public string this[string key]
        {
            get
            {
                var dict = _lang == "en" ? En : Ru;
                return dict.TryGetValue(key, out var v) ? v : key;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // ── Russian strings ──────────────────────────────────────────────
        private static readonly Dictionary<string, string> Ru = new Dictionary<string, string>
        {
            ["app_title"]              = "MAGA Мессенджер",
            ["settings"]               = "Настройки",
            ["my_profile"]             = "МОЙ ПРОФИЛЬ",
            ["save_profile"]           = "💾 Сохранить профиль",
            ["auto_start"]             = "Автозапуск при входе в систему",
            ["my_computer"]            = "МОЙ КОМПЬЮТЕР",
            ["export_history"]         = "Экспорт истории",
            ["import_history"]         = "Импорт",
            ["notifications"]          = "Уведомления о сообщениях",
            ["stealth_mode"]           = "Невидимый режим",
            ["connect_by_ip"]          = "ПОДКЛЮЧИТЬ ПО IP",
            ["appearance"]             = "ОФОРМЛЕНИЕ",
            ["light_theme"]            = "Светлая тема",
            ["chat_wallpaper"]         = "🖼 Обои чата",
            ["clear_wallpaper"]        = "✕ Убрать обои",
            ["folders"]                = "ПАПКИ",
            ["create_folder"]          = "+ Создать папку",
            ["storage"]                = "ХРАНИЛИЩЕ",
            ["use_json_storage"]       = "Использовать JSON (не рекомендуется)",
            ["migrate_to_sqlite"]      = "Перенести в SQLite",
            ["migrate_to_json"]        = "Перенести в JSON",
            ["language"]               = "ЯЗЫК",
            ["lang_ru"]                = "Русский",
            ["lang_en"]                = "English",
            ["online"]                 = "онлайн",
            ["offline"]                = "не в сети",
            ["users_online"]           = "ПОЛЬЗОВАТЕЛИ В СЕТИ",
            ["searching"]              = "Поиск пользователей...",
            ["select_contact"]         = "Выберите собеседника",
            ["lan_hint"]               = "Пользователи в локальной сети появятся в списке слева",
            ["send"]                   = "Отправить",
            ["retry"]                  = "↺ Повторить",
            ["edit"]                   = "✎ Редактировать",
            ["delete"]                 = "🗑 Удалить",
            ["react"]                  = "😊 Реакция",
            ["cancel_edit"]            = "✕ Отмена",
            ["confirm_edit"]           = "✓ Сохранить",
            ["encryption_off"]         = "🔓 Без шифрования",
            ["encryption_on"]          = "🔒 Шифрование",
            ["request_encryption"]     = "Запросить шифрование",
            ["accept_encryption"]      = "Принять шифрование",
            ["encrypt_request_title"]  = "Запрос шифрования",
            ["encrypt_request_body"]   = "хочет включить шифрование для этого чата.\nВведите общий пароль:",
            ["encrypt_set_password"]   = "Введите пароль шифрования для чата с",
            ["encrypt_success"]        = "✅ Шифрование включено",
            ["encrypt_failed"]         = "❌ Неверный пароль шифрования",
            ["message_not_delivered"]  = "⚠️ Сообщение не доставлено",
            ["sending"]                = "Отправка…",
            ["sent"]                   = "Отправлено",
            ["folder_name_prompt"]     = "Введите название новой папки:",
            ["close"]                  = "✕",
            ["minimize"]               = "—",
            ["maximize"]               = "□",
            ["open"]                   = "Открыть",
            ["exit"]                   = "Выход",
            ["minimized_to_tray"]      = "Приложение свёрнуто в трей.",
            ["all_folders"]            = "Все",
            ["send_image"]             = "📷 Изображение",
            ["send_file"]              = "📎 Файл",
            ["send_video"]             = "🎬 Видео",
            ["send_voice"]             = "🎙 Голосовое",
            ["record_voice"]           = "🎙 Запись…",
            ["play_voice"]             = "▶ Воспроизвести",
            ["stop_voice"]             = "⏹ Стоп",
            ["save_file"]              = "Сохранить файл",
            ["bio_placeholder"]        = "О себе (до 1024 символов)",
            ["ip_placeholder"]         = "IP-адрес собеседника",
            ["search_placeholder"]     = "Поиск…",
            ["message_placeholder"]    = "Сообщение…",
            ["contact_wallpaper"]      = "🖼 Обои диалога",
            ["clear_contact_wallpaper"]= "✕",
            ["move_to_folder"]         = "В папку",
            ["delete_selected"]        = "Удалить выбранные",
            ["select_messages"]        = "Выбрать",
            ["cancel_selection"]       = "✕ Отмена выбора",
        };

        // ── English strings ──────────────────────────────────────────────
        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            ["app_title"]              = "MAGA Messenger",
            ["settings"]               = "Settings",
            ["my_profile"]             = "MY PROFILE",
            ["save_profile"]           = "💾 Save profile",
            ["auto_start"]             = "Launch on Windows startup",
            ["my_computer"]            = "MY COMPUTER",
            ["export_history"]         = "Export history",
            ["import_history"]         = "Import",
            ["notifications"]          = "Message notifications",
            ["stealth_mode"]           = "Stealth mode (hide from others)",
            ["connect_by_ip"]          = "CONNECT BY IP",
            ["appearance"]             = "APPEARANCE",
            ["light_theme"]            = "Light theme",
            ["chat_wallpaper"]         = "🖼 Chat wallpaper",
            ["clear_wallpaper"]        = "✕ Clear",
            ["folders"]                = "FOLDERS",
            ["create_folder"]          = "+ Create folder",
            ["storage"]                = "STORAGE",
            ["use_json_storage"]       = "Use JSON storage (not recommended)",
            ["migrate_to_sqlite"]      = "Migrate to SQLite",
            ["migrate_to_json"]        = "Migrate to JSON",
            ["language"]               = "LANGUAGE",
            ["lang_ru"]                = "Русский",
            ["lang_en"]                = "English",
            ["online"]                 = "online",
            ["offline"]                = "offline",
            ["users_online"]           = "USERS ONLINE",
            ["searching"]              = "Searching for users…",
            ["select_contact"]         = "Select a contact",
            ["lan_hint"]               = "Users on your local network will appear in the list",
            ["send"]                   = "Send",
            ["retry"]                  = "↺ Retry",
            ["edit"]                   = "✎ Edit",
            ["delete"]                 = "🗑 Delete",
            ["react"]                  = "😊 React",
            ["cancel_edit"]            = "✕ Cancel",
            ["confirm_edit"]           = "✓ Save",
            ["encryption_off"]         = "🔓 No encryption",
            ["encryption_on"]          = "🔒 Encrypted",
            ["request_encryption"]     = "Request encryption",
            ["accept_encryption"]      = "Accept encryption",
            ["encrypt_request_title"]  = "Encryption request",
            ["encrypt_request_body"]   = "wants to enable encryption for this chat.\nEnter a shared password:",
            ["encrypt_set_password"]   = "Enter encryption password for chat with",
            ["encrypt_success"]        = "✅ Encryption enabled",
            ["encrypt_failed"]         = "❌ Wrong encryption password",
            ["message_not_delivered"]  = "⚠️ Message not delivered",
            ["sending"]                = "Sending…",
            ["sent"]                   = "Sent",
            ["folder_name_prompt"]     = "Enter new folder name:",
            ["close"]                  = "✕",
            ["minimize"]               = "—",
            ["maximize"]               = "□",
            ["open"]                   = "Open",
            ["exit"]                   = "Exit",
            ["minimized_to_tray"]      = "App minimized to tray.",
            ["all_folders"]            = "All",
            ["send_image"]             = "📷 Image",
            ["send_file"]              = "📎 File",
            ["send_video"]             = "🎬 Video",
            ["send_voice"]             = "🎙 Voice",
            ["record_voice"]           = "🎙 Recording…",
            ["play_voice"]             = "▶ Play",
            ["stop_voice"]             = "⏹ Stop",
            ["save_file"]              = "Save file",
            ["bio_placeholder"]        = "About me (up to 1024 chars)",
            ["ip_placeholder"]         = "Contact's IP address",
            ["search_placeholder"]     = "Search…",
            ["message_placeholder"]    = "Message…",
            ["contact_wallpaper"]      = "🖼 Chat wallpaper",
            ["clear_contact_wallpaper"]= "✕",
            ["move_to_folder"]         = "Move to folder",
            ["delete_selected"]        = "Delete selected",
            ["select_messages"]        = "Select",
            ["cancel_selection"]       = "✕ Cancel selection",
        };
    }
}
