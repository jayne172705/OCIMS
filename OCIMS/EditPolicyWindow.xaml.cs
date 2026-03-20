using System;
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
            TxtCoverage.Text = policy.CoverageAmount.ToString();
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
            if (!decimal.TryParse(TxtCoverage.Text.Replace(",", ""), out coverage))
            { ShowError("Coverage amount must be a valid number."); return; }

            _policy.PolicyName = TxtPolicyName.Text.Trim();
            _policy.PolicyType = (CmbType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? _policy.PolicyType;
            _policy.PolicyStatus = (CmbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? _policy.PolicyStatus;
            _policy.Provider = TxtProvider.Text.Trim();
            _policy.CoverageAmount = coverage;
            _policy.Description = TxtDescription.Text.Trim();

            if (_repo.Update(_policy))
            {
                IsSaved = true;
                MessageBox.Show("✔ Policy '" + _policy.PolicyName + "' updated successfully!",
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
