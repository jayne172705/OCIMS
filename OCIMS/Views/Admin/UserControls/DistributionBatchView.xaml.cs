using System.Windows.Controls;
using eSureHi.Data;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class DistributionBatchView : UserControl
    {
        public DistributionBatchView()
        {
            InitializeComponent();
            DataContext = new DistributionBatchViewModel(eSureHiDbContextFactory.Create());
        }
    }
}
