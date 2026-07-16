using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OCIMS.Data;
using OCIMS.Models;

namespace OCIMS.Pages
{
    public partial class DocumentsPage : Page
    {
        private DocumentRepository _repo = new DocumentRepository();
        private List<Document> _allDocs = new List<Document>();
        private List<DocumentType> _docTypes = new List<DocumentType>();

        public DocumentsPage()
        {
            InitializeComponent();
            this.Loaded += PageLoaded;
        }

        private void PageLoaded(object sender, RoutedEventArgs e)
        {
            LoadDocumentTypes();
            LoadDocuments();
        }

        private void LoadDocumentTypes()
        {
            _docTypes = _repo.GetDocumentTypes();
            TxtTypes.Text = _docTypes.Count.ToString();

            FilterType.Items.Clear();
            FilterType.Items.Add(new ComboBoxItem { Content = "All Types", Tag = 0 });
            foreach (var t in _docTypes)
                FilterType.Items.Add(new ComboBoxItem { Content = t.TypeName, Tag = t.DocTypeId });
            FilterType.SelectedIndex = 0;
        }

        private void LoadDocuments()
        {
            _allDocs = _repo.GetAll();
            ApplyFilter();
            UpdateStats();
        }

        private void UpdateStats()
        {
            TxtTotal.Text = _allDocs.Count.ToString();
            var uniqueClients = new System.Collections.Generic.HashSet<int>();
            foreach (var d in _allDocs) uniqueClients.Add(d.EmpId);
            TxtClients.Text = uniqueClients.Count.ToString();
        }

        private void ApplyFilter()
        {
            string kw = SearchBox != null ? SearchBox.Text.Trim().ToLower() : "";

            int typeId = 0;
            if (FilterType != null && FilterType.SelectedItem != null)
            {
                var sel = FilterType.SelectedItem as ComboBoxItem;
                if (sel != null) typeId = (int)sel.Tag;
            }

            var filtered = _allDocs.AsEnumerable();

            if (typeId > 0)
                filtered = filtered.Where(d => d.DocTypeId == typeId);

            if (!string.IsNullOrEmpty(kw))
                filtered = filtered.Where(d =>
                    (!string.IsNullOrEmpty(d.DocTitle) && d.DocTitle.ToLower().Contains(kw)) ||
                    (!string.IsNullOrEmpty(d.ClientName) && d.ClientName.ToLower().Contains(kw)) ||
                    (!string.IsNullOrEmpty(d.ClientId) && d.ClientId.ToLower().Contains(kw)) ||
                    (!string.IsNullOrEmpty(d.DocTypeName) && d.DocTypeName.ToLower().Contains(kw)));

            var result = filtered.ToList();
            DocsGrid.ItemsSource = null;
            DocsGrid.ItemsSource = result;
            TotalCount.Text = "Showing: " + result.Count + " documents";
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void FilterType_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void AddDocument_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AddDocumentWindow(_docTypes);
            dlg.Owner = Window.GetWindow(this);
            dlg.ShowDialog();
            if (dlg.IsSaved) LoadDocuments();
        }

        private void ViewDoc_Click(object sender, RoutedEventArgs e)
        {
            var doc = GetRow(sender);
            if (doc == null) return;
            MessageBox.Show(
                "TITLE        : " + doc.DocTitle + "\n" +
                "TYPE         : " + doc.DocTypeName + "\n" +
                "CLIENT       : " + doc.ClientName + "\n" +
                "CLIENT ID    : " + doc.ClientId + "\n" +
                "FILE         : " + (doc.FileName ?? "-") + "\n" +
                "FILE SIZE    : " + (doc.FileSize ?? "-") + "\n" +
                "DATE UPLOADED: " + doc.DateUploaded + "\n" +
                "REMARKS      : " + (doc.Remarks ?? "-"),
                "Document Details — " + doc.DocTitle,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static readonly string[] BlockedExtensions =
            { ".exe", ".bat", ".cmd", ".com", ".scr", ".ps1", ".vbs", ".js", ".lnk", ".msi" };

        private void OpenDoc_Click(object sender, RoutedEventArgs e)
        {
            var doc = GetRow(sender);
            if (doc == null) return;

            if (string.IsNullOrEmpty(doc.FilePath) || !System.IO.File.Exists(doc.FilePath))
            {
                MessageBox.Show("File not found or no file attached.",
                    "OCIMS", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string ext = System.IO.Path.GetExtension(doc.FilePath);
            if (BlockedExtensions.Any(b => string.Equals(b, ext, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Cannot open executable files from here.",
                    "OCIMS", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(doc.FilePath)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open the file: " + ex.Message,
                    "OCIMS", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteDoc_Click(object sender, RoutedEventArgs e)
        {
            var doc = GetRow(sender);
            if (doc == null) return;
            var r = MessageBox.Show(
                "Delete document '" + doc.DocTitle + "'?\n\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r == MessageBoxResult.Yes)
            {
                if (_repo.Delete(doc.DocumentId))
                {
                    MessageBox.Show("✔ Document deleted.", "OCIMS",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadDocuments();
                }
            }
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Documents",
                    Filter = "Excel File (*.xlsx)|*.xlsx|CSV File (*.csv)|*.csv",
                    FileName = "Documents_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
                };
                if (dlg.ShowDialog() != true) return;
                var data = DocsGrid.ItemsSource as List<Document>;
                if (data == null) return;
                if (ExportHelper.IsXlsx(dlg.FileName)) ExportExcel(dlg.FileName, data);
                else ExportCsv(dlg.FileName, data);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Export error: " + ex.Message, "OCIMS",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportExcel(string path, List<Document> data)
        {
            var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("Documents");
            ws.Cell(1, 1).Value = "TITLE"; ws.Cell(1, 2).Value = "TYPE";
            ws.Cell(1, 3).Value = "CLIENT"; ws.Cell(1, 4).Value = "CLIENT ID";
            ws.Cell(1, 5).Value = "FILE NAME"; ws.Cell(1, 6).Value = "DATE UPLOADED";
            var hdr = ws.Range("A1:F1");
            hdr.Style.Font.Bold = true;
            hdr.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            hdr.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#2E86DE");
            int row = 2;
            foreach (var d in data)
            {
                ws.Cell(row, 1).Value = d.DocTitle; ws.Cell(row, 2).Value = d.DocTypeName;
                ws.Cell(row, 3).Value = d.ClientName; ws.Cell(row, 4).Value = d.ClientId;
                ws.Cell(row, 5).Value = d.FileName ?? ""; ws.Cell(row, 6).Value = d.DateUploaded;
                if (row % 2 == 0) ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#F0F4F8");
                row++;
            }
            ws.Columns().AdjustToContents();
            wb.SaveAs(path);
            ExportHelper.OfferOpen(path);
        }

        private void ExportCsv(string path, List<Document> data)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(ExportHelper.CsvLine("TITLE", "TYPE", "CLIENT", "CLIENT ID", "FILE NAME", "DATE UPLOADED"));
            foreach (var d in data)
                sb.AppendLine(ExportHelper.CsvLine(d.DocTitle, d.DocTypeName, d.ClientName,
                    d.ClientId, d.FileName ?? "", d.DateUploaded));
            System.IO.File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
            ExportHelper.OfferOpen(path);
        }

        private Document GetRow(object sender)
        {
            var btn = sender as Button;
            if (btn != null)
            {
                var row = FindParent<DataGridRow>(btn);
                if (row != null) return row.Item as Document;
            }
            return DocsGrid.SelectedItem as Document;
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