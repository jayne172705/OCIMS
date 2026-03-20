using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OCIMS.Data;
using OCIMS.Models;

namespace OCIMS.Pages
{
    public partial class TransactionsPage : Page
    {
        private TransactionRepository _repo = new TransactionRepository();
        private List<DocumentTransaction> _allTx = new List<DocumentTransaction>();
        private string _typeFilter = "All";
        private bool _isLoaded = false;

        public TransactionsPage()
        {
            InitializeComponent();
            this.Loaded += PageLoaded;
        }

        private void PageLoaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            LoadTransactions();
        }

        private void LoadTransactions()
        {
            if (!_isLoaded) return;
            _allTx = _repo.GetAll();
            UpdateCounters();
            ApplyFilter();
        }

        private void UpdateCounters()
        {
            if (TxtAll == null) return;
            TxtAll.Text = _allTx.Count.ToString();
            TxtIncoming.Text = _allTx.Count(t => t.TransactionType == "Incoming").ToString();
            TxtOutgoing.Text = _allTx.Count(t => t.TransactionType == "Outgoing").ToString();
            TxtPending.Text = _allTx.Count(t => t.Status == "Pending").ToString();
        }

        private void ApplyFilter()
        {
            if (!_isLoaded) return;
            if (TxGrid == null) return;

            string kw = SearchBox != null ? SearchBox.Text.Trim().ToLower() : "";
            var filtered = _allTx.AsEnumerable();

            if (_typeFilter != "All")
                filtered = filtered.Where(t => t.TransactionType == _typeFilter);

            if (!string.IsNullOrEmpty(kw))
                filtered = filtered.Where(t =>
                    (!string.IsNullOrEmpty(t.TransactionNo) && t.TransactionNo.ToLower().Contains(kw)) ||
                    (!string.IsNullOrEmpty(t.Subject) && t.Subject.ToLower().Contains(kw)) ||
                    (!string.IsNullOrEmpty(t.SenderName) && t.SenderName.ToLower().Contains(kw)) ||
                    (!string.IsNullOrEmpty(t.ReceiverName) && t.ReceiverName.ToLower().Contains(kw)) ||
                    (!string.IsNullOrEmpty(t.Status) && t.Status.ToLower().Contains(kw)) ||
                    (!string.IsNullOrEmpty(t.Priority) && t.Priority.ToLower().Contains(kw)));

            var result = filtered.ToList();
            TxGrid.ItemsSource = null;
            TxGrid.ItemsSource = result;

            if (TotalCount != null)
                TotalCount.Text = _typeFilter == "All"
                    ? "Total: " + result.Count + " transactions"
                    : _typeFilter + ": " + result.Count + " transactions";
        }

        private void TypeFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (FAll == null) return;
            if (FAll.IsChecked == true) _typeFilter = "All";
            else if (FIncoming.IsChecked == true) _typeFilter = "Incoming";
            else if (FOutgoing.IsChecked == true) _typeFilter = "Outgoing";
            else if (FInternal.IsChecked == true) _typeFilter = "Internal Transfer";
            ApplyFilter();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void AddTransaction_Click(object sender, RoutedEventArgs e)
        {
            var senders = _repo.GetSenders();
            var receivers = _repo.GetReceivers();
            var dlg = new AddTransactionWindow(senders, receivers);
            dlg.Owner = Window.GetWindow(this);
            dlg.ShowDialog();
            if (dlg.IsSaved) LoadTransactions();
        }

        private void ViewTx_Click(object sender, RoutedEventArgs e)
        {
            var tx = GetRow(sender);
            if (tx == null) return;
            MessageBox.Show(
                "TX NO        : " + tx.TransactionNo + "\n" +
                "TYPE         : " + tx.TransactionType + "\n" +
                "SUBJECT      : " + tx.Subject + "\n" +
                "SENDER       : " + tx.SenderName + "\n" +
                "RECEIVER     : " + tx.ReceiverName + "\n" +
                "DATE         : " + tx.TransactionDate + "\n" +
                "DUE DATE     : " + tx.DueDate + "\n" +
                "PRIORITY     : " + tx.Priority + "\n" +
                "STATUS       : " + tx.Status + "\n" +
                "DOCUMENT     : " + tx.DocumentTitle + "\n" +
                "DESCRIPTION  : " + (tx.Description ?? "-") + "\n" +
                "REMARKS      : " + (tx.Remarks ?? "-"),
                "Transaction Details — " + tx.TransactionNo,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CompleteTx_Click(object sender, RoutedEventArgs e)
        {
            var tx = GetRow(sender);
            if (tx == null) return;
            if (tx.Status == "Completed")
            { MessageBox.Show("This transaction is already completed.", "OCIMS", MessageBoxButton.OK, MessageBoxImage.Information); return; }

            var r = MessageBox.Show(
                "Mark transaction '" + tx.TransactionNo + "' as Completed?",
                "Confirm Complete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes)
            {
                if (_repo.UpdateStatus(tx.TransactionId, "Completed"))
                {
                    MessageBox.Show("✔ Transaction marked as completed.", "OCIMS",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadTransactions();
                }
            }
        }

        private void DeleteTx_Click(object sender, RoutedEventArgs e)
        {
            var tx = GetRow(sender);
            if (tx == null) return;
            var r = MessageBox.Show(
                "Delete transaction '" + tx.TransactionNo + "'?\n\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r == MessageBoxResult.Yes)
            {
                if (_repo.Delete(tx.TransactionId))
                {
                    MessageBox.Show("✔ Transaction deleted.", "OCIMS",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadTransactions();
                }
            }
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Transactions",
                    Filter = "Excel File (*.xlsx)|*.xlsx|CSV File (*.csv)|*.csv",
                    FileName = "Transactions_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
                };
                if (dlg.ShowDialog() != true) return;
                var data = TxGrid.ItemsSource as List<DocumentTransaction>;
                if (data == null) return;
                if (dlg.FileName.EndsWith(".xlsx")) ExportExcel(dlg.FileName, data);
                else ExportCsv(dlg.FileName, data);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Export error: " + ex.Message, "OCIMS",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportExcel(string path, List<DocumentTransaction> data)
        {
            var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("Transactions");
            ws.Cell(1, 1).Value = "TX NO"; ws.Cell(1, 2).Value = "TYPE";
            ws.Cell(1, 3).Value = "SUBJECT"; ws.Cell(1, 4).Value = "SENDER";
            ws.Cell(1, 5).Value = "RECEIVER"; ws.Cell(1, 6).Value = "DATE";
            ws.Cell(1, 7).Value = "PRIORITY"; ws.Cell(1, 8).Value = "STATUS";
            var hdr = ws.Range("A1:H1");
            hdr.Style.Font.Bold = true;
            hdr.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            hdr.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#2E86DE");
            int row = 2;
            foreach (var t in data)
            {
                ws.Cell(row, 1).Value = t.TransactionNo; ws.Cell(row, 2).Value = t.TransactionType;
                ws.Cell(row, 3).Value = t.Subject; ws.Cell(row, 4).Value = t.SenderName;
                ws.Cell(row, 5).Value = t.ReceiverName; ws.Cell(row, 6).Value = t.TransactionDate;
                ws.Cell(row, 7).Value = t.Priority; ws.Cell(row, 8).Value = t.Status;
                if (row % 2 == 0) ws.Range(row, 1, row, 8).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#F0F4F8");
                row++;
            }
            ws.Columns().AdjustToContents();
            wb.SaveAs(path);
            MessageBox.Show("✔ Exported " + (row - 2) + " transactions!\n\n" + path,
                "Export Success", MessageBoxButton.OK, MessageBoxImage.Information);
            System.Diagnostics.Process.Start(path);
        }

        private void ExportCsv(string path, List<DocumentTransaction> data)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("TX NO,TYPE,SUBJECT,SENDER,RECEIVER,DATE,PRIORITY,STATUS");
            foreach (var t in data)
                sb.AppendLine("\"" + t.TransactionNo + "\",\"" + t.TransactionType + "\",\"" + t.Subject + "\",\"" +
                              t.SenderName + "\",\"" + t.ReceiverName + "\",\"" + t.TransactionDate + "\",\"" +
                              t.Priority + "\",\"" + t.Status + "\"");
            System.IO.File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
            MessageBox.Show("✔ CSV exported!\n\n" + path, "Export Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
            System.Diagnostics.Process.Start(path);
        }

        private DocumentTransaction GetRow(object sender)
        {
            var btn = sender as Button;
            if (btn != null)
            {
                var row = FindParent<DataGridRow>(btn);
                if (row != null) return row.Item as DocumentTransaction;
            }
            return TxGrid.SelectedItem as DocumentTransaction;
        }

        private static T FindParent<T>(System.Windows.DependencyObject child)
            where T : System.Windows.DependencyObject
        {
            var p = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (p == null) return null;
            if (p is T t) return t;
            return FindParent<T>(p);
        }
    }
}
