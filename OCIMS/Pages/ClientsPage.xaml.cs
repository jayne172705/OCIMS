using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OCIMS.Data;
using OCIMS.Models;

namespace OCIMS.Pages
{
    public partial class ClientsPage : Page
    {
        private EmployeeRepository _repo = new EmployeeRepository();
        private List<Employee> _allClients = new List<Employee>();

        public ClientsPage()
        {
            InitializeComponent();
            LoadClients();
        }

        // ── LOAD ─────────────────────────────────────────────
        private void LoadClients()
        {
            _allClients = _repo.GetAll();
            ClientsGrid.ItemsSource = null;
            ClientsGrid.ItemsSource = _allClients;
            TotalCount.Text = "Total: " + _allClients.Count + " clients";
            SelectedInfo.Text = "";
        }

        // ── SEARCH ───────────────────────────────────────────
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string keyword = SearchBox.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(keyword))
            {
                ClientsGrid.ItemsSource = null;
                ClientsGrid.ItemsSource = _allClients;
                TotalCount.Text = "Total: " + _allClients.Count + " clients";
                return;
            }

            var filtered = _allClients.Where(c =>
                (!string.IsNullOrEmpty(c.FullName) && c.FullName.ToLower().Contains(keyword)) ||
                (!string.IsNullOrEmpty(c.Email) && c.Email.ToLower().Contains(keyword)) ||
                (!string.IsNullOrEmpty(c.PhoneMobile) && c.PhoneMobile.ToLower().Contains(keyword)) ||
                (!string.IsNullOrEmpty(c.Address) && c.Address.ToLower().Contains(keyword)) ||
                (!string.IsNullOrEmpty(c.EmployeeNo) && c.EmployeeNo.ToLower().Contains(keyword))
            ).ToList();

            ClientsGrid.ItemsSource = null;
            ClientsGrid.ItemsSource = filtered;
            TotalCount.Text = "Showing " + filtered.Count + " of " + _allClients.Count + " clients";
        }

        // ── SELECTION ────────────────────────────────────────
        private void ClientsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClientsGrid.SelectedItem is Employee emp)
                SelectedInfo.Text = "Selected: " + emp.FullName;
            else
                SelectedInfo.Text = "";
        }

        // ── ADD ──────────────────────────────────────────────
        private void AddClient_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddEmployeeWindow();
            dialog.Owner = Window.GetWindow(this);
            dialog.ShowDialog();
            if (dialog.IsSaved)
                LoadClients();
        }

        // ── VIEW ─────────────────────────────────────────────
        private void ViewClient_Click(object sender, RoutedEventArgs e)
        {
            var emp = GetRowClient(sender);
            if (emp == null) return;

            MessageBox.Show(
                "CLIENT ID : " + emp.EmployeeNo + "\n" +
                "NAME      : " + emp.FullName + "\n" +
                "EMAIL     : " + emp.Email + "\n" +
                "PHONE     : " + emp.PhoneMobile + "\n" +
                "ADDRESS   : " + (emp.Address ?? "-") + "\n" +
                "STATUS    : " + emp.EmploymentStatus,
                "Client Details — " + emp.FullName,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ── EDIT ─────────────────────────────────────────────
        private void EditClient_Click(object sender, RoutedEventArgs e)
        {
            var emp = GetRowClient(sender);
            if (emp == null) return;

            var dialog = new EditClientWindow(emp);
            dialog.Owner = Window.GetWindow(this);
            dialog.ShowDialog();
            if (dialog.IsSaved)
                LoadClients();
        }

        // ── DELETE ───────────────────────────────────────────
        private void DeleteClient_Click(object sender, RoutedEventArgs e)
        {
            var emp = GetRowClient(sender);
            if (emp == null) return;

            var result = MessageBox.Show(
                "Are you sure you want to delete:\n\n" +
                "Name: " + emp.FullName + "\n" +
                "ID:   " + emp.EmployeeNo + "\n\n" +
                "This action cannot be undone!",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                bool deleted = _repo.Delete(emp.EmployeeNo);
                if (deleted)
                {
                    MessageBox.Show(
                        "✔ Client '" + emp.FullName + "' deleted successfully.",
                        "OCIMS", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadClients();
                }
            }
        }

        // ── EXPORT ───────────────────────────────────────────
        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Clients",
                    Filter = "Excel File (*.xlsx)|*.xlsx|CSV File (*.csv)|*.csv",
                    FileName = "Clients_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
                };

                if (saveDialog.ShowDialog() != true) return;

                string path = saveDialog.FileName;

                if (path.EndsWith(".xlsx"))
                    ExportToExcel(path);
                else
                    ExportToCsv(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Export error: " + ex.Message,
                    "OCIMS", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToExcel(string path)
        {
            var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("Clients");

            // Headers
            ws.Cell(1, 1).Value = "CLIENT ID";
            ws.Cell(1, 2).Value = "FULL NAME";
            ws.Cell(1, 3).Value = "EMAIL";
            ws.Cell(1, 4).Value = "PHONE";
            ws.Cell(1, 5).Value = "ADDRESS";
            ws.Cell(1, 6).Value = "STATUS";

            var hdr = ws.Range("A1:F1");
            hdr.Style.Font.Bold = true;
            hdr.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            hdr.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#2E86DE");
            hdr.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;

            // Data
            int row = 2;
            foreach (var emp in _allClients)
            {
                ws.Cell(row, 1).Value = emp.EmployeeNo;
                ws.Cell(row, 2).Value = emp.FullName;
                ws.Cell(row, 3).Value = emp.Email;
                ws.Cell(row, 4).Value = emp.PhoneMobile;
                ws.Cell(row, 5).Value = emp.Address ?? "";
                ws.Cell(row, 6).Value = emp.EmploymentStatus;

                if (row % 2 == 0)
                    ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor =
                        ClosedXML.Excel.XLColor.FromHtml("#F0F4F8");
                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(path);

            MessageBox.Show(
                "✔ Exported " + (row - 2) + " clients to Excel!\n\n" + path,
                "Export Success", MessageBoxButton.OK, MessageBoxImage.Information);

            System.Diagnostics.Process.Start(path);
        }

        private void ExportToCsv(string path)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("CLIENT ID,FULL NAME,EMAIL,PHONE,ADDRESS,STATUS");

            foreach (var emp in _allClients)
            {
                sb.AppendLine(
                    "\"" + emp.EmployeeNo + "\"," +
                    "\"" + emp.FullName + "\"," +
                    "\"" + emp.Email + "\"," +
                    "\"" + emp.PhoneMobile + "\"," +
                    "\"" + (emp.Address ?? "") + "\"," +
                    "\"" + emp.EmploymentStatus + "\"");
            }

            System.IO.File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);

            MessageBox.Show(
                "✔ Exported " + _allClients.Count + " clients to CSV!\n\n" + path,
                "Export Success", MessageBoxButton.OK, MessageBoxImage.Information);

            System.Diagnostics.Process.Start(path);
        }

        // ── HELPER ───────────────────────────────────────────
        private Employee GetRowClient(object sender)
        {
            var btn = sender as Button;
            if (btn != null)
            {
                var row = FindParent<DataGridRow>(btn);
                if (row != null)
                    return row.Item as Employee;
            }
            return ClientsGrid.SelectedItem as Employee;
        }

        private static T FindParent<T>(System.Windows.DependencyObject child)
            where T : System.Windows.DependencyObject
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parent == null) return null;
            if (parent is T t) return t;
            return FindParent<T>(parent);
        }
    }
}
