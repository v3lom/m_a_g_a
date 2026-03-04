using System.Windows;
using System.Windows.Input;

namespace M_A_G_A.Views
{
    public partial class PasswordDialog : Window
    {
        public string Password { get; private set; }

        public PasswordDialog(string prompt = "Введите пароль:")
        {
            InitializeComponent();
            PromptText.Text = prompt;
        }

        private void OkClick(object sender, RoutedEventArgs e)
        {
            Password   = PwdBox.Password;
            DialogResult = true;
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void PwdBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) OkClick(null, null);
            if (e.Key == Key.Escape) CancelClick(null, null);
        }
    }
}
