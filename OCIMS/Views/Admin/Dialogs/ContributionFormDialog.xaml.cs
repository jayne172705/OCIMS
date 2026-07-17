using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class ContributionFormDialog : Window
    {
        private readonly ContributionFormViewModel _vm;

        public ContributionFormDialog(int empId)
        {
            InitializeComponent();
            _vm = new ContributionFormViewModel(empId);
            _vm.CloseAction = () => { DialogResult = false; Close(); };
            _vm.OnSaveSuccess = () => { DialogResult = true; Close(); };
            DataContext = _vm;
        }

        public ContributionFormDialog(int empId, int contributionId)
        {
            InitializeComponent();
            _vm = new ContributionFormViewModel(empId, contributionId);
            _vm.CloseAction = () => { DialogResult = false; Close(); };
            _vm.OnSaveSuccess = () => { DialogResult = true; Close(); };
            DataContext = _vm;
        }
    }
}
