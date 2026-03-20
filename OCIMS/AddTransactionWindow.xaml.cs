using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using OCIMS.Data;
using OCIMS.Models;

namespace OCIMS
{
    public partial class AddTransactionWindow : Window
    {
        public bool IsSaved { get; private set; } = false;
        private TransactionRepository _repo = new TransactionRepository();
        private List<Sender> _senders;
        private List<Receiver> _receivers;

        public AddTransactionWindow(List<Sender> senders, List<Receiver> receivers)
        {
            InitializeComponent();
            _senders = senders;
            _receivers = receivers;

            // Auto-generate TX No
            TxtTxNo.Text = "TX-" + DateTime.Now.ToString("yyMMddHHmmss");

            // Populate Senders
            CmbSender.Items.Clear();
            CmbSender.Items.Add(new ComboBoxItem { Content = "— Select Sender —", Tag = 0 });
            foreach (var s in _senders)
                CmbSender.Items.Add(new ComboBoxItem
                {
                    Content = s.SenderName + " (" + s.SenderType + ")",
                    Tag = s.SenderId
                });
            CmbSender.SelectedIndex = 0;

            // Populate Receivers
            CmbReceiver.Items.Clear();
            CmbReceiver.Items.Add(new ComboBoxItem { Content = "— Select Receiver —", Tag = 0 });
            foreach (var r in _receivers)
                CmbReceiver.Items.Add(new ComboBoxItem
                {
                    Content = r.ReceiverName + " (" + r.Department + ")",
                    Tag = r.ReceiverId
                });
            CmbReceiver.SelectedIndex = 0;
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (CmbType.SelectedItem == null)
            { ShowError("Please select a transaction type."); return; }

            if (string.IsNullOrWhiteSpace(TxtSubject.Text))
            { ShowError("Please enter the subject."); return; }

            // Get sender ID
            int senderId = 0;
            if (CmbSender.SelectedItem != null)
            {
                var sel = CmbSender.SelectedItem as ComboBoxItem;
                if (sel != null) senderId = (int)sel.Tag;
            }
            if (senderId == 0)
            { ShowError("Please select a sender."); return; }

            // Get receiver ID
            int receiverId = 0;
            if (CmbReceiver.SelectedItem != null)
            {
                var sel = CmbReceiver.SelectedItem as ComboBoxItem;
                if (sel != null) receiverId = (int)sel.Tag;
            }
            if (receiverId == 0)
            { ShowError("Please select a receiver."); return; }

            // Check duplicate TX No
            if (_repo.TxNoExists(TxtTxNo.Text.Trim()))
            { ShowError("Transaction No. already exists. Please restart."); return; }

            string priority = "Normal";
            if (CmbPriority.SelectedItem != null)
            {
                var sel = CmbPriority.SelectedItem as ComboBoxItem;
                if (sel != null) priority = sel.Content.ToString();
            }

            string dueDate = "";
            if (DpDueDate.SelectedDate.HasValue)
                dueDate = DpDueDate.SelectedDate.Value.ToString("MMM dd, yyyy");

            var tx = new DocumentTransaction
            {
                TransactionNo = TxtTxNo.Text.Trim(),
                TransactionType = (CmbType.SelectedItem as ComboBoxItem).Content.ToString(),
                Subject = TxtSubject.Text.Trim(),
                Description = TxtDescription.Text.Trim(),
                Priority = priority,
                DueDate = dueDate,
                Remarks = TxtRemarks.Text.Trim(),
                Status = "Pending"
            };

            if (_repo.Save(tx, senderId, receiverId))
            {
                IsSaved = true;
                MessageBox.Show(
                    "✔ Transaction '" + tx.TransactionNo + "' saved successfully!",
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
