using System.Windows.Controls;
using System.Windows.Input;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class PoliciesView : UserControl
    {
        public PoliciesView() => InitializeComponent();

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is PoliciesViewModel vm)
                vm.EditCommand.Execute(null);
        }
    }
}
