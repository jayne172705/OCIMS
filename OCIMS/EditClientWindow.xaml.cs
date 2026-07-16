using System.Windows;
using OCIMS.Data;
using OCIMS.Models;

namespace OCIMS
{
    public partial class EditClientWindow : Window
    {
        public bool IsSaved { get; private set; } = false;
        private EmployeeRepository _repo = new EmployeeRepository();
        private Employee _client;

        public EditClientWindow(Employee client)
        {
            InitializeComponent();
            _client = client;

            // Pre-fill fields
            TxtFullName.Text = client.FullName;
            TxtEmail.Text = client.Email;
            TxtPhone.Text = client.PhoneMobile;
            TxtAddress.Text = client.Address;
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtFullName.Text))
            { ShowError("Please enter the full name."); return; }

            if (string.IsNullOrWhiteSpace(TxtEmail.Text))
            { ShowError("Please enter the email address."); return; }

            if (string.IsNullOrWhiteSpace(TxtPhone.Text))
            { ShowError("Please enter the phone number."); return; }

            // Split name
            string fullName = TxtFullName.Text.Trim();
            string firstName = fullName;
            string lastName = "";
            int spaceIndex = fullName.LastIndexOf(' ');
            if (spaceIndex > 0)
            {
                firstName = fullName.Substring(0, spaceIndex).Trim();
                lastName = fullName.Substring(spaceIndex + 1);
            }

            var updated = new Employee
            {
                EmployeeNo = _client.EmployeeNo,
                FirstName = firstName,
                LastName = lastName,
                Email = TxtEmail.Text.Trim(),
                PhoneMobile = TxtPhone.Text.Trim(),
                Address = TxtAddress.Text.Trim()
            };

            bool saved = _repo.Update(updated);
            if (saved)
            {
                _client.FirstName = updated.FirstName;
                _client.LastName = updated.LastName;
                _client.Email = updated.Email;
                _client.PhoneMobile = updated.PhoneMobile;
                _client.Address = updated.Address;
                IsSaved = true;
                MessageBox.Show(
                    "✔ Client '" + _client.FullName + "' updated successfully!",
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
