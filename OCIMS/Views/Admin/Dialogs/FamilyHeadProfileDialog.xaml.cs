using System.Windows;
using eSureHi.Models;
using eSureHi.ViewModels.Admin.Dialogs;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class FamilyHeadProfileDialog : Window
    {
        private readonly FamilyHeadProfileViewModel _viewModel;

        public FamilyHeadProfileDialog(Employee head)
        {
            InitializeComponent();
            _viewModel = new FamilyHeadProfileViewModel(head)
            {
                CloseRequested = Close
            };
            DataContext = _viewModel;

            Loaded += async (s, e) => await _viewModel.LoadAsync();
        }
    }
}
