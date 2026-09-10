using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class CreateUserAccountDialog : Window
    {
        private readonly CreateUserAccountViewModel _vm;

        public CreateUserAccountDialog(int empId, string empName)
        {
            InitializeComponent();
            _vm = new CreateUserAccountViewModel(empId, empName);
            _vm.CloseAction = () => Close();
            _vm.GetPassword = () => PasswordBox.Password;
            DataContext = _vm;
            PasswordBox.PasswordChanged += (s, e) => _vm.ErrorMessage = string.Empty;
        }

        public CreateUserAccountDialog(int beneficiaryId, string beneficiaryName, bool isBeneficiary)
        {
            InitializeComponent();
            _vm = new CreateUserAccountViewModel(beneficiaryId, beneficiaryName, isBeneficiary);
            _vm.CloseAction = () => Close();
            _vm.GetPassword = () => PasswordBox.Password;
            DataContext = _vm;
            PasswordBox.PasswordChanged += (s, e) => _vm.ErrorMessage = string.Empty;
        }

        public void SetSaveCallback(System.Action onSave)
            => _vm.OnSaveSuccess = onSave;
    }
}
