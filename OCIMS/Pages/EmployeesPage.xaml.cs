using System.Windows;
using System.Windows.Controls;

namespace OCIMS.Pages
{
    public partial class EmployeesPage : Page
    {
        public EmployeesPage()
        {
            InitializeComponent();

            // Bind sa shared EmployeeStore — auto mag-refresh
            EmployeeGrid.ItemsSource = EmployeeStore.Employees;
        }

        private void AddEmployee_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddEmployeeWindow();
            dialog.Owner = Window.GetWindow(this);
            dialog.ShowDialog();
            // Dili na kinahanglan manual refresh
            // Kay ObservableCollection — auto update ang DataGrid
        }

        private void ViewEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (EmployeeGrid.SelectedItem is OCIMS.Models.Employee emp)
            {
                MessageBox.Show(
                    $"Name: {emp.FullName}\n" +
                    $"Employee No: {emp.EmployeeNo}\n" +
                    $"Department: {emp.Department}\n" +
                    $"Position: {emp.Position}\n" +
                    $"Status: {emp.EmploymentStatus}\n" +
                    $"Email: {emp.Email}\n" +
                    $"Date Hired: {emp.DateHired:MMM dd, yyyy}",
                    "Employee Details",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Please select an employee first.",
                    "OCIMS", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void EditEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (EmployeeGrid.SelectedItem is OCIMS.Models.Employee emp)
            {
                MessageBox.Show($"Edit '{emp.FullName}' — Coming Soon!",
                    "OCIMS", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Please select an employee first.",
                    "OCIMS", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}