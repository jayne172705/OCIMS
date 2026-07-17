using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class TransactionQueueDialog : Window
    {
        private readonly TransactionQueueViewModel _vm;

        public TransactionQueueDialog()
        {
            InitializeComponent();
            _vm = new TransactionQueueViewModel();
            _vm.CloseAction = () => Close();
            DataContext = _vm;
        }
    }
}
