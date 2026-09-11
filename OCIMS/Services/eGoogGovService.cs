using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using eSureHi.Models;
using eSureHi.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

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
                
                // Open local CSV copy
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = saveDialog.FileName,
                    UseShellExecute = true
                });

                // Attempt to upload to GGMS database
                var uploadSuccess = await UploadToGgmsDatabaseAsync(transactions);
                if (uploadSuccess)
                {
                    System.Windows.MessageBox.Show(
                        "Distributed Transactions successfully exported to CSV and uploaded to GGMS Database.",
                        "Success",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        "Distributed Transactions successfully exported to CSV. However, GGMS database is offline or not configured; transactions have been queued locally and will sync when you next press Sync.",
                        "Offline / Queued",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> UploadToGgmsDatabaseAsync(IEnumerable<DocumentTransaction> transactions)
        {
            var ggmsConfig = SharedDatabaseConfiguration.LoadGgms();
            if (!ggmsConfig.IsConfigured)
            {
                await QueueLocallyAsync(transactions, "GGMS database connection is not configured.");
                return false;
            }

            try
            {
                var connStr = ggmsConfig.ToConnectionString();
                using var conn = new MySqlConnection(connStr);
                await conn.OpenAsync();

                foreach (var t in transactions)
                {
                    // Check if already exists in consolidated_transactions to prevent duplicates
                    using (var checkCmd = new MySqlCommand(
                        "SELECT COUNT(*) FROM consolidated_transactions WHERE project_code = @code", conn))
                    {
                        checkCmd.Parameters.AddWithValue("@code", t.TransactionNo);
                        var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
                        if (exists) continue; // Skip duplicate
                    }

                    // Insert into consolidated_transactions (amount = 0.00 since it is document transaction)
                    using (var cmd = new MySqlCommand(@"
                        INSERT INTO consolidated_transactions
                            (beneficiary_id, civil_registry_id, project_code, project_name,
                             office_id, full_name, first_name, middle_name, last_name,
                             office_name, transaction_type, amount, transaction_date, status)
                        VALUES
                            (NULL, NULL, @project_code, @project_name,
                             @office_id, @full_name, '', '', '',
                             @office_name, @transaction_type, 0.00, @transaction_date, @status)", conn))
                    {
                        cmd.Parameters.AddWithValue("@project_code", t.TransactionNo);
                        cmd.Parameters.AddWithValue("@project_name", t.Subject);
                        cmd.Parameters.AddWithValue("@office_id", "OFF-2026-0004"); // Sulop IMS office code
                        cmd.Parameters.AddWithValue("@full_name", t.Sender?.SenderName ?? "System");
                        cmd.Parameters.AddWithValue("@office_name", "Insurance Management System");
                        cmd.Parameters.AddWithValue("@transaction_type", t.TransactionType);
                        cmd.Parameters.AddWithValue("@transaction_date", t.TransactionDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@status", t.Status);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                await QueueLocallyAsync(transactions, ex.Message);
                return false;
            }
        }

        private static async Task QueueLocallyAsync(IEnumerable<DocumentTransaction> transactions, string errorReason)
        {
            try
            {
                await using var localDb = eSureHiDbContextFactory.CreateLocal();
                foreach (var t in transactions)
                {
                    // Check if already queued to avoid local duplicates
                    var exists = await localDb.GgmsQueueItems
                        .AnyAsync(q => q.ProjectCode == t.TransactionNo);
                    if (exists) continue;

                    var queueItem = new GgmsQueueItem
                    {
                        ProjectCode = t.TransactionNo,
                        ProjectName = t.Subject,
                        BeneficiaryId = null,
                        CivilRegistryId = null,
                        FirstName = string.Empty,
                        MiddleName = null,
                        LastName = string.Empty,
                        FullName = t.Sender?.SenderName ?? "System",
                        TransactionType = t.TransactionType,
                        Amount = 0,
                        TransactionDate = t.TransactionDate.HasValue 
                            ? new DateTime(t.TransactionDate.Value.Year, t.TransactionDate.Value.Month, t.TransactionDate.Value.Day) 
                            : DateTime.Today,
                        Status = "Pending",
                        ErrorMessage = $"Queued from eGoogGOV upload: {errorReason}",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    localDb.GgmsQueueItems.Add(queueItem);
                }
                await localDb.SaveChangesAsync();
            }
            catch (Exception dbEx)
            {
                System.Diagnostics.Debug.WriteLine("Failed to queue document transactions locally: " + dbEx.Message);
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
