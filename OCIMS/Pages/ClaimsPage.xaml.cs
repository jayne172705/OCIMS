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
    public partial class ClaimsPage : Page
    {
        private ClaimRepository _repo = new ClaimRepository();
        private List<Claim> _allClaims = new List<Claim>();
        private string _currentFilter = "All";
        private bool _isLoaded = false;

        public ClaimsPage()
        {
            InitializeComponent();
            this.Loaded += PageLoaded;
        }

        private void PageLoaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            LoadClaims();
        }

        private void LoadClaims()
        {
            if (!_isLoaded) return;
            _allClaims = _repo.GetAll();
            UpdateCounters();
            ApplyFilter();
        }

        private void UpdateCounters()
        {
            if (TxtAll == null) return;
            TxtAll.Text = _allClaims.Count.ToString();
            TxtPending.Text = _allClaims.Count(c => c.ClaimStatus == "Pending").ToString();
            TxtApproved.Text = _allClaims.Count(c => c.ClaimStatus == "Approved").ToString();
            TxtRejected.Text = _allClaims.Count(c => c.ClaimStatus == "Rejected").ToString();
        }

        private void StatusFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (FilterAll == null) return;
            if (FilterAll.IsChecked == true) _currentFilter = "All";
            else if (FilterPending.IsChecked == true) _currentFilter = "Pending";
            else if (FilterApproved.IsChecked == true) _currentFilter = "Approved";
            else if (FilterRejected.IsChecked == true) _currentFilter = "Rejected";
            else if (FilterReleased.IsChecked == true) _currentFilter = "Released";
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (!_isLoaded) return;
            if (ClaimsGrid == null) return;

            string kw = SearchBox != null ? SearchBox.Text.Trim().ToLower() : "";
            var filtered = _allClaims.AsEnumerable();

            if (_currentFilter != "All")
                filtered = filtered.Where(c => c.ClaimStatus == _currentFilter);

            if (!string.IsNullOrEmpty(kw))
                filtered = filtered.Where(c =>
                    (!string.IsNullOrEmpty(c.ClaimNo) && c.ClaimNo.ToLower().Contains(kw)) ||
                    (!string.IsNullOrEmpty(c.ClientName) && c.ClientName.ToLower().Contains(kw)) ||
                    (!string.IsNullOrEmpty(c.ClaimType) && c.ClaimType.ToLower().Contains(kw)) ||
                    (!string.IsNullOrEmpty(c.ClaimStatus) && c.ClaimStatus.ToLower().Contains(kw)));

            var result = filtered.ToList();
            ClaimsGrid.ItemsSource = null;
            ClaimsGrid.ItemsSource = result;

            if (TotalCount != null)
                TotalCount.Text = _currentFilter == "All"
                    ? "Total: " + result.Count + " claims"
                    : _currentFilter + ": " + result.Count + " claims";
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void FileClaim_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AddClaimWindow();
            dlg.Owner = Window.GetWindow(this);
            dlg.ShowDialog();
            if (dlg.IsSaved) LoadClaims();
        }

        private void ApproveClaim_Click(object sender, RoutedEventArgs e)
        {
            var c = GetRow(sender);
            if (c == null) return;
            if (c.ClaimStatus == "Approved")
            { MessageBox.Show("This claim is already approved.", "OCIMS", MessageBoxButton.OK, MessageBoxImage.Information); return; }

            var r = MessageBox.Show("Approve claim '" + c.ClaimNo + "' for " + c.ClientName + "?",
                "Confirm Approve", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes)
            {
                if (_repo.UpdateStatus(c.ClaimId, "Approved"))
                {
                    ShowToast("✔ Claim approved successfully!");
                    LoadClaims();
                }
            }
        }

        private void RejectClaim_Click(object sender, RoutedEventArgs e)
        {
            var c = GetRow(sender);
            if (c == null) return;
            if (c.ClaimStatus == "Rejected")
            { MessageBox.Show("This claim is already rejected.", "OCIMS", MessageBoxButton.OK, MessageBoxImage.Information); return; }

            var r = MessageBox.Show("Reject claim '" + c.ClaimNo + "'?",
                "Confirm Reject", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r == MessageBoxResult.Yes)
            {
                if (_repo.UpdateStatus(c.ClaimId, "Rejected"))
                {
                    ShowToast("Claim rejected.");
                    LoadClaims();
                }
            }
        }

        private void DeleteClaim_Click(object sender, RoutedEventArgs e)
        {
            var c = GetRow(sender);
            if (c == null) return;
            var r = MessageBox.Show("Delete claim '" + c.ClaimNo + "'?\n\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r == MessageBoxResult.Yes)
            {
                if (_repo.Delete(c.ClaimId))
                {
                    ShowToast("✔ Claim deleted.");
                    LoadClaims();
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

            doc.Blocks.Add(new Paragraph(new Run("OCIMS — Claims Report"))
            {
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(26, 58, 92)),
                Margin = new Thickness(0, 0, 0, 4)
            });

            var claims = ClaimsGrid.ItemsSource as List<Claim>;
            int count = claims != null ? claims.Count : 0;

            doc.Blocks.Add(new Paragraph(new Run(
                "Filter: " + _currentFilter +
                "   |   Date Printed: " + DateTime.Now.ToString("MMM dd, yyyy hh:mm tt") +
                "   |   Total Records: " + count))
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(122, 143, 166)),
                Margin = new Thickness(0, 0, 0, 16)
            });

            var table = new Table { CellSpacing = 0 };
            table.Columns.Add(new TableColumn { Width = new GridLength(100) });
            table.Columns.Add(new TableColumn { Width = new GridLength(150) });
            table.Columns.Add(new TableColumn { Width = new GridLength(90) });
            table.Columns.Add(new TableColumn { Width = new GridLength(90) });
            table.Columns.Add(new TableColumn { Width = new GridLength(90) });
            table.Columns.Add(new TableColumn { Width = new GridLength(80) });

            var hg = new TableRowGroup();
            var hr = new TableRow { Background = new SolidColorBrush(Color.FromRgb(46, 134, 222)) };
            foreach (var h in new[] { "Claim No", "Client Name", "Type", "Date Filed", "Amount", "Status" })
                hr.Cells.Add(new TableCell(new Paragraph(new Run(h))
                {
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    Margin = new Thickness(6, 4, 6, 4)
                }));
            hg.Rows.Add(hr);
            table.RowGroups.Add(hg);

            var dg = new TableRowGroup();
            if (claims != null)
            {
                int i = 0;
                foreach (var c in claims)
                {
                    var row = new TableRow
                    {
                        Background = i % 2 == 0
                            ? Brushes.White
                            : new SolidColorBrush(Color.FromRgb(250, 252, 255))
                    };
                    row.Cells.Add(MakeCell(c.ClaimNo));
                    row.Cells.Add(MakeCell(c.ClientName));
                    row.Cells.Add(MakeCell(c.ClaimType));
                    row.Cells.Add(MakeCell(c.ClaimDate));
                    row.Cells.Add(MakeCell(c.AmountDisplay));
                    row.Cells.Add(MakeCell(c.ClaimStatus));
                    dg.Rows.Add(row);
                    i++;
                }
            }
            table.RowGroups.Add(dg);
            doc.Blocks.Add(table);

            var pag = ((IDocumentPaginatorSource)doc).DocumentPaginator;
            pag.PageSize = new Size(pd.PrintableAreaWidth, pd.PrintableAreaHeight);
            pd.PrintDocument(pag, "Claims Report — OCIMS");
            ShowToast("✔ Sent to printer!");
        }

        private TableCell MakeCell(string text)
        {
            return new TableCell(new Paragraph(new Run(text ?? ""))
            {
                Margin = new Thickness(6, 3, 6, 3),
                FontSize = 11
            });
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Claims",
                    Filter = "Excel File (*.xlsx)|*.xlsx|CSV File (*.csv)|*.csv",
                    FileName = "Claims_" + _currentFilter + "_" + DateTime.Now.ToString("yyyyMMdd")
                };
                if (dlg.ShowDialog() != true) return;
                var data = ClaimsGrid.ItemsSource as List<Claim>;
                if (data == null) return;
                if (dlg.FileName.EndsWith(".xlsx")) ExportExcel(dlg.FileName, data);
                else ExportCsv(dlg.FileName, data);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Export error: " + ex.Message, "OCIMS", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportExcel(string path, List<Claim> data)
        {
            var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("Claims");
            ws.Cell(1, 1).Value = "CLAIM NO"; ws.Cell(1, 2).Value = "CLIENT NAME";
            ws.Cell(1, 3).Value = "TYPE"; ws.Cell(1, 4).Value = "DATE FILED";
            ws.Cell(1, 5).Value = "AMOUNT"; ws.Cell(1, 6).Value = "STATUS";
            var hdr = ws.Range("A1:F1");
            hdr.Style.Font.Bold = true;
            hdr.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            hdr.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#2E86DE");
            int row = 2;
            foreach (var c in data)
            {
                ws.Cell(row, 1).Value = c.ClaimNo; ws.Cell(row, 2).Value = c.ClientName;
                ws.Cell(row, 3).Value = c.ClaimType; ws.Cell(row, 4).Value = c.ClaimDate;
                ws.Cell(row, 5).Value = c.AmountDisplay; ws.Cell(row, 6).Value = c.ClaimStatus;
                if (row % 2 == 0) ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#F0F4F8");
                row++;
            }
            ws.Columns().AdjustToContents();
            wb.SaveAs(path);
            ShowToast("✔ Exported " + (row - 2) + " claims!");
            System.Diagnostics.Process.Start(path);
        }

        private void ExportCsv(string path, List<Claim> data)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("CLAIM NO,CLIENT NAME,TYPE,DATE FILED,AMOUNT,STATUS");
            foreach (var c in data)
                sb.AppendLine("\"" + c.ClaimNo + "\",\"" + c.ClientName + "\",\"" + c.ClaimType + "\",\"" +
                              c.ClaimDate + "\",\"" + c.AmountDisplay + "\",\"" + c.ClaimStatus + "\"");
            System.IO.File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
            ShowToast("✔ CSV exported!");
            System.Diagnostics.Process.Start(path);
        }

        private void ShowToast(string message)
        {
            MessageBox.Show(message, "OCIMS", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private Claim GetRow(object sender)
        {
            var btn = sender as Button;
            if (btn != null)
            {
                var row = FindParent<DataGridRow>(btn);
                if (row != null) return row.Item as Claim;
            }
            return ClaimsGrid.SelectedItem as Claim;
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