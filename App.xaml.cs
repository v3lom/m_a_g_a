using System;
using System.Threading;
using System.Windows;
using M_A_G_A.Helpers;

namespace M_A_G_A
{
    public partial class App : Application
    {
        private static Mutex _mutex;
        private static bool  _ownsMutex;
        private static EventWaitHandle _bringToFrontEvent;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Single-instance enforcement
            _mutex = new Mutex(true, "MAGA_Messenger_SingleInstance", out _ownsMutex);
            if (!_ownsMutex)
            {
                // Another instance is running – signal it to come to front
                try
                {
                    using (var bringEvt = EventWaitHandle.OpenExisting("MAGA_Messenger_BringToFront"))
                        bringEvt.Set();
                }
                catch { }
                Shutdown();
                return;
            }

            // Register event so subsequent instances can activate this one
            _bringToFrontEvent = new EventWaitHandle(false, EventResetMode.AutoReset,
                "MAGA_Messenger_BringToFront");
            var thread = new Thread(() =>
            {
                while (_bringToFrontEvent.WaitOne())
                {
                    Dispatcher.Invoke(() =>
                    {
                        var win = Current?.MainWindow;
                        if (win != null)
                        {
                            win.ShowInTaskbar = true;
                            win.WindowState   = WindowState.Normal;
                            win.Activate();
                            win.Focus();
                        }
                    });
                }
            }) { IsBackground = true };
            thread.Start();

            base.OnStartup(e);

            // Apply saved theme before any window is created so StaticResource lookups
            // in styles pick up the correct brush values at first use.
            var settings = AppSettingsStore.Load();
            ThemeManager.Apply(settings.IsLightTheme);

            // Enable auto-start by default on very first launch
            if (!settings.AutoStartConfigured)
            {
                AutoStartHelper.SetEnabled(true);
                settings.AutoStartConfigured = true;
                AppSettingsStore.Save(settings);
            }

            var mainWin = new MainWindow();
            // Start minimized if launched with --minimized flag (daemon mode)
            if (AutoStartHelper.IsStartMinimized())
            {
                mainWin.ShowInTaskbar = false;
                mainWin.WindowState   = WindowState.Minimized;
            }
            mainWin.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _bringToFrontEvent?.Dispose();
            if (_ownsMutex)
            {
                try { _mutex?.ReleaseMutex(); } catch { }
            }
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
