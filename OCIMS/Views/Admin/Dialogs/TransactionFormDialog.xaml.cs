using System.Windows;
using System.Windows.Controls;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class TransactionFormDialog : Window
    {
        private readonly TransactionFormViewModel _vm;

        public TransactionFormDialog()
        {
            InitializeComponent();
            _vm = new TransactionFormViewModel();
            _vm.CloseAction = () => Close();
            DataContext = _vm;
        }

        public TransactionFormDialog(int txId) : this()
        {
            _ = _vm.InitEditAsync(txId);
        }

        public void SetSaveCallback(System.Action onSave)
            => _vm.OnSaveSuccess = onSave;

    }
}
