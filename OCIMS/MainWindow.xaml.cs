using System.Windows;
using OCIMS.Pages;

namespace OCIMS
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            if (LoginWindow.CurrentUser != null)
            {
                TxtUserName.Text = LoginWindow.CurrentUser.FullName;
                TxtUserRole.Text = LoginWindow.CurrentUser.Role;
                TxtUserInitials.Text = GetInitials(LoginWindow.CurrentUser.FullName);
            }
            ContentFrame.Navigate(new DashboardPage());
        }

        private string GetInitials(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "AD";
            var parts = fullName.Split(' ');
            if (parts.Length >= 2)
                return ("" + parts[0][0] + parts[1][0]).ToUpper();
            return fullName.Length >= 2
                ? fullName.Substring(0, 2).ToUpper()
                : fullName.ToUpper();
        }

        private void NavDashboard_Click(object sender, RoutedEventArgs e)
        {
            PageTitle.Text = "Dashboard";
            ActionButton.Content = "+ New Client";
            ContentFrame.Navigate(new DashboardPage());
        }

        private void NavClients_Click(object sender, RoutedEventArgs e)
        {
            PageTitle.Text = "Clients";
            ActionButton.Content = "+ New Client";
            ContentFrame.Navigate(new ClientsPage());
        }

        private void NavPolicies_Click(object sender, RoutedEventArgs e)
        {
            PageTitle.Text = "Policies";
            ActionButton.Content = "+ New Policy";
            ContentFrame.Navigate(new PoliciesPage());
        }

        private void NavClaims_Click(object sender, RoutedEventArgs e)
        {
            PageTitle.Text = "Claims";
            ActionButton.Content = "+ File Claim";
            ContentFrame.Navigate(new ClaimsPage());
        }

        private void NavPayments_Click(object sender, RoutedEventArgs e)
        {
            PageTitle.Text = "Payments";
            ActionButton.Content = "+ New Payment";
            ContentFrame.Navigate(new PaymentsPage());
        }

        private void NavDocuments_Click(object sender, RoutedEventArgs e)
        {
            PageTitle.Text = "Documents";
            ActionButton.Content = "+ Add Document";
            ContentFrame.Navigate(new DocumentsPage());
        }

        private void NavTransactions_Click(object sender, RoutedEventArgs e)
        {
            PageTitle.Text = "Transactions";
            ActionButton.Content = "+ New Transaction";
            ContentFrame.Navigate(new TransactionsPage());
        }

        private void NavResources_Click(object sender, RoutedEventArgs e)
        {
            PageTitle.Text = "Resources";
            ActionButton.Content = "+ Add Fund Source";
            ContentFrame.Navigate(new ResourcesPage());
        }

        private void NavReports_Click(object sender, RoutedEventArgs e)
        {
            PageTitle.Text = "Reports";
            ActionButton.Content = "Export";
            ContentFrame.Navigate(new ReportsPage());
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
        }
    }
}
