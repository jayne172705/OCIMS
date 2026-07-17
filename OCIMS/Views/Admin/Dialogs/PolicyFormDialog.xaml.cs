using System.Threading.Tasks;
using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class PolicyFormDialog : Window
    {
        private readonly PolicyFormViewModel _vm;

        public PolicyFormDialog()
        {
            InitializeComponent();
            _vm = new PolicyFormViewModel();
            _vm.CloseAction = () => Close();
            _vm.InitNew();
            DataContext = _vm;
        }

        public PolicyFormDialog(int policyId) : this()
        {
            _ = _vm.InitEditAsync(policyId);
        }

        public void SetSaveCallback(System.Action onSave)
            => _vm.OnSaveSuccess = onSave;
    }
}
