using System.Windows;
using System.Windows.Controls;
using eSureHi.Services;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class ManageMembersView : UserControl
    {
        public ManageMembersView()
        {
            InitializeComponent();
            Loaded += ManageMembersView_Loaded;
        }

        private void ManageMembersView_Loaded(object sender, RoutedEventArgs e)
        {
            if (!PermissionService.CanApproveWorkflow)
            {
                foreach (var column in MembersDataGrid.Columns)
                {
                    if (column.Header?.ToString() == "Update Section")
                    {
                        column.Visibility = Visibility.Collapsed;
                        break;
                    }
                }
            }
        }
    }
}
