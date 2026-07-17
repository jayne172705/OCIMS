using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class UseBenefitDialog : Window
    {
        private readonly UseBenefitViewModel _vm;

        public UseBenefitDialog(int benefitId)
        {
            InitializeComponent();
            _vm = new UseBenefitViewModel(benefitId);
            _vm.CloseAction = () => Close();
            DataContext = _vm;
        }

        public void SetSaveCallback(System.Action onSave)
            => _vm.OnSaveSuccess = onSave;
    }
}
