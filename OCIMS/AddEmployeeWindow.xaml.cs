using System.Windows;
using OCIMS.Models;
using OCIMS.Data;

namespace OCIMS
{
    public partial class AddEmployeeWindow : Window
    {
        public bool IsSaved { get; private set; } = false;
        private EmployeeRepository _repo = new EmployeeRepository();

        public AddEmployeeWindow()
        {
            InitializeComponent();
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            // Validations
            if (string.IsNullOrWhiteSpace(TxtFullName.Text))
            { ShowError("Please enter the full name."); return; }

            if (string.IsNullOrWhiteSpace(TxtEmail.Text))
            { ShowError("Please enter the email address."); return; }

            if (string.IsNullOrWhiteSpace(TxtPhone.Text))
            { ShowError("Please enter the phone number."); return; }

            // Split full name
            string fullName = TxtFullName.Text.Trim();
            string firstName = fullName;
            string lastName = "";

            int spaceIndex = fullName.IndexOf(' ');
            if (spaceIndex > 0)
            {
                firstName = fullName.Substring(0, spaceIndex);
                lastName = fullName.Substring(spaceIndex + 1);
            }

            // Generate employee no
            string empNo = "CLT-" + System.DateTime.Now.ToString("yyMMddHHmmss");

            // Build client object
            var newClient = new Employee
            {
                EmployeeNo = empNo,
                FirstName = firstName,
                LastName = lastName,
                Email = TxtEmail.Text.Trim(),
                PhoneMobile = TxtPhone.Text.Trim(),
                Address = TxtAddress.Text.Trim(),
                EmploymentStatus = "Active",
                DateHired = System.DateTime.Today,
                DateOfBirth = new System.DateTime(1990, 1, 1),
                Department = "General",
                Position = "Client",
                Gender = "Other",
                EmploymentType = "Regular",
                PolicyCount = "0 Policies"
            };

            // Save to MySQL
            bool saved = _repo.Save(newClient);

            if (saved)
            {
                EmployeeStore.Employees.Add(newClient);
                IsSaved = true;

                MessageBox.Show(
                    "✔ Client '" + newClient.FullName + "' saved successfully!",
                    "OCIMS — Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                this.Close();
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ShowError(string message)
        {
            ErrorMsg.Text = "⚠ " + message;
            ErrorMsg.Visibility = Visibility.Visible;
        }
    }
}