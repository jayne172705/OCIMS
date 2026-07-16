using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using OCIMS.Data;
using OCIMS.Models;

namespace OCIMS
{
    public partial class EditPolicyWindow : Window
    {
        public bool IsSaved { get; private set; } = false;
        private PolicyRepository _repo = new PolicyRepository();
        private Policy _policy;

        public EditPolicyWindow(Policy policy)
        {
            InitializeComponent();
            _policy = policy;

            TxtPolicyName.Text = policy.PolicyName;
            TxtProvider.Text = policy.Provider;
            TxtCoverage.Text = policy.CoverageAmount.ToString("0.##", CultureInfo.InvariantCulture);
            TxtDescription.Text = policy.Description;

            // Set combo selections
            foreach (ComboBoxItem item in CmbType.Items)
                if (item.Content.ToString() == policy.PolicyType)
                { CmbType.SelectedItem = item; break; }

            foreach (ComboBoxItem item in CmbStatus.Items)
                if (item.Content.ToString() == policy.PolicyStatus)
                { CmbStatus.SelectedItem = item; break; }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtPolicyName.Text))
            { ShowError("Please enter the policy name."); return; }

            decimal coverage;
            if (!AppFormats.TryParseAmount(TxtCoverage.Text, out coverage))
            { ShowError("Coverage amount must be a valid positive number (e.g. 250000 or 250,000.00)."); return; }

            var updated = new Policy
            {
                PolicyId = _policy.PolicyId,
                PolicyNo = _policy.PolicyNo,
                PolicyName = TxtPolicyName.Text.Trim(),
                PolicyType = (CmbType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? _policy.PolicyType,
                PolicyStatus = (CmbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? _policy.PolicyStatus,
                Provider = TxtProvider.Text.Trim(),
                CoverageAmount = coverage,
                Description = TxtDescription.Text.Trim(),
                ExpiryDate = _policy.ExpiryDate
            };

            if (_repo.Update(updated))
            {
                _policy.PolicyName = updated.PolicyName;
                _policy.PolicyType = updated.PolicyType;
                _policy.PolicyStatus = updated.PolicyStatus;
                _policy.Provider = updated.Provider;
                _policy.CoverageAmount = updated.CoverageAmount;
                _policy.Description = updated.Description;
                IsSaved = true;
                MessageBox.Show("✔ Policy '" + _policy.PolicyName + "' updated successfully!",
                    "eSureHi — Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
