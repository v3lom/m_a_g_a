using System.Windows;
using System.Windows.Input;
using M_A_G_A.ViewModels;

namespace M_A_G_A
{
    public partial class MainWindow : Window
    {
        private readonly AppViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new AppViewModel();
            DataContext = _vm;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) MaximizeClick(null, null);
            else DragMove();
        }

        private void MinimizeClick(object sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        private void MaximizeClick(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }

        private void CloseClick(object sender, RoutedEventArgs e)
        {
            _vm.Dispose();
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _vm.Dispose();
            base.OnClosing(e);
        }
    }
}

