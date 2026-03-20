using System;
using System.Windows;
using System.Windows.Controls;

namespace OCIMS
{
    public partial class AddFundWindow : Window
    {
        public bool IsSaved { get; private set; } = false;
        public Pages.FundSource NewFund { get; private set; }

        private Pages.FundSource _editFund = null;
        private bool _isEdit = false;

        // Add mode
        public AddFundWindow()
        {
            InitializeComponent();
            DpDate.SelectedDate = DateTime.Today;
        }

        // Edit mode
        public AddFundWindow(Pages.FundSource fund)
        {
            InitializeComponent();
            _editFund = fund;
            _isEdit = true;

            HeaderTitle.Text = "Edit Fund Source";
            SaveBtn.Content = "Save Changes";

            TxtFundName.Text = fund.FundName;
            TxtAmount.Text = fund.Amount.ToString();
            TxtSource.Text = fund.Source;
            TxtNotes.Text = fund.Notes ?? "";

            foreach (ComboBoxItem item in CmbFundType.Items)
                if (item.Content.ToString() == fund.FundType)
                { CmbFundType.SelectedItem = item; break; }

            foreach (ComboBoxItem item in CmbStatus.Items)
                if (item.Content.ToString() == fund.FundStatus)
                { CmbStatus.SelectedItem = item; break; }

            if (!string.IsNullOrEmpty(fund.DateReceived))
            {
                try { DpDate.SelectedDate = DateTime.Parse(fund.DateReceived); }
                catch { DpDate.SelectedDate = DateTime.Today; }
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtFundName.Text))
            { ShowError("Please enter the fund name."); return; }

            if (CmbFundType.SelectedItem == null)
            { ShowError("Please select a fund type."); return; }

            if (string.IsNullOrWhiteSpace(TxtAmount.Text))
            { ShowError("Please enter the amount."); return; }

            decimal amount;
            if (!decimal.TryParse(TxtAmount.Text.Replace(",", ""), out amount))
            { ShowError("Amount must be a valid number."); return; }

            if (string.IsNullOrWhiteSpace(TxtSource.Text))
            { ShowError("Please enter the source/donor."); return; }

            if (DpDate.SelectedDate == null)
            { ShowError("Please select the date received."); return; }

            string fundType = (CmbFundType.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string fundStatus = (CmbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Active";
            string dateStr = DpDate.SelectedDate.Value.ToString("MMM dd, yyyy");

            if (_isEdit && _editFund != null)
            {
                // Update existing
                _editFund.FundName = TxtFundName.Text.Trim();
                _editFund.FundType = fundType;
                _editFund.Amount = amount;
                _editFund.Source = TxtSource.Text.Trim();
                _editFund.DateReceived = dateStr;
                _editFund.FundStatus = fundStatus;
                _editFund.Notes = TxtNotes.Text.Trim();
                IsSaved = true;
                MessageBox.Show("✔ Fund source updated successfully!",
                    "OCIMS — Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // New fund
                NewFund = new Pages.FundSource
                {
                    FundName = TxtFundName.Text.Trim(),
                    FundType = fundType,
                    Amount = amount,
                    Source = TxtSource.Text.Trim(),
                    DateReceived = dateStr,
                    FundStatus = fundStatus,
                    Notes = TxtNotes.Text.Trim()
                };
                IsSaved = true;
                MessageBox.Show("✔ Fund source '" + NewFund.FundName + "' added successfully!",
                    "OCIMS — Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            this.Close();
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
