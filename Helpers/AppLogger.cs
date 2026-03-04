using System;
using System.IO;
using System.Text;

namespace M_A_G_A.Helpers
{
    /// <summary>
    /// Application-level logger. Writes events to AppData/MAGA/logs/app-YYYY-MM-DD.log.
    /// Message content is NEVER logged for security.
    /// </summary>
    public static class AppLogger
    {
        private static readonly string LogDir =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MAGA", "logs");

        // Refreshed lazily so midnight rolls the log file naturally
        private static string LogPath =>
            Path.Combine(LogDir, $"app-{DateTime.Now:yyyy-MM-dd}.log");

        private static readonly object _lock = new object();

        static AppLogger()
        {
            try { Directory.CreateDirectory(LogDir); } catch { }
        }

        public static void Info(string message)  => Write("INFO ", message);
        public static void Warn(string message)  => Write("WARN ", message);
        public static void Error(string message) => Write("ERROR", message);
        public static void Error(string message, Exception ex) =>
            Write("ERROR", $"{message}: {ex.GetType().Name} - {ex.Message}");

        private static void Write(string level, string message)
        {
            try
            {
                var line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
                lock (_lock)
                    File.AppendAllText(LogPath, line, Encoding.UTF8);
            }
            catch { /* never throw from logger */ }
        }
    }
}
