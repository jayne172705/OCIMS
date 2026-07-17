using System.Windows.Controls;
using System.Windows.Input;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class TransactionsView : UserControl
    {
        public TransactionsView() => InitializeComponent();

        public TransactionsView(TransactionsViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is TransactionsViewModel vm && vm.SelectedTransaction is not null)
                vm.ViewCommand.Execute(null);
        }
    }
}
