using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OCIMS.Pages
{
    public class FundSource
    {
        public int FundId { get; set; }
        public string FundName { get; set; }
        public string FundType { get; set; }
        public decimal Amount { get; set; }
        public string Source { get; set; }
        public string DateReceived { get; set; }
        public string FundStatus { get; set; } = "Active";
        public string Notes { get; set; }

        public string AmountDisplay
        {
            get { return "₱" + Amount.ToString("N2"); }
        }

        public string Icon
        {
            get
            {
                if (FundType == "Government") return "🏛";
                if (FundType == "Donation") return "🤝";
                if (FundType == "Grant") return "📜";
                if (FundType == "Budget") return "💼";
                if (FundType == "Premium") return "💰";
                return "💵";
            }
        }

        public string StatusBg
        {
            get
            {
                if (FundStatus == "Active") return "#E6F9F0";
                if (FundStatus == "Depleted") return "#FDEAEA";
                if (FundStatus == "On Hold") return "#FEF5E7";
                return "#F0F4F8";
            }
        }

        public string StatusFg
        {
            get
            {
                if (FundStatus == "Active") return "#1A8A4A";
                if (FundStatus == "Depleted") return "#C0392B";
                if (FundStatus == "On Hold") return "#D68910";
                return "#7A8FA6";
            }
        }
    }

    public partial class ResourcesPage : Page
    {
        private List<FundSource> _allFunds = new List<FundSource>();
        private int _nextId = 1;

        public ResourcesPage()
        {
            InitializeComponent();
            this.Loaded += PageLoaded;
        }

        private void PageLoaded(object sender, RoutedEventArgs e)
        {
            LoadFunds();
        }

        private void LoadFunds()
        {
            FundsGrid.ItemsSource = null;
            FundsGrid.ItemsSource = _allFunds;
            TotalCount.Text = "Total: " + _allFunds.Count + " fund sources";
            TxtTotalFunds.Text = _allFunds.Count.ToString();
            TxtActiveFunds.Text = _allFunds.Count(f => f.FundStatus == "Active").ToString();
            decimal total = 0;
            foreach (var f in _allFunds) total += f.Amount;
            TxtTotalAmount.Text = "₱" + total.ToString("N2");
            TotalAmountFooter.Text = "Total: ₱" + total.ToString("N2");
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string kw = SearchBox.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(kw))
            {
                FundsGrid.ItemsSource = null;
                FundsGrid.ItemsSource = _allFunds;
                TotalCount.Text = "Total: " + _allFunds.Count + " fund sources";
                return;
            }
            var f = _allFunds.Where(x =>
                (!string.IsNullOrEmpty(x.FundName) && x.FundName.ToLower().Contains(kw)) ||
                (!string.IsNullOrEmpty(x.FundType) && x.FundType.ToLower().Contains(kw)) ||
                (!string.IsNullOrEmpty(x.Source) && x.Source.ToLower().Contains(kw)) ||
                (!string.IsNullOrEmpty(x.FundStatus) && x.FundStatus.ToLower().Contains(kw))
            ).ToList();
            FundsGrid.ItemsSource = null;
            FundsGrid.ItemsSource = f;
            TotalCount.Text = "Showing " + f.Count + " of " + _allFunds.Count + " fund sources";
        }

        private void AddFund_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AddFundWindow();
            dlg.Owner = Window.GetWindow(this);
            dlg.ShowDialog();
            if (dlg.IsSaved && dlg.NewFund != null)
            {
                dlg.NewFund.FundId = _nextId++;
                _allFunds.Add(dlg.NewFund);
                LoadFunds();
            }
        }

        private void ViewFund_Click(object sender, RoutedEventArgs e)
        {
            var fund = GetRow(sender);
            if (fund == null) return;
            MessageBox.Show(
                "FUND NAME    : " + fund.FundName + "\n" +
                "TYPE         : " + fund.FundType + "\n" +
                "AMOUNT       : " + fund.AmountDisplay + "\n" +
                "SOURCE/DONOR : " + fund.Source + "\n" +
                "DATE RECEIVED: " + fund.DateReceived + "\n" +
                "STATUS       : " + fund.FundStatus + "\n" +
                "NOTES        : " + (fund.Notes ?? "-"),
                "Fund Source Details — " + fund.FundName,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void EditFund_Click(object sender, RoutedEventArgs e)
        {
            var fund = GetRow(sender);
            if (fund == null) return;
            var dlg = new AddFundWindow(fund);
            dlg.Owner = Window.GetWindow(this);
            dlg.ShowDialog();
            if (dlg.IsSaved) LoadFunds();
        }

        private void DeleteFund_Click(object sender, RoutedEventArgs e)
        {
            var fund = GetRow(sender);
            if (fund == null) return;
            var r = MessageBox.Show(
                "Delete fund source '" + fund.FundName + "'?\n\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r == MessageBoxResult.Yes)
            {
                _allFunds.Remove(fund);
                LoadFunds();
                MessageBox.Show("✔ Fund source deleted.", "OCIMS",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Fund Sources",
                    Filter = "Excel File (*.xlsx)|*.xlsx|CSV File (*.csv)|*.csv",
                    FileName = "FundSources_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
                };
                if (dlg.ShowDialog() != true) return;
                if (dlg.FileName.EndsWith(".xlsx")) ExportExcel(dlg.FileName);
                else ExportCsv(dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Export error: " + ex.Message, "OCIMS",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportExcel(string path)
        {
            var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("Fund Sources");
            ws.Cell(1, 1).Value = "FUND NAME"; ws.Cell(1, 2).Value = "TYPE";
            ws.Cell(1, 3).Value = "AMOUNT"; ws.Cell(1, 4).Value = "SOURCE/DONOR";
            ws.Cell(1, 5).Value = "DATE RECEIVED"; ws.Cell(1, 6).Value = "STATUS";
            var hdr = ws.Range("A1:F1");
            hdr.Style.Font.Bold = true;
            hdr.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            hdr.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#2E86DE");
            int row = 2;
            foreach (var f in _allFunds)
            {
                ws.Cell(row, 1).Value = f.FundName; ws.Cell(row, 2).Value = f.FundType;
                ws.Cell(row, 3).Value = f.AmountDisplay; ws.Cell(row, 4).Value = f.Source;
                ws.Cell(row, 5).Value = f.DateReceived; ws.Cell(row, 6).Value = f.FundStatus;
                if (row % 2 == 0) ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#F0F4F8");
                row++;
            }
            ws.Columns().AdjustToContents();
            wb.SaveAs(path);
            MessageBox.Show("✔ Exported " + (row - 2) + " fund sources!\n\n" + path,
                "Export Success", MessageBoxButton.OK, MessageBoxImage.Information);
            System.Diagnostics.Process.Start(path);
        }

        private void ExportCsv(string path)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("FUND NAME,TYPE,AMOUNT,SOURCE/DONOR,DATE RECEIVED,STATUS");
            foreach (var f in _allFunds)
                sb.AppendLine("\"" + f.FundName + "\",\"" + f.FundType + "\",\"" + f.AmountDisplay + "\",\"" +
                              f.Source + "\",\"" + f.DateReceived + "\",\"" + f.FundStatus + "\"");
            System.IO.File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
            MessageBox.Show("✔ Exported!\n\n" + path, "Export Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
            System.Diagnostics.Process.Start(path);
        }

        private FundSource GetRow(object sender)
        {
            var btn = sender as Button;
            if (btn != null)
            {
                var row = FindParent<DataGridRow>(btn);
                if (row != null) return row.Item as FundSource;
            }
            return FundsGrid.SelectedItem as FundSource;
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