using System.Windows;
using M_A_G_A.Helpers;

namespace M_A_G_A
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
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
