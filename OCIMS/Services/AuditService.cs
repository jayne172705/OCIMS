using System;
using System.Text.Json;
using System.Threading.Tasks;
using eSureHi.Data;
using eSureHi.Models;

namespace eSureHi.Services
{
    public static class AuditService
    {
        public static async Task LogAsync(
            string action,
            string tableName,
            int recordId,
            string description)
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();

                // new_values column requires valid JSON
                var jsonValue = JsonSerializer.Serialize(
                    new { description });

                db.AuditLogs.Add(new AuditLog
                {
                    UserId = AuthService.Instance.CurrentUser?.UserId,
                    Action = action,
                    TableName = tableName,
                    RecordId = recordId,
                    NewValues = jsonValue,
                    LoggedAt = DateTime.Now
                });
                await db.SaveChangesAsync();
            }
            catch { /* Never crash the app over a log failure */ }
        }

        public static Task LogInsert(string table, int id, string desc)
            => LogAsync("INSERT", table, id, desc);

        public static Task LogUpdate(string table, int id, string desc)
            => LogAsync("UPDATE", table, id, desc);

        public static Task LogDelete(string table, int id, string desc)
            => LogAsync("DELETE", table, id, desc);
    }
}
