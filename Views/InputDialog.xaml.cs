using System.Windows;
using System.Windows.Input;

namespace M_A_G_A.Views
{
    /// <summary>
    /// General-purpose text input dialog (replaces PasswordDialog for non-password use cases).
    /// </summary>
    public partial class InputDialog : Window
    {
        public string InputText { get; private set; }

        public InputDialog(string prompt = "Введите значение:", string defaultValue = "")
        {
            InitializeComponent();
            PromptText.Text = prompt;
            InputBox.Text   = defaultValue;
            InputBox.SelectAll();
        }

        protected override void OnSourceInitialized(System.EventArgs e)
        {
            base.OnSourceInitialized(e);
            InputBox.Focus();
        }

        private void OkClick(object sender, RoutedEventArgs e)
        {
            InputText    = InputBox.Text;
            DialogResult = true;
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)  OkClick(null, null);
            if (e.Key == Key.Escape) CancelClick(null, null);
        }
    }
}
