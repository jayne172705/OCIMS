using System.Windows.Controls;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class ClaimSearchDialog : UserControl
    {
        public ClaimSearchDialog()
        {
            InitializeComponent();
        }

        public ClaimSearchDialog(ClaimsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
