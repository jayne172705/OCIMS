using Microsoft.EntityFrameworkCore;
using System;

namespace eSureHi.Data
{
    public static class GgmsDbContextFactory
    {
        private static readonly MySqlServerVersion ServerVersion =
            new(new Version(8, 0, 44));

        public static GgmsDbContext Create()
        {
            var config = SharedDatabaseConfiguration.LoadGgms();
            var connStr = config.ToConnectionString();

            var options = new DbContextOptionsBuilder<GgmsDbContext>()
                .UseMySql(connStr, ServerVersion,
                    o =>
                    {
                        o.CommandTimeout(60);
                        o.EnableRetryOnFailure();
                    })
                .Options;
            return new GgmsDbContext(options);
        }

        public static string GetConnectionString()
        {
            var config = SharedDatabaseConfiguration.LoadGgms();
            return config.ToConnectionString();
        }
    }
}
