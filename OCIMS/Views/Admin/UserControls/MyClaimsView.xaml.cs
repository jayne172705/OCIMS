using System.Windows.Controls;
using System.Windows.Input;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class MyClaimsView : UserControl
    {
        public MyClaimsView() => InitializeComponent();

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MyClaimsViewModel vm && vm.SelectedClaim is not null)
                vm.ViewCommand.Execute(null);
        }
    }
}
