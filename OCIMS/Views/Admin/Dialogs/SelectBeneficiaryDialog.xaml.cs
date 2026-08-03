using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using eSureHi.ViewModels.Admin;
using MaterialDesignThemes.Wpf;

namespace eSureHi.Views.Admin.Dialogs
{
    /// <summary>
    /// The "Select Beneficiary" search panel as a modal, shown through
    /// BeneficiariesDialogHost. Hosts the page's own BeneficiaryStagingViewModel,
    /// so a pick here is a pick on the page.
    /// </summary>
    public partial class SelectBeneficiaryDialog : UserControl
    {
        private INotifyPropertyChanged? _watched;

        public SelectBeneficiaryDialog(BeneficiaryStagingViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.SelectionOnly = true;

            // Watching the ViewModel rather than the ListBoxes covers both entry
            // points with one hook: a row click and a suggestion tap both land on
            // the same two selection properties.
            _watched = viewModel;
            _watched.PropertyChanged += OnViewModelPropertyChanged;

            Loaded += (_, _) => BeneficiarySearchBox.Focus();
            Unloaded += (_, _) => Detach();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is not (nameof(BeneficiaryStagingViewModel.SelectedRecord)
                                    or nameof(BeneficiaryStagingViewModel.SelectedSystemBeneficiary)))
                return;

            if (DataContext is not BeneficiaryStagingViewModel vm)
                return;

            // CloseProfile() nulls both properties to send the user back to search,
            // so only a non-null value means an actual pick.
            if (vm.SelectedRecord is null && vm.SelectedSystemBeneficiary is null)
                return;

            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Close()
        {
            // Selecting a system beneficiary synthesises a SelectedRecord inside the
            // setter, which re-enters this handler; tearing the subscription down
            // first keeps the close to a single shot.
            Detach();

            if (DataContext is BeneficiaryStagingViewModel vm)
                vm.IsSuggestionsOpen = false;

            // Deferred because the trigger is usually a ListBox selection change:
            // ripping the list out of the visual tree mid-notification is asking for
            // trouble, so let the current change settle first.
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                if (DialogHost.CloseDialogCommand.CanExecute(null, this))
                    DialogHost.CloseDialogCommand.Execute(null, this);
            }));
        }

        private void Detach()
        {
            if (_watched is null)
                return;

            _watched.PropertyChanged -= OnViewModelPropertyChanged;

            if (DataContext is BeneficiaryStagingViewModel vm)
            {
                vm.SelectionOnly = false;
            }

            _watched = null;
        }
    }
}
