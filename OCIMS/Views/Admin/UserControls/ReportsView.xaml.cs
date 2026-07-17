using System.Windows.Controls;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class ReportsView : UserControl
    {
        public ReportsView() : this(null)
        {
        }

        public ReportsView(int selectedReportTab) : this((int?)selectedReportTab)
        {
        }

        private ReportsView(int? selectedReportTab)
        {
            InitializeComponent();

            if (selectedReportTab.HasValue && DataContext is ReportsViewModel vm)
                vm.SelectedReportTab = selectedReportTab.Value;
        }
    }
}
