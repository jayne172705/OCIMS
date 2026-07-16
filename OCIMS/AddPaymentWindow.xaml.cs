using System;
using System.Windows;
using System.Windows.Controls;
using OCIMS.Data;
using OCIMS.Models;

namespace OCIMS
{
    public partial class AddPaymentWindow : Window
    {
        public bool IsSaved { get; private set; } = false;
        public Payment NewPayment { get; private set; }

        private readonly PaymentRepository _repo = new PaymentRepository();

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
            if (!AppFormats.TryParseAmount(TxtAmount.Text, out amount))
            { ShowError("Amount must be a valid number greater than zero."); return; }

            if (CmbMethod.SelectedItem == null)
            { ShowError("Please select a payment method."); return; }

            if (DpDate.SelectedDate == null)
            { ShowError("Please select a payment date."); return; }

            string paymentNo = GeneratePaymentNo();
            if (paymentNo == null)
            { ShowError("Could not generate a unique payment number. Please try again."); return; }

            var payment = new Payment
            {
                PaymentNo = paymentNo,
                ClientName = TxtClientName.Text.Trim(),
                Amount = amount,
                PaymentDate = AppFormats.ToDisplayDate(DpDate.SelectedDate.Value),
                PaymentMode = (CmbMethod.SelectedItem as ComboBoxItem)?.Content?.ToString(),
                PaymentStatus = "Paid",
                Notes = TxtNotes.Text.Trim()
            };

            if (!_repo.Save(payment)) return;

            NewPayment = payment;
            IsSaved = true;
            MessageBox.Show(
                "✔ Payment saved successfully!",
                "eSureHi — Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            this.Close();
        }

        private string GeneratePaymentNo()
        {
            string paymentNo = "PAY-" + DateTime.Now.ToString("yyMMddHHmmss");
            if (!_repo.PaymentNoExists(paymentNo)) return paymentNo;

            var rnd = new Random();
            for (int i = 0; i < 5; i++)
            {
                string candidate = paymentNo + "-" + rnd.Next(100, 1000);
                if (!_repo.PaymentNoExists(candidate)) return candidate;
            }
            return null;
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
