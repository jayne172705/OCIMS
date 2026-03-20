using System;
using System.Windows;
using System.Windows.Controls;
using OCIMS.Models;

namespace OCIMS
{
    public partial class AddPaymentWindow : Window
    {
        public bool IsSaved { get; private set; } = false;
        public Payment NewPayment { get; private set; }

        public AddPaymentWindow()
        {
            InitializeComponent();
            DpDate.SelectedDate = DateTime.Today;
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtClientName.Text))
            { ShowError("Please enter the client name."); return; }

            if (string.IsNullOrWhiteSpace(TxtAmount.Text))
            { ShowError("Please enter the amount."); return; }

            decimal amount;
            if (!decimal.TryParse(TxtAmount.Text.Replace(",", ""), out amount))
            { ShowError("Amount must be a valid number."); return; }

            if (CmbMethod.SelectedItem == null)
            { ShowError("Please select a payment method."); return; }

            if (DpDate.SelectedDate == null)
            { ShowError("Please select a payment date."); return; }

            NewPayment = new Payment
            {
                PaymentNo = "PAY-" + DateTime.Now.ToString("yyMMddHHmmss"),
                ClientName = TxtClientName.Text.Trim(),
                Amount = amount,
                PaymentDate = DpDate.SelectedDate.Value.ToString("MMM dd, yyyy"),
                PaymentMode = (CmbMethod.SelectedItem as ComboBoxItem)?.Content?.ToString(),
                PaymentStatus = "Paid",
                Notes = TxtNotes.Text.Trim()
            };

            IsSaved = true;
            MessageBox.Show(
                "✔ Payment saved successfully!",
                "OCIMS — Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
