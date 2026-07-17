using System.Threading.Tasks;
using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class ClaimFormDialog : Window
    {
        private readonly ClaimFormViewModel _vm;

        public ClaimFormDialog()
        {
            InitializeComponent();
            _vm = new ClaimFormViewModel();
            _vm.CloseAction = () => Close();
            DataContext = _vm;
        }

        public ClaimFormDialog(int claimId) : this()
        {
            _ = _vm.InitEditAsync(claimId);
        }

        public ClaimFormDialog(int beneficiaryId, bool beneficiaryMode = true) : this()
        {
            _ = _vm.InitForBeneficiaryAsync(beneficiaryId);
        }

        public void SetSaveCallback(System.Action onSave)
            => _vm.OnSaveSuccess = onSave;
    }
}
