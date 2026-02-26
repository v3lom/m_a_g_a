using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace M_A_G_A.Views
{
    public partial class MessengerView : UserControl
    {
        public MessengerView() { InitializeComponent(); }

        private void MessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift))
            {
                var vm = DataContext as M_A_G_A.ViewModels.AppViewModel;
                if (vm?.SendTextCommand.CanExecute(null) == true)
                    vm.SendTextCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void MsgScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.ExtentHeightChange > 0)
                MsgScroll.ScrollToEnd();
        }
    }
}
