using System;
using System.Windows;
using System.Windows.Controls;
using OCIMS.Data;
using OCIMS.Models;

namespace OCIMS
{
    public partial class AddPolicyWindow : Window
    {
        public bool IsSaved { get; private set; } = false;
        private PolicyRepository _repo = new PolicyRepository();

        public AddPolicyWindow()
        {
            InitializeComponent();
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtPolicyNo.Text))
            { ShowError("Please enter the policy number."); return; }

            if (string.IsNullOrWhiteSpace(TxtPolicyName.Text))
            { ShowError("Please enter the policy name."); return; }

            if (CmbType.SelectedItem == null)
            { ShowError("Please select a policy type."); return; }

            if (string.IsNullOrWhiteSpace(TxtCoverage.Text))
            { ShowError("Please enter the coverage amount."); return; }

            decimal coverage;
            if (!AppFormats.TryParseAmount(TxtCoverage.Text, out coverage))
            { ShowError("Coverage amount must be a valid positive number (e.g. 250000 or 250,000.00)."); return; }

            if (DpExpiry.SelectedDate.HasValue && DpExpiry.SelectedDate.Value.Date <= DateTime.Today)
            { ShowError("Expiry date must be in the future."); return; }

            if (_repo.PolicyNoExists(TxtPolicyNo.Text.Trim()))
            { ShowError("Policy No. '" + TxtPolicyNo.Text.Trim() + "' already exists."); return; }

            var policy = new Policy
            {
                PolicyNo = TxtPolicyNo.Text.Trim(),
                PolicyName = TxtPolicyName.Text.Trim(),
                PolicyType = (CmbType.SelectedItem as ComboBoxItem)?.Content?.ToString(),
                Provider = TxtProvider.Text.Trim(),
                CoverageAmount = coverage,
                PolicyStatus = "Active",
                Description = TxtDescription.Text.Trim(),
                ExpiryDate = DpExpiry.SelectedDate.HasValue
                                 ? AppFormats.ToDisplayDate(DpExpiry.SelectedDate.Value)
                                 : "—"
            };

            if (_repo.Save(policy))
            {
                IsSaved = true;
                MessageBox.Show("✔ Policy '" + policy.PolicyName + "' saved successfully!",
                    "OCIMS — Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
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
