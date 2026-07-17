using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class AssignPolicyDialog : Window
    {
        private readonly AssignPolicyViewModel _vm;

        public AssignPolicyDialog(int policyId, string policyName)
        {
            InitializeComponent();
            _vm = new AssignPolicyViewModel(policyId, policyName);
            _vm.CloseAction = () => Close();
            DataContext = _vm;
        }

        public void SetSaveCallback(System.Action onSave)
            => _vm.OnSaveSuccess = onSave;
    }
}
