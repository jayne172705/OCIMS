using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace eSureHi.Views.Admin.Dialogs
{
    public class BarangayFundSourceOption
    {
        public int SourceFundId { get; init; }
        public string Name { get; init; } = string.Empty;
        public decimal AvailableBalance { get; init; }
        public string DisplayText => $"{Name} - Available: {AvailableBalance:N2}";
    }

    public partial class BarangayFundReleaseDialog : Window
    {
        public string BarangayDisplay { get; }
        public decimal CurrentBalance { get; }
        public IReadOnlyList<BarangayFundSourceOption> SourceOptions { get; }
        public BarangayFundSourceOption? SelectedSource { get; set; }
        public DateTime ReleaseDate { get; set; } = DateTime.Today;

        public int SourceFundId => SelectedSource?.SourceFundId ?? 0;
        public decimal Amount { get; private set; }
        public string ReferenceNumber { get; private set; } = string.Empty;
        public string Purpose { get; private set; } = string.Empty;
        public string Remarks { get; private set; } = string.Empty;

        public BarangayFundReleaseDialog(
            string barangay,
            decimal currentBalance,
            IReadOnlyList<BarangayFundSourceOption> sourceOptions)
        {
            BarangayDisplay = $"Barangay {barangay}";
            CurrentBalance = currentBalance;
            SourceOptions = sourceOptions;
            SelectedSource = sourceOptions.Count > 0 ? sourceOptions[0] : null;

            InitializeComponent();
            DataContext = this;
        }

        private void Release_Click(object sender, RoutedEventArgs e)
        {
            ValidationText.Text = string.Empty;

            if (SelectedSource is null)
            {
                ValidationText.Text = "Select a source fund.";
                return;
            }

            if (!decimal.TryParse(
                    AmountTextBox.Text,
                    NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                    CultureInfo.CurrentCulture,
                    out var amount) || amount <= 0)
            {
                ValidationText.Text = "Enter a valid release amount greater than zero.";
                return;
            }

            if (amount > SelectedSource.AvailableBalance)
            {
                ValidationText.Text =
                    $"Amount cannot exceed the available balance of {SelectedSource.AvailableBalance:N2}.";
                return;
            }

            var reference = ReferenceTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(reference))
            {
                ValidationText.Text = "Reference or voucher number is required.";
                return;
            }

            var purpose = PurposeTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(purpose))
            {
                ValidationText.Text = "Purpose is required.";
                return;
            }

            Amount = amount;
            ReferenceNumber = reference;
            Purpose = purpose;
            Remarks = RemarksTextBox.Text.Trim();
            ReleaseDate = ReleaseDatePicker.SelectedDate ?? DateTime.Today;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) =>
            DialogResult = false;
    }
}
