using System;
using System.Reflection;
using Microsoft.Win32;

namespace M_A_G_A.Helpers
{
    /// <summary>
    /// Manages Windows registry auto-start entry so the app runs on login (daemon mode).
    /// </summary>
    public static class AutoStartHelper
    {
        private const string AppName = "MAGA_Messenger";
        private const string RunKey  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        public static bool IsEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                    return key?.GetValue(AppName) != null;
            }
            catch { return false; }
        }

        public static void SetEnabled(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return;
                    if (enable)
                    {
                        var exePath = Assembly.GetExecutingAssembly().Location;
                        key.SetValue(AppName, $"\"{exePath}\" --minimized");
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            catch { }
        }

        /// <summary>Returns true if the process was launched with --minimized flag.</summary>
        public static bool IsStartMinimized()
        {
            var args = Environment.GetCommandLineArgs();
            foreach (var a in args)
                if (a.Equals("--minimized", StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
