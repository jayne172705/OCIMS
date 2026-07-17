using System.Windows.Controls;
using System.Windows.Input;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class DocumentsView : UserControl
    {
        public DocumentsView() => InitializeComponent();

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is DocumentsViewModel vm && vm.SelectedDocument is not null)
                vm.EditCommand.Execute(null);
        }
    }
}
