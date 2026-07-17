using System.Windows.Controls;
using System.Windows.Input;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class ClaimsView : UserControl
    {
        public ClaimsView() : this(ClaimsListMode.All)
        {
        }

        public ClaimsView(ClaimsListMode listMode)
        {
            InitializeComponent();
            DataContext = new ClaimsViewModel(listMode);
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ClaimsViewModel vm && vm.SelectedClaim is not null)
                vm.ViewCommand.Execute(null);
        }
    }
}
