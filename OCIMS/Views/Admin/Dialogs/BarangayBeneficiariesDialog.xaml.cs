using System.Windows;
using eSureHi.Models;
using eSureHi.ViewModels.Admin.Dialogs;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class BarangayBeneficiariesDialog : Window
    {
        private readonly BarangayBeneficiariesViewModel _viewModel;

        public BarangayBeneficiariesDialog(string barangay)
        {
            InitializeComponent();
            _viewModel = new BarangayBeneficiariesViewModel(barangay)
            {
                OpenProfileRequested = OpenProfile
            };
            DataContext = _viewModel;

            Loaded += async (s, e) => await _viewModel.LoadAsync();
        }

        private void OpenProfile(Employee selectedHead)
        {
            var profileDialog = new FamilyHeadProfileDialog(selectedHead);
            profileDialog.Owner = this;
            profileDialog.ShowDialog();
        }

        private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && _viewModel != null)
            {
                _viewModel.SearchQuery = textBox.Text;
            }
        }
    }
}
