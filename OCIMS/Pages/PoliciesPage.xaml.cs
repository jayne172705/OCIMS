using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OCIMS.Data;
using OCIMS.Models;

namespace OCIMS.Pages
{
    public partial class PoliciesPage : Page
    {
        private PolicyRepository _repo = new PolicyRepository();
        private List<Policy> _allPolicies = new List<Policy>();

        public PoliciesPage()
        {
            InitializeComponent();
            LoadPolicies();
        }

        private void LoadPolicies()
        {
            _allPolicies = _repo.GetAll();
            PoliciesGrid.ItemsSource = null;
            PoliciesGrid.ItemsSource = _allPolicies;
            TotalCount.Text = "Total: " + _allPolicies.Count + " policies";
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string kw = SearchBox.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(kw))
            {
                PoliciesGrid.ItemsSource = null;
                PoliciesGrid.ItemsSource = _allPolicies;
                TotalCount.Text = "Total: " + _allPolicies.Count + " policies";
                return;
            }
            var f = _allPolicies.Where(p =>
                (!string.IsNullOrEmpty(p.PolicyNo) && p.PolicyNo.ToLower().Contains(kw)) ||
                (!string.IsNullOrEmpty(p.PolicyName) && p.PolicyName.ToLower().Contains(kw)) ||
                (!string.IsNullOrEmpty(p.PolicyType) && p.PolicyType.ToLower().Contains(kw)) ||
                (!string.IsNullOrEmpty(p.Provider) && p.Provider.ToLower().Contains(kw))
            ).ToList();
            PoliciesGrid.ItemsSource = null;
            PoliciesGrid.ItemsSource = f;
            TotalCount.Text = "Showing " + f.Count + " of " + _allPolicies.Count + " policies";
        }

        private void AddPolicy_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AddPolicyWindow();
            dlg.Owner = Window.GetWindow(this);
            dlg.ShowDialog();
            if (dlg.IsSaved) LoadPolicies();
        }

        private void ViewPolicy_Click(object sender, RoutedEventArgs e)
        {
            var p = GetRow(sender);
            if (p == null) return;
            MessageBox.Show(
                "POLICY NO  : " + p.PolicyNo + "\n" +
                "NAME       : " + p.PolicyName + "\n" +
                "TYPE       : " + p.PolicyType + "\n" +
                "PROVIDER   : " + p.Provider + "\n" +
                "COVERAGE   : " + p.CoverageDisplay + "\n" +
                "EXPIRY     : " + p.ExpiryDate + "\n" +
                "STATUS     : " + p.PolicyStatus + "\n" +
                "DESCRIPTION: " + p.Description,
                "Policy Details — " + p.PolicyName,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void EditPolicy_Click(object sender, RoutedEventArgs e)
        {
            var p = GetRow(sender);
            if (p == null) return;
            var dlg = new EditPolicyWindow(p);
            dlg.Owner = Window.GetWindow(this);
            dlg.ShowDialog();
            if (dlg.IsSaved) LoadPolicies();
        }

        private void DeletePolicy_Click(object sender, RoutedEventArgs e)
        {
            var p = GetRow(sender);
            if (p == null) return;
            var result = MessageBox.Show(
                "Delete policy '" + p.PolicyName + "'?\n\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                if (_repo.Delete(p.PolicyId))
                {
                    MessageBox.Show("✔ Policy deleted.", "eSureHi",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadPolicies();
                }
            }
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Policies",
                    Filter = "Excel File (*.xlsx)|*.xlsx|CSV File (*.csv)|*.csv",
                    FileName = "Policies_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
                };
                if (dlg.ShowDialog() != true) return;
                if (ExportHelper.IsXlsx(dlg.FileName)) ExportExcel(dlg.FileName);
                else ExportCsv(dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Export error: " + ex.Message, "eSureHi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportExcel(string path)
        {
            var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("Policies");
            ws.Cell(1, 1).Value = "POLICY NO"; ws.Cell(1, 2).Value = "POLICY NAME";
            ws.Cell(1, 3).Value = "TYPE"; ws.Cell(1, 4).Value = "PROVIDER";
            ws.Cell(1, 5).Value = "COVERAGE"; ws.Cell(1, 6).Value = "EXPIRY DATE";
            ws.Cell(1, 7).Value = "STATUS";
            var hdr = ws.Range("A1:G1");
            hdr.Style.Font.Bold = true;
            hdr.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            hdr.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#2E86DE");
            int row = 2;
            foreach (var p in _allPolicies)
            {
                ws.Cell(row, 1).Value = p.PolicyNo; ws.Cell(row, 2).Value = p.PolicyName;
                ws.Cell(row, 3).Value = p.PolicyType; ws.Cell(row, 4).Value = p.Provider;
                ws.Cell(row, 5).Value = p.CoverageDisplay; ws.Cell(row, 6).Value = p.ExpiryDate;
                ws.Cell(row, 7).Value = p.PolicyStatus;
                if (row % 2 == 0) ws.Range(row, 1, row, 7).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#F0F4F8");
                row++;
            }
            ws.Columns().AdjustToContents();
            wb.SaveAs(path);
            ExportHelper.OfferOpen(path);
        }

        private void ExportCsv(string path)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(ExportHelper.CsvLine("POLICY NO", "POLICY NAME", "TYPE", "PROVIDER", "COVERAGE", "EXPIRY DATE", "STATUS"));
            foreach (var p in _allPolicies)
                sb.AppendLine(ExportHelper.CsvLine(p.PolicyNo, p.PolicyName, p.PolicyType,
                    p.Provider, p.CoverageDisplay, p.ExpiryDate, p.PolicyStatus));
            System.IO.File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
            ExportHelper.OfferOpen(path);
        }

        private Policy GetRow(object sender)
        {
            var btn = sender as Button;
            if (btn != null)
            {
                var row = FindParent<DataGridRow>(btn);
                if (row != null) return row.Item as Policy;
            }
            return PoliciesGrid.SelectedItem as Policy;
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
