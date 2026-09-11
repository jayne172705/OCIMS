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

        public static string LocalDatabasePath
        {
            get
            {
                var configuredPath = Environment.GetEnvironmentVariable("ESUREHI_LOCAL_DATABASE_PATH");
                return string.IsNullOrWhiteSpace(configuredPath)
                    ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ims.db")
                    : Path.GetFullPath(configuredPath);
            }
        }

        public static string GetLocalConnectionString() =>
            $"Data Source={LocalDatabasePath}";

        public static string GetConnectionString()
        {
            var config = DatabaseConfiguration.Load();
            return config.ToConnectionString();
        }

        public static eSureHiDbContext Create()
        {
            return App.DbConfig.UsesRemoteDatabase ? CreateCloud() : CreateLocal();
        }

        /// <summary>
        /// Creates a context suitable for an explicit database transaction. MySQL's
        /// retry strategy cannot be combined with a user-started transaction, so
        /// remote callers needing BeginTransactionAsync use this context instead.
        /// </summary>
        public static eSureHiDbContext CreateWithoutRetry()
        {
            return App.DbConfig.UsesRemoteDatabase ? CreateCloudWithoutRetry() : CreateLocal();
        }

        /// <summary>
        /// Creates a context for the on-device SQLite working copy. Sync and offline
        /// support must use this explicitly, regardless of the selected main source.
        /// </summary>
        public static eSureHiDbContext CreateLocal()
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

        public static eSureHiDbContext CreateCloudWithoutRetry()
        {
            return CreateCloudWithoutRetry(App.DbConfig);
        }

        public static eSureHiDbContext CreateCloud(DatabaseConfiguration config)
        {
            var connStr = config.ToConnectionString();

            var options = new DbContextOptionsBuilder<eSureHiDbContext>()
                .UseMySql(connStr, CloudServerVersion, mySqlOptions => mySqlOptions.EnableRetryOnFailure())
                .Options;

            return new eSureHiDbContext(options);
        }

        public static eSureHiDbContext CreateCloudWithoutRetry(DatabaseConfiguration config)
        {
            var options = new DbContextOptionsBuilder<eSureHiDbContext>()
                .UseMySql(config.ToConnectionString(), CloudServerVersion)
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
