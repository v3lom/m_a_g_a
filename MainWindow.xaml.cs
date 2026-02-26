using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using M_A_G_A.ViewModels;

namespace M_A_G_A
{
    public partial class MainWindow : Window
    {
        private readonly AppViewModel _vm;
        private NotifyIcon _trayIcon;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new AppViewModel();
            DataContext = _vm;
            InitTrayIcon();
        }

        // ─── Tray icon ───────────────────────────────────────────────
        private void InitTrayIcon()
        {
            _trayIcon = new NotifyIcon
            {
                Icon    = SystemIcons.Application,
                Text    = "MAGA Messenger",
                Visible = true
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Открыть", null, (s, e) => ShowWindow());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Выход",   null, (s, e) => ExitApp());
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (s, e) => ShowWindow();

            // Minimize to tray on close button
            StateChanged += (s, e) =>
            {
                if (WindowState == WindowState.Minimized)
                {
                    ShowInTaskbar = false;
                    _trayIcon.ShowBalloonTip(800, "MAGA", "Приложение свёрнуто в трей.", ToolTipIcon.Info);
                }
            };
        }

        private void ShowWindow()
        {
            ShowInTaskbar  = true;
            WindowState    = WindowState.Normal;
            Activate();
            Focus();
        }

        private void ExitApp()
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _vm.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        // ─── Title bar drag/controls ─────────────────────────────────
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) MaximizeClick(null, null);
            else DragMove();
        }

        private void MinimizeClick(object sender, RoutedEventArgs e)
        {
            WindowState   = WindowState.Minimized;
            ShowInTaskbar = false;
        }

        private void MaximizeClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseClick(object sender, RoutedEventArgs e)
        {
            // Minimize to tray instead of closing
            WindowState   = WindowState.Minimized;
            ShowInTaskbar = false;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Only truly close when ExitApp() is called
            e.Cancel = true;
            WindowState   = WindowState.Minimized;
            ShowInTaskbar = false;
        }

        protected override void OnClosed(EventArgs e)
        {
            _trayIcon?.Dispose();
            _vm.Dispose();
            base.OnClosed(e);
        }
    }
}

