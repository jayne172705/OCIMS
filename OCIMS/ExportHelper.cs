using System;
using System.Text;
using System.Windows;

namespace OCIMS
{
    /// <summary>
    /// Shared export utilities used by every page's Export button, so CSV
    /// escaping and file handling behave the same everywhere.
    /// </summary>
    public static class ExportHelper
    {
        /// <summary>True when the chosen file name is an .xlsx workbook (case-insensitive).</summary>
        public static bool IsXlsx(string path)
        {
            return path != null && path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// RFC 4180 CSV field escaping: doubles embedded quotes, wraps fields
        /// containing separators/quotes/newlines, and prefixes values starting
        /// with =, +, -, or @ so spreadsheet apps don't run them as formulas.
        /// </summary>
        public static string CsvField(string value)
        {
            if (value == null) return "\"\"";

            if (value.Length > 0 && (value[0] == '=' || value[0] == '+' || value[0] == '-' || value[0] == '@'))
                value = "'" + value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>Builds one CSV line from the given field values.</summary>
        public static string CsvLine(params string[] fields)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(CsvField(fields[i]));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Asks the user whether to open the exported file, and only opens
        /// known spreadsheet extensions — never scripts or executables.
        /// </summary>
        public static void OfferOpen(string path)
        {
            bool safe = IsXlsx(path) || path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
            if (!safe) return;

            var answer = MessageBox.Show("Export complete!\n\n" + path + "\n\nOpen the file now?",
                "OCIMS", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (answer != MessageBoxResult.Yes) return;

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open the file: " + ex.Message, "OCIMS",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
