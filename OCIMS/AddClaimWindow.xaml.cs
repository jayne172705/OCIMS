using System;
using System.Windows;
using System.Windows.Controls;
using OCIMS.Data;
using OCIMS.Models;

namespace OCIMS
{
    public partial class AddClaimWindow : Window
    {
        public bool IsSaved { get; private set; } = false;
        private ClaimRepository _claimRepo = new ClaimRepository();
        private EmployeeRepository _empRepo = new EmployeeRepository();

        public AddClaimWindow()
        {
            InitializeComponent();
            // Auto-generate claim no
            TxtClaimNo.Text = "CLM-" + DateTime.Now.ToString("yyMMddHHmmss");
            CmbPolicy.ItemsSource = new PolicyRepository().GetAll();
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtClaimNo.Text))
            { ShowError("Please enter a claim number."); return; }

            if (CmbType.SelectedItem == null)
            { ShowError("Please select a claim type."); return; }

            if (string.IsNullOrWhiteSpace(TxtClientId.Text))
            { ShowError("Please enter the client ID."); return; }

            if (CmbPolicy.SelectedValue == null)
            { ShowError("Please select a policy."); return; }

            if (string.IsNullOrWhiteSpace(TxtAmount.Text))
            { ShowError("Please enter the amount claimed."); return; }

            decimal amount;
            if (!AppFormats.TryParseAmount(TxtAmount.Text, out amount))
            { ShowError("Amount must be a valid positive number (e.g. 5000 or 5,000.00)."); return; }

            string claimNo = TxtClaimNo.Text.Trim();
            if (_claimRepo.ClaimNoExists(claimNo))
            {
                claimNo = "CLM-" + DateTime.Now.ToString("yyMMddHHmmss") + "-" + new Random().Next(100, 1000);
                if (_claimRepo.ClaimNoExists(claimNo))
                { ShowError("Claim No. already exists. Please change it and try again."); return; }
                TxtClaimNo.Text = claimNo;
            }

            // Get emp_id from client ID
            string lookupError;
            int empId = GetEmpId(TxtClientId.Text.Trim(), out lookupError);
            if (lookupError != null)
            { ShowError("Cannot reach the database: " + lookupError); return; }
            if (empId == 0)
            { ShowError("Client ID '" + TxtClientId.Text.Trim() + "' not found."); return; }

            var claim = new Claim
            {
                ClaimNo = claimNo,
                EmpId = empId,
                PolicyId = (int)CmbPolicy.SelectedValue,
                ClaimType = (CmbType.SelectedItem as ComboBoxItem)?.Content?.ToString(),
                ClaimDate = AppFormats.ToDisplayDate(DateTime.Today),
                Amount = amount,
                ClaimStatus = "Pending",
                Description = TxtDescription.Text.Trim()
            };

            if (_claimRepo.Save(claim))
            {
                IsSaved = true;
                MessageBox.Show("✔ Claim '" + claim.ClaimNo + "' filed successfully!",
                    "eSureHi — Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
        }

        private int GetEmpId(string empNo, out string error)
        {
            error = null;
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT emp_id FROM employees WHERE employee_no=@no LIMIT 1";
                    using (var cmd = new MySql.Data.MySqlClient.MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@no", empNo);
                        var result = cmd.ExecuteScalar();
                        if (result != null) return Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex) { error = ex.Message; }
            return 0;
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ShowError(string msg)
        {
            ErrorMsg.Text = "⚠ " + msg;
            ErrorMsg.Visibility = Visibility.Visible;
        }
    }
}
