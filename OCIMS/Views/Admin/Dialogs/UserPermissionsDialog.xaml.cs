using System.Windows;
using eSureHi.Models;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class UserPermissionsDialog : Window
    {
        private readonly UserPermissionsViewModel _vm;

        public UserPermissionsDialog(SystemUser user)
        {
            InitializeComponent();
            _vm = new UserPermissionsViewModel(user);
            _vm.CloseAction = () => Close();
            DataContext = _vm;
        }

        public void SetSaveCallback(System.Action onSave)
            => _vm.OnSaveSuccess = onSave;
    }
}
