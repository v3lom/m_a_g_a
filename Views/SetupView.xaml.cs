using System.Windows.Controls;
using System.Windows.Input;

namespace M_A_G_A.Views
{
    public partial class SetupView : UserControl
    {
        public SetupView() { InitializeComponent(); }

        private void NameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var vm = DataContext as M_A_G_A.ViewModels.AppViewModel;
                if (vm?.StartAppCommand.CanExecute(null) == true)
                    vm.StartAppCommand.Execute(null);
            }
        }
    }
}
