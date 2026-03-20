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
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtClaimNo.Text))
            { ShowError("Please enter a claim number."); return; }

            if (CmbType.SelectedItem == null)
            { ShowError("Please select a claim type."); return; }

            if (string.IsNullOrWhiteSpace(TxtClientId.Text))
            { ShowError("Please enter the client ID."); return; }

            if (string.IsNullOrWhiteSpace(TxtAmount.Text))
            { ShowError("Please enter the amount claimed."); return; }

            decimal amount;
            if (!decimal.TryParse(TxtAmount.Text.Replace(",", ""), out amount))
            { ShowError("Amount must be a valid number."); return; }

            if (_claimRepo.ClaimNoExists(TxtClaimNo.Text.Trim()))
            { ShowError("Claim No. already exists."); return; }

            // Get emp_id from client ID
            int empId = GetEmpId(TxtClientId.Text.Trim());
            if (empId == 0)
            { ShowError("Client ID '" + TxtClientId.Text.Trim() + "' not found."); return; }

            var claim = new Claim
            {
                ClaimNo = TxtClaimNo.Text.Trim(),
                EmpId = empId,
                ClaimType = (CmbType.SelectedItem as ComboBoxItem)?.Content?.ToString(),
                Amount = amount,
                ClaimStatus = "Pending",
                Description = TxtDescription.Text.Trim()
            };

            if (_claimRepo.Save(claim))
            {
                IsSaved = true;
                MessageBox.Show("✔ Claim '" + claim.ClaimNo + "' filed successfully!",
                    "OCIMS — Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
        }

        private int GetEmpId(string empNo)
        {
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
            catch { }
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
