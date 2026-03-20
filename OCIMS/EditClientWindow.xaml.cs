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
            int spaceIndex = fullName.IndexOf(' ');
            if (spaceIndex > 0)
            {
                firstName = fullName.Substring(0, spaceIndex);
                lastName = fullName.Substring(spaceIndex + 1);
            }

            // Update client object
            _client.FirstName = firstName;
            _client.LastName = lastName;
            _client.Email = TxtEmail.Text.Trim();
            _client.PhoneMobile = TxtPhone.Text.Trim();
            _client.Address = TxtAddress.Text.Trim();

            bool saved = _repo.Update(_client);
            if (saved)
            {
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
