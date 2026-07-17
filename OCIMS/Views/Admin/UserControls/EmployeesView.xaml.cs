using System.Windows.Controls;
using System.Windows.Input;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class EmployeesView : UserControl
    {
        public EmployeesView() => InitializeComponent();

        // Double-click row to edit
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is EmployeesViewModel vm)
                vm.EditCommand.Execute(null);
        }
    }
}
