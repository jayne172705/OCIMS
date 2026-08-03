using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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
            if (_openedInitialSearch)
                return;

            _openedInitialSearch = true;

            // DialogHost registers its identifier during its own load pass, and this
            // handler runs on an ancestor, so showing straight away can race it.
            // Queueing at Loaded priority lets that pass finish first.
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                new Action(() => OpenSearchDialog(clearCurrentSelection: false)));
        }

        private void OpenSearchButton_Click(object sender, RoutedEventArgs e)
            => OpenSearchDialog(clearCurrentSelection: true);

        /// <summary>
        /// Shows the "Select Beneficiary" search panel as a modal. The dialog shares
        /// this page's ViewModel, so a pick inside it populates the detail panel out
        /// here; the dialog closes itself off that same selection.
        /// </summary>
        private async void OpenSearchDialog(bool clearCurrentSelection)
        {
            if (DataContext is not BeneficiaryStagingViewModel vm)
                return;

            if (clearCurrentSelection)
            {
                // Drop the current profile first — otherwise the dialog opens on an
                // already-satisfied selection and closes itself again immediately.
                vm.BackToSearchCommand.Execute(null);
            }
            else if (vm.HasSelection)
            {
                // Arrived pre-seeded (e.g. MemberDetailView's Update hand-off), so
                // there is nothing to search for.
                return;
            }

            var dialog = new SelectBeneficiaryDialog(vm);
            await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "BeneficiariesDialogHost");

            if (!vm.HasSelection)
            {
                vm.BackToDashboardCommand.Execute(null);
            }
        }

        private async void PrintIdCardBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is BeneficiaryStagingViewModel vm && vm.SelectedRecord != null)
            {
                var dialog = new PrintIdCardDialog(vm.SelectedRecord);
                await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "BeneficiariesDialogHost");
            }
        }
    }
}
