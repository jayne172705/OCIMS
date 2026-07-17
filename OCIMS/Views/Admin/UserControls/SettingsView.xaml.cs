using System.Windows;
using System.Windows.Controls;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class SettingsView : UserControl
    {
        public SettingsView() => InitializeComponent();

        private void ManageDepartments_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Views.Admin.Dialogs.ManageDepartmentsDialog();
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        private void CrsImport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Views.Admin.Dialogs.BeneficiaryStagingDialog();
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }
    }
}
