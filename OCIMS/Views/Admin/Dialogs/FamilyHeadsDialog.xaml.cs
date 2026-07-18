using System.Windows;
using eSureHi.Models;
using eSureHi.ViewModels.Admin.Dialogs;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class FamilyHeadsDialog : Window
    {
        private readonly FamilyHeadsViewModel _viewModel;

        public FamilyHeadsDialog(string barangay)
        {
            InitializeComponent();
            _viewModel = new FamilyHeadsViewModel(barangay)
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
    }
}
