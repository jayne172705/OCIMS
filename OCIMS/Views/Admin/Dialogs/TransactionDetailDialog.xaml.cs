using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class TransactionDetailDialog : Window
    {
        private readonly TransactionDetailViewModel _vm;

        public TransactionDetailDialog(int txId)
        {
            InitializeComponent();
            _vm = new TransactionDetailViewModel();
            _vm.CloseAction = () => Close();
            DataContext = _vm;
            _ = _vm.LoadAsync(txId);
        }

        public void SetStatusChangedCallback(System.Action onChanged)
            => _vm.OnStatusChanged = onChanged;
    }
}
