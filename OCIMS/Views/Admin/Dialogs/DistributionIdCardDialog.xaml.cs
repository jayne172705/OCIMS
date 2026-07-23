using System.Windows.Controls;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class DistributionIdCardDialog : UserControl
    {
        public DistributionIdCardDialog(DistributionIdCardViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
