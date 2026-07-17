using System;
using System.Windows.Controls;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class PremiumsView : UserControl
    {
        public PremiumsView() : this(null, null)
        {
        }

        public PremiumsView(string? statusFilter, DateTime? monthFilter)
        {
            InitializeComponent();

            if (DataContext is eSureHi.ViewModels.Admin.PremiumsViewModel vm)
            {
                if (!string.IsNullOrWhiteSpace(statusFilter))
                    vm.StatusFilter = statusFilter;
                if (monthFilter.HasValue)
                    vm.MonthFilter = monthFilter.Value;
            }
        }
    }
}
