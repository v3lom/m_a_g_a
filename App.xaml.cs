using System.Windows;
using M_A_G_A.Helpers;

namespace M_A_G_A
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Apply saved theme before any window is created so StaticResource lookups
            // in styles pick up the correct brush values at first use.
            var isLight = AppSettingsStore.Load().IsLightTheme;
            ThemeManager.Apply(isLight);

            var win = new MainWindow();
            // Start minimized if launched with --minimized flag (daemon mode)
            if (AutoStartHelper.IsStartMinimized())
            {
                win.ShowInTaskbar = false;
                win.WindowState   = System.Windows.WindowState.Minimized;
            }
            win.Show();
        }
    }
}
