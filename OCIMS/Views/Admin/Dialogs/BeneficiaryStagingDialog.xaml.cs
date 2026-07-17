using System.Windows;
using System.Windows.Controls;
using eSureHi.Services;
using eSureHi.ViewModels.Admin;
using eSureHi.Views.Admin.UserControls;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class BeneficiaryStagingDialog : Window
    {
        private readonly BeneficiaryStagingViewModel _vm;
        private readonly bool _selectionOnly;
        public eSureHi.Models.BeneficiaryStaging? SelectedCrsRecord { get; private set; }

        public BeneficiaryStagingDialog(bool selectionOnly = false)
        {
            InitializeComponent();
            _selectionOnly = selectionOnly;
            _vm = new BeneficiaryStagingViewModel(selectionOnly);
            DataContext = _vm;
            Loaded += (_, _) =>
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
            };
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();

        private void ResultList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
                return;

            if (_selectionOnly && e.AddedItems[0] is eSureHi.Models.BeneficiaryStaging staging)
            {
                SelectedCrsRecord = staging;
                DialogResult = true;
                Close();
                return;
            }

            if (_selectionOnly)
                return;

            NavigationService.Instance.NavigateTo(new BeneficiariesView(_vm, openInitialSearch: false));
            Close();
        }
    }
}
