using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using OCIMS.Data;
using OCIMS.Models;

namespace OCIMS.Pages
{
    public partial class PaymentsPage : Page
    {
        private readonly PaymentRepository _repo = new PaymentRepository();
        private List<Payment> _allPayments = new List<Payment>();
        private string _statusFilter = "All";
        private bool _isLoaded = false;

        public PaymentsPage()
        {
            InitializeComponent();
            _isLoaded = true;
            ReloadPayments();
        }

        private void ReloadPayments()
        {
            _allPayments = _repo.GetAll();
            LoadPayments();
        }

        private void LoadPayments()
        {
            if (!_isLoaded) return;
            PaymentsGrid.ItemsSource = null;
            PaymentsGrid.ItemsSource = _allPayments;
            UpdateSummary();
            ApplyFilter();
        }

        private void UpdateSummary()
        {
            TxtTotalCount.Text = _allPayments.Count.ToString();
            TxtUnpaid.Text = _allPayments.Count(p => p.PaymentStatus == "Unpaid").ToString();
            decimal total = 0;
            foreach (var p in _allPayments.Where(x => x.PaymentStatus == "Paid"))
                total += p.Amount;
            TxtTotalAmt.Text = "₱" + total.ToString("N2");
        }

        private void ApplyFilter()
        {
            if (!_isLoaded) return;
            if (PaymentsGrid == null) return;

            string kw = SearchBox != null ? SearchBox.Text.Trim().ToLower() : "";

            var filtered = _allPayments.AsEnumerable();

            if (_statusFilter != "All")
                filtered = filtered.Where(p => p.PaymentStatus == _statusFilter);

            if (DpFrom != null && DpFrom.SelectedDate.HasValue)
            {
                var from = DpFrom.SelectedDate.Value.Date;
                filtered = filtered.Where(p =>
                {
                    DateTime dt;
                    return AppFormats.TryParseDisplayDate(p.PaymentDate, out dt) && dt.Date >= from;
                });
            }

            if (DpTo != null && DpTo.SelectedDate.HasValue)
            {
                var to = DpTo.SelectedDate.Value.Date;
                filtered = filtered.Where(p =>
                {
                    DateTime dt;
                    return AppFormats.TryParseDisplayDate(p.PaymentDate, out dt) && dt.Date <= to;
                });
            }

            if (!string.IsNullOrEmpty(kw))
                filtered = filtered.Where(p =>
                    (!string.IsNullOrEmpty(p.PaymentNo) && p.PaymentNo.ToLower().Contains(kw)) ||
                    (!string.IsNullOrEmpty(p.ClientName) && p.ClientName.ToLower().Contains(kw)) ||
                    (!string.IsNullOrEmpty(p.PaymentMode) && p.PaymentMode.ToLower().Contains(kw)) ||
                    (!string.IsNullOrEmpty(p.PaymentStatus) && p.PaymentStatus.ToLower().Contains(kw)));

            var result = filtered.ToList();
            PaymentsGrid.ItemsSource = null;
            PaymentsGrid.ItemsSource = result;

            decimal filteredTotal = 0;
            foreach (var p in result.Where(x => x.PaymentStatus == "Paid"))
                filteredTotal += p.Amount;

            if (TotalCount != null) TotalCount.Text = "Showing: " + result.Count + " payments";
            if (FilteredTotal != null) FilteredTotal.Text = "Total: ₱" + filteredTotal.ToString("N2");
        }

        private void DateFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ClearDateFilter_Click(object sender, RoutedEventArgs e)
        {
            if (DpFrom != null) DpFrom.SelectedDate = null;
            if (DpTo != null) DpTo.SelectedDate = null;
            ApplyFilter();
        }

        private void PayFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (PayFilterAll == null) return;
            if (PayFilterAll.IsChecked == true) _statusFilter = "All";
            else if (PayFilterPaid.IsChecked == true) _statusFilter = "Paid";
            else if (PayFilterUnpaid.IsChecked == true) _statusFilter = "Unpaid";
            ApplyFilter();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void AddPayment_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AddPaymentWindow();
            dlg.Owner = Window.GetWindow(this);
            dlg.ShowDialog();
            if (dlg.IsSaved && dlg.NewPayment != null)
            {
                ReloadPayments();
            }
        }

        private void ViewPayment_Click(object sender, RoutedEventArgs e)
        {
            var p = GetRow(sender);
            if (p == null) return;
            MessageBox.Show(
                "PAYMENT NO : " + p.PaymentNo + "\n" +
                "CLIENT     : " + p.ClientName + "\n" +
                "AMOUNT     : " + p.AmountDisplay + "\n" +
                "DATE       : " + p.PaymentDate + "\n" +
                "METHOD     : " + p.PaymentMode + "\n" +
                "STATUS     : " + p.PaymentStatus + "\n" +
                "NOTES      : " + (p.Notes ?? "-"),
                "Payment Details", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeletePayment_Click(object sender, RoutedEventArgs e)
        {
            var p = GetRow(sender);
            if (p == null) return;
            var r = MessageBox.Show("Delete payment '" + p.PaymentNo + "'?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r == MessageBoxResult.Yes)
            {
                if (_repo.Delete(p.PaymentId))
                {
                    _allPayments.Remove(p);
                    LoadPayments();
                }
            }
        }

        private void PrintBtn_Click(object sender, RoutedEventArgs e)
        {
            var pd = new PrintDialog();
            if (pd.ShowDialog() != true) return;

            var doc = new FlowDocument
            {
                PagePadding = new Thickness(40),
                ColumnWidth = double.MaxValue,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12
            };

            doc.Blocks.Add(new Paragraph(new Run("eSureHi — Payments Report"))
            {
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(26, 58, 92)),
                Margin = new Thickness(0, 0, 0, 4)
            });

            string dateRange = "";
            bool hasFrom = DpFrom != null && DpFrom.SelectedDate.HasValue;
            bool hasTo = DpTo != null && DpTo.SelectedDate.HasValue;
            if (hasFrom || hasTo)
                dateRange = "   |   Date Range: " +
                    (hasFrom ? AppFormats.ToDisplayDate(DpFrom.SelectedDate.Value) : "Any") +
                    " — " +
                    (hasTo ? AppFormats.ToDisplayDate(DpTo.SelectedDate.Value) : "Any");

            doc.Blocks.Add(new Paragraph(new Run(
                "Status: " + _statusFilter + dateRange +
                "   |   Printed: " + DateTime.Now.ToString("MMM dd, yyyy hh:mm tt")))
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(122, 143, 166)),
                Margin = new Thickness(0, 0, 0, 16)
            });

            var table = new Table { CellSpacing = 0 };
            table.Columns.Add(new TableColumn { Width = new GridLength(110) });
            table.Columns.Add(new TableColumn { Width = new GridLength(150) });
            table.Columns.Add(new TableColumn { Width = new GridLength(100) });
            table.Columns.Add(new TableColumn { Width = new GridLength(100) });
            table.Columns.Add(new TableColumn { Width = new GridLength(120) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });

            var hg = new TableRowGroup();
            var hr = new TableRow { Background = new SolidColorBrush(Color.FromRgb(46, 134, 222)) };
            foreach (var h in new[] { "Payment No", "Client Name", "Amount", "Date", "Method", "Status" })
                hr.Cells.Add(new TableCell(new Paragraph(new Run(h))
                {
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    Margin = new Thickness(6, 4, 6, 4)
                }));
            hg.Rows.Add(hr);
            table.RowGroups.Add(hg);

            var dg = new TableRowGroup();
            var payments = PaymentsGrid.ItemsSource as List<Payment>;
            if (payments != null)
            {
                int i = 0;
                foreach (var p in payments)
                {
                    var row = new TableRow
                    {
                        Background = i % 2 == 0
                            ? Brushes.White
                            : new SolidColorBrush(Color.FromRgb(250, 252, 255))
                    };
                    row.Cells.Add(MakeCell(p.PaymentNo));
                    row.Cells.Add(MakeCell(p.ClientName));
                    row.Cells.Add(MakeCell(p.AmountDisplay));
                    row.Cells.Add(MakeCell(p.PaymentDate));
                    row.Cells.Add(MakeCell(p.PaymentMode));
                    row.Cells.Add(MakeCell(p.PaymentStatus));
                    dg.Rows.Add(row);
                    i++;
                }
            }
            table.RowGroups.Add(dg);
            doc.Blocks.Add(table);

            var pag = ((IDocumentPaginatorSource)doc).DocumentPaginator;
            pag.PageSize = new Size(pd.PrintableAreaWidth, pd.PrintableAreaHeight);
            pd.PrintDocument(pag, "Payments Report — OCIMS");
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Payments",
                    Filter = "Excel File (*.xlsx)|*.xlsx|CSV File (*.csv)|*.csv",
                    FileName = "Payments_" + DateTime.Now.ToString("yyyyMMdd")
                };
                if (dlg.ShowDialog() != true) return;
                var data = PaymentsGrid.ItemsSource as List<Payment>;
                if (data == null) return;
                if (ExportHelper.IsXlsx(dlg.FileName)) ExportExcel(dlg.FileName, data);
                else ExportCsv(dlg.FileName, data);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Export error: " + ex.Message, "eSureHi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportExcel(string path, List<Payment> data)
        {
            var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("Payments");
            ws.Cell(1, 1).Value = "PAYMENT NO"; ws.Cell(1, 2).Value = "CLIENT NAME";
            ws.Cell(1, 3).Value = "AMOUNT"; ws.Cell(1, 4).Value = "DATE";
            ws.Cell(1, 5).Value = "METHOD"; ws.Cell(1, 6).Value = "STATUS";
            var hdr = ws.Range("A1:F1");
            hdr.Style.Font.Bold = true;
            hdr.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            hdr.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#2E86DE");
            int row = 2;
            foreach (var p in data)
            {
                ws.Cell(row, 1).Value = p.PaymentNo; ws.Cell(row, 2).Value = p.ClientName;
                ws.Cell(row, 3).Value = p.AmountDisplay; ws.Cell(row, 4).Value = p.PaymentDate;
                ws.Cell(row, 5).Value = p.PaymentMode; ws.Cell(row, 6).Value = p.PaymentStatus;
                if (row % 2 == 0) ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#F0F4F8");
                row++;
            }
            ws.Columns().AdjustToContents();
            wb.SaveAs(path);
            ExportHelper.OfferOpen(path);
        }

        private void ExportCsv(string path, List<Payment> data)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(ExportHelper.CsvLine("PAYMENT NO", "CLIENT NAME", "AMOUNT", "DATE", "METHOD", "STATUS"));
            foreach (var p in data)
                sb.AppendLine(ExportHelper.CsvLine(p.PaymentNo, p.ClientName, p.AmountDisplay,
                    p.PaymentDate, p.PaymentMode, p.PaymentStatus));
            System.IO.File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
            ExportHelper.OfferOpen(path);
        }

        private TableCell MakeCell(string text)
        {
            return new TableCell(new Paragraph(new Run(text ?? ""))
            {
                Margin = new Thickness(6, 3, 6, 3),
                FontSize = 11
            });
        }

        private Payment GetRow(object sender)
        {
            var btn = sender as Button;
            if (btn != null)
            {
                var row = FindParent<DataGridRow>(btn);
                if (row != null) return row.Item as Payment;
            }
            return PaymentsGrid.SelectedItem as Payment;
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