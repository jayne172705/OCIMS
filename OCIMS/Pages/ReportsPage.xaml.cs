using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using OCIMS.Data;
using OCIMS.Models;

namespace OCIMS.Pages
{
    public partial class ReportsPage : Page
    {
        private EmployeeRepository _empRepo = new EmployeeRepository();
        private PolicyRepository _polRepo = new PolicyRepository();
        private ClaimRepository _claimRepo = new ClaimRepository();

        public ReportsPage()
        {
            InitializeComponent();
            LoadStats();
        }

        private void LoadStats()
        {
            try
            {
                var clients = _empRepo.GetAll();
                var policies = _polRepo.GetAll();
                var claims = _claimRepo.GetAll();

                StatClients.Text = clients.Count.ToString();
                StatPolicies.Text = policies.Count.ToString();
                StatClaims.Text = claims.Count.ToString();

                int approved = 0;
                foreach (var c in claims)
                    if (c.ClaimStatus == "Approved") approved++;
                StatApproved.Text = approved.ToString();
            }
            catch { }
        }

        // ── EXPORT CLIENTS ───────────────────────────────────
        private void ExportClients_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Clients",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = "Clients_Report_" + DateTime.Now.ToString("yyyyMMdd")
                };
                if (dlg.ShowDialog() != true) return;
                var clients = _empRepo.GetAll();
                var wb = new ClosedXML.Excel.XLWorkbook();
                var ws = wb.Worksheets.Add("Clients");
                ws.Cell(1, 1).Value = "CLIENT ID"; ws.Cell(1, 2).Value = "FULL NAME";
                ws.Cell(1, 3).Value = "EMAIL"; ws.Cell(1, 4).Value = "PHONE";
                ws.Cell(1, 5).Value = "ADDRESS"; ws.Cell(1, 6).Value = "STATUS";
                StyleHeader(ws.Range("A1:F1"), "#2E86DE");
                int row = 2;
                foreach (var c in clients)
                {
                    ws.Cell(row, 1).Value = c.EmployeeNo; ws.Cell(row, 2).Value = c.FullName;
                    ws.Cell(row, 3).Value = c.Email; ws.Cell(row, 4).Value = c.PhoneMobile;
                    ws.Cell(row, 5).Value = c.Address ?? ""; ws.Cell(row, 6).Value = c.EmploymentStatus;
                    if (row % 2 == 0) ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#F0F4F8");
                    row++;
                }
                ws.Columns().AdjustToContents();
                wb.SaveAs(dlg.FileName);
                Success("Clients report exported! " + dlg.FileName);
                System.Diagnostics.Process.Start(dlg.FileName);
            }
            catch (Exception ex) { Error(ex.Message); }
        }

        // ── EXPORT POLICIES ──────────────────────────────────
        private void ExportPolicies_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Policies",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = "Policies_Report_" + DateTime.Now.ToString("yyyyMMdd")
                };
                if (dlg.ShowDialog() != true) return;
                var policies = _polRepo.GetAll();
                var wb = new ClosedXML.Excel.XLWorkbook();
                var ws = wb.Worksheets.Add("Policies");
                ws.Cell(1, 1).Value = "POLICY NO"; ws.Cell(1, 2).Value = "NAME";
                ws.Cell(1, 3).Value = "TYPE"; ws.Cell(1, 4).Value = "PROVIDER";
                ws.Cell(1, 5).Value = "COVERAGE"; ws.Cell(1, 6).Value = "EXPIRY"; ws.Cell(1, 7).Value = "STATUS";
                StyleHeader(ws.Range("A1:G1"), "#1A8A4A");
                int row = 2;
                foreach (var p in policies)
                {
                    ws.Cell(row, 1).Value = p.PolicyNo; ws.Cell(row, 2).Value = p.PolicyName;
                    ws.Cell(row, 3).Value = p.PolicyType; ws.Cell(row, 4).Value = p.Provider;
                    ws.Cell(row, 5).Value = p.CoverageDisplay; ws.Cell(row, 6).Value = p.ExpiryDate;
                    ws.Cell(row, 7).Value = p.PolicyStatus;
                    if (row % 2 == 0) ws.Range(row, 1, row, 7).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#F0F4F8");
                    row++;
                }
                ws.Columns().AdjustToContents();
                wb.SaveAs(dlg.FileName);
                Success("Policies report exported! " + dlg.FileName);
                System.Diagnostics.Process.Start(dlg.FileName);
            }
            catch (Exception ex) { Error(ex.Message); }
        }

        // ── EXPORT CLAIMS ────────────────────────────────────
        private void ExportClaims_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Claims",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = "Claims_Report_" + DateTime.Now.ToString("yyyyMMdd")
                };
                if (dlg.ShowDialog() != true) return;
                var claims = _claimRepo.GetAll();
                var wb = new ClosedXML.Excel.XLWorkbook();
                var ws = wb.Worksheets.Add("Claims");
                ws.Cell(1, 1).Value = "CLAIM NO"; ws.Cell(1, 2).Value = "CLIENT NAME";
                ws.Cell(1, 3).Value = "TYPE"; ws.Cell(1, 4).Value = "DATE";
                ws.Cell(1, 5).Value = "AMOUNT"; ws.Cell(1, 6).Value = "STATUS";
                StyleHeader(ws.Range("A1:F1"), "#D68910");
                int row = 2;
                foreach (var c in claims)
                {
                    ws.Cell(row, 1).Value = c.ClaimNo; ws.Cell(row, 2).Value = c.ClientName;
                    ws.Cell(row, 3).Value = c.ClaimType; ws.Cell(row, 4).Value = c.ClaimDate;
                    ws.Cell(row, 5).Value = c.AmountDisplay; ws.Cell(row, 6).Value = c.ClaimStatus;
                    if (row % 2 == 0) ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#F0F4F8");
                    row++;
                }
                ws.Columns().AdjustToContents();
                wb.SaveAs(dlg.FileName);
                Success("Claims report exported! " + dlg.FileName);
                System.Diagnostics.Process.Start(dlg.FileName);
            }
            catch (Exception ex) { Error(ex.Message); }
        }

        // ── EXPORT PAYMENTS ──────────────────────────────────
        private void ExportPayments_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Payments are stored in memory per session.\n\n" +
                "To export payments, go to the Payments page and use the Export button there.",
                "OCIMS — Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── PRINT CLIENTS ────────────────────────────────────
        private void PrintClients_Click(object sender, RoutedEventArgs e)
        {
            var clients = _empRepo.GetAll();
            string[] headers = { "Client ID", "Full Name", "Email", "Phone", "Status" };
            var rows = new List<string[]>();
            foreach (var c in clients)
                rows.Add(new[] { c.EmployeeNo, c.FullName, c.Email, c.PhoneMobile, c.EmploymentStatus });
            PrintTable("Clients Report", headers, rows, "#2E86DE",
                new[] { 100.0, 150.0, 160.0, 110.0, 80.0 });
        }

        // ── PRINT POLICIES ───────────────────────────────────
        private void PrintPolicies_Click(object sender, RoutedEventArgs e)
        {
            var policies = _polRepo.GetAll();
            string[] headers = { "Policy No", "Name", "Type", "Provider", "Coverage", "Status" };
            var rows = new List<string[]>();
            foreach (var p in policies)
                rows.Add(new[] { p.PolicyNo, p.PolicyName, p.PolicyType, p.Provider, p.CoverageDisplay, p.PolicyStatus });
            PrintTable("Policies Report", headers, rows, "#1A8A4A",
                new[] { 100.0, 140.0, 90.0, 120.0, 90.0, 80.0 });
        }

        // ── PRINT CLAIMS ─────────────────────────────────────
        private void PrintClaims_Click(object sender, RoutedEventArgs e)
        {
            var claims = _claimRepo.GetAll();
            string[] headers = { "Claim No", "Client Name", "Type", "Date", "Amount", "Status" };
            var rows = new List<string[]>();
            foreach (var c in claims)
                rows.Add(new[] { c.ClaimNo, c.ClientName, c.ClaimType, c.ClaimDate, c.AmountDisplay, c.ClaimStatus });
            PrintTable("Claims Report", headers, rows, "#D68910",
                new[] { 100.0, 150.0, 90.0, 100.0, 90.0, 80.0 });
        }

        // ── PRINT PAYMENTS ───────────────────────────────────
        private void PrintPayments_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "To print payments, go to the Payments page and use the Print button there.",
                "OCIMS — Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── PRINT HELPER ─────────────────────────────────────
        private void PrintTable(string title, string[] headers, List<string[]> rows,
                                string headerColor, double[] colWidths)
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

            doc.Blocks.Add(new Paragraph(new Run("OCIMS — " + title))
            {
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(26, 58, 92)),
                Margin = new Thickness(0, 0, 0, 4)
            });

            doc.Blocks.Add(new Paragraph(new Run(
                "Printed: " + DateTime.Now.ToString("MMM dd, yyyy hh:mm tt") +
                "   |   Total Records: " + rows.Count))
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(122, 143, 166)),
                Margin = new Thickness(0, 0, 0, 16)
            });

            var table = new Table { CellSpacing = 0 };
            foreach (var w in colWidths)
                table.Columns.Add(new TableColumn { Width = new GridLength(w) });

            // Header row
            var hg = new TableRowGroup();
            var hr = new TableRow();
            var col = (Color)ColorConverter.ConvertFromString(headerColor);
            hr.Background = new SolidColorBrush(col);
            foreach (var h in headers)
                hr.Cells.Add(new TableCell(new Paragraph(new Run(h))
                {
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    Margin = new Thickness(6, 4, 6, 4)
                }));
            hg.Rows.Add(hr);
            table.RowGroups.Add(hg);

            // Data rows
            var dg = new TableRowGroup();
            for (int i = 0; i < rows.Count; i++)
            {
                var row = new TableRow
                {
                    Background = i % 2 == 0
                        ? Brushes.White
                        : new SolidColorBrush(Color.FromRgb(250, 252, 255))
                };
                foreach (var cell in rows[i])
                    row.Cells.Add(new TableCell(new Paragraph(new Run(cell ?? ""))
                    { Margin = new Thickness(6, 3, 6, 3), FontSize = 11 }));
                dg.Rows.Add(row);
            }
            table.RowGroups.Add(dg);
            doc.Blocks.Add(table);

            var pag = ((IDocumentPaginatorSource)doc).DocumentPaginator;
            pag.PageSize = new Size(pd.PrintableAreaWidth, pd.PrintableAreaHeight);
            pd.PrintDocument(pag, title + " — OCIMS");
        }

        // ── EXCEL HELPER ─────────────────────────────────────
        private void StyleHeader(ClosedXML.Excel.IXLRange range, string color)
        {
            range.Style.Font.Bold = true;
            range.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            range.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml(color);
            range.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
        }

        private void Success(string msg)
        {
            MessageBox.Show("✔ " + msg, "OCIMS — Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Error(string msg)
        {
            MessageBox.Show("Error: " + msg, "OCIMS",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
