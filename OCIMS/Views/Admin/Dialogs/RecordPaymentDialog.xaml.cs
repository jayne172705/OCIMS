using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class RecordPaymentDialog : Window
    {
        private readonly RecordPaymentViewModel _vm;

        public RecordPaymentDialog(int premiumId)
        {
            InitializeComponent();
            _vm = new RecordPaymentViewModel(premiumId);
            _vm.CloseAction = () => Close();
            DataContext = _vm;
        }

        public void SetSaveCallback(System.Action onSave)
            => _vm.OnSaveSuccess = onSave;
    }
}
