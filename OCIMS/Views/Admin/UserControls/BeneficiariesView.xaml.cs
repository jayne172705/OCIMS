using System.Windows;
using System.Windows.Controls;
using eSureHi.Models;
using eSureHi.ViewModels.Admin;
using eSureHi.Views.Admin.Dialogs;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class BeneficiariesView : UserControl
    {
        private bool _openedInitialSearch;
        private readonly bool _openInitialSearch;

        public BeneficiariesView()
            : this(null, false)
        {
        }

        public BeneficiariesView(BeneficiaryStagingViewModel? viewModel, bool openInitialSearch)
        {
            InitializeComponent();
            DataContext = viewModel ?? new BeneficiaryStagingViewModel();

            _openInitialSearch = openInitialSearch;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_openInitialSearch)
                return;

            if (_openedInitialSearch)
                return;

            _openedInitialSearch = true;
            OpenSearchDialog(clearCurrentSelection: false);
        }

        private void OpenSearchButton_Click(object sender, RoutedEventArgs e)
            => OpenSearchDialog(clearCurrentSelection: true);

        private async void OpenSearchDialog(bool clearCurrentSelection)
        {
            if (DataContext is not BeneficiaryStagingViewModel vm)
                return;

            if (clearCurrentSelection)
            {
                vm.BackToSearchCommand.Execute(null);
                return;
            }

            var queueVm = new BeneficiaryQueueViewModel();
            var dialog = new BeneficiaryQueueDialog(queueVm);
            var owner = Window.GetWindow(this);
            if (owner is not null)
                dialog.Owner = owner;

            BeneficiaryQueueItem? selectedItem = null;
            queueVm.OnItemSelected = item => selectedItem = item;

            dialog.ShowDialog();

            if (selectedItem != null)
            {
                if (selectedItem.OriginalSource is BeneficiaryStaging staging)
                {
                    vm.SelectedRecord = staging;
                    await vm.AddSelectedRecordToInsuranceAsync();
                }
                else if (selectedItem.OriginalSource is Beneficiary beneficiary)
                {
                    vm.SelectedSystemBeneficiary = beneficiary;
                }
            }
        }
    }
}
