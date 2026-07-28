using System.Windows;
using System.Windows.Controls;
using eSureHi.Data;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class DistributionBatchView : UserControl
    {
        public DistributionBatchView(DistributionBoardMode? mode = null)
        {
            InitializeComponent();
            DataContext = new DistributionBatchViewModel(eSureHiDbContextFactory.Create(), mode);
        }

        private void DotsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }
    }
}
