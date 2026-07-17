using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class ClaimDetailDialog : Window
    {
        private readonly ClaimDetailViewModel _vm;

        public ClaimDetailDialog(int claimId, bool isReadOnly = false)
        {
            InitializeComponent();
            _vm = new ClaimDetailViewModel();
            _vm.CloseAction = () => Close();
            _vm.IsReadOnly = isReadOnly;
            DataContext = _vm;
            _ = _vm.LoadAsync(claimId);
        }

        public void SetStatusChangedCallback(System.Action onChanged)
            => _vm.OnStatusChanged = onChanged;
    }
}
