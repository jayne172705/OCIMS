using Microsoft.EntityFrameworkCore;
using eSureHi.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace eSureHi.Data
{
    public static class eSureHiDbContextFactory
    {
        private static readonly MySqlServerVersion CloudServerVersion =
            new(new Version(8, 0, 31));

        public static string LocalDatabasePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ims.db");

        public static string GetLocalConnectionString() =>
            $"Data Source={LocalDatabasePath}";

        public static string GetConnectionString()
        {
            var config = DatabaseConfiguration.Load();
            return config.ToConnectionString();
        }

        public static eSureHiDbContext Create()
        {
            var options = new DbContextOptionsBuilder<eSureHiDbContext>()
                .UseSqlite(GetLocalConnectionString())
                .Options;

            return new eSureHiDbContext(options);
        }

        public static eSureHiDbContext Create(DatabaseConfiguration config)
        {
            return CreateCloud(config);
        }

        public static eSureHiDbContext CreateCloud()
        {
            return CreateCloud(App.DbConfig);
        }

        public static eSureHiDbContext CreateCloud(DatabaseConfiguration config)
        {
            var connStr = config.ToConnectionString();

            var options = new DbContextOptionsBuilder<eSureHiDbContext>()
                .UseMySql(connStr, CloudServerVersion)
                .Options;

            return new eSureHiDbContext(options);
        }

        public static async Task<bool> CanConnectAsync()
        {
            try
            {
                if (!App.DbConfig.IsConfigured)
                    return false;

                using var db = CreateCloud();
                return await db.Database.CanConnectAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Database Connection Error: " + ex.ToString());
                return false;
            }
        }
    }
}
