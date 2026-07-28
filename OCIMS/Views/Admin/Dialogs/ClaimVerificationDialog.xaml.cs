using System;
using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class ClaimVerificationDialog : Window
    {
        private readonly ClaimFormViewModel _vm;

        public ClaimVerificationDialog(int beneficiaryId)
        {
            InitializeComponent();
            _vm = new ClaimFormViewModel();
            _vm.CloseAction = () => Close();
            DataContext = _vm;
            _ = _vm.InitForBeneficiaryAsync(beneficiaryId);
        }

        public void SetSaveCallback(Action onSave)
            => _vm.OnSaveSuccess = onSave;
    }
}
