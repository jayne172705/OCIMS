using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using eSureHi.Models;

namespace eSureHi.Services
{
    public static class eGoogGovService
    {
        public static async Task<bool> ExportConsolidatedTransactionAsync(IEnumerable<DocumentTransaction> transactions)
        {
            var saveDialog = new SaveFileDialog
            {
                FileName = $"Consolidated_Transaction_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                DefaultExt = ".csv",
                Filter = "CSV (Comma delimited)|*.csv"
            };

            if (saveDialog.ShowDialog() != true) return false;

            try
            {
                var sb = new StringBuilder();
                // Headers for the consolidated transaction
                sb.AppendLine("TransactionNo,Type,Subject,Priority,Status,Sender,Receiver,CreatedAt");

                foreach (var t in transactions)
                {
                    var sender = Escape(t.Sender?.SenderName);
                    var receiver = Escape(t.Receiver?.ReceiverName);
                    var subject = Escape(t.Subject);

                    sb.AppendLine($"{t.TransactionNo},{t.TransactionType},{subject},{t.Priority},{t.Status},{sender},{receiver},{t.CreatedAt:yyyy-MM-dd HH:mm}");
                }

                await File.WriteAllTextAsync(saveDialog.FileName, sb.ToString(), Encoding.UTF8);
                
                // Simulate success and open
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = saveDialog.FileName,
                    UseShellExecute = true
                });

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string Escape(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            if (value.Contains(",") || value.Contains("\""))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }
    }
}
