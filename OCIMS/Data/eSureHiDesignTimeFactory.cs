using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System.IO;

namespace eSureHi.Data
{
    /// <summary>
    /// Allows EF Core CLI tools (dotnet ef) to instantiate eSureHiDbContext
    /// at design-time without needing the full WPF app to start.
    /// Reads eSureHiConfig.txt from the project root directory.
    /// </summary>
    public class eSureHiDesignTimeFactory : IDesignTimeDbContextFactory<eSureHiDbContext>
    {
        public eSureHiDbContext CreateDbContext(string[] args)
        {
            // Build connection string directly from eSureHiConfig.txt
            // Use project directory so dotnet ef can find the file
            var projectDir = FindProjectRoot();
            var configPath = Path.Combine(projectDir, "eSureHiConfig.txt");

            string server = "192.168.0.47", port = "3306", database = "ims_db",
                   user = "root", password = "network@2026";

            if (File.Exists(configPath))
            {
                foreach (var line in File.ReadAllLines(configPath))
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length != 2) continue;
                    switch (parts[0].Trim())
                    {
                        case "Server":   server   = parts[1].Trim(); break;
                        case "Port":     port     = parts[1].Trim(); break;
                        case "Database": database = parts[1].Trim(); break;
                        case "User":     user     = parts[1].Trim(); break;
                        case "Password": password = parts[1].Trim(); break;
                    }
                }
            }

            var connStr = $"Server={server};Port={port};Database={database};" +
                          $"User={user};Password={password};" +
                          $"Connection Timeout=10;SslMode=none;AllowZeroDateTime=True;ConvertZeroDateTime=True;";

            var optionsBuilder = new DbContextOptionsBuilder<eSureHiDbContext>();

            optionsBuilder.UseMySql(connStr, new MySqlServerVersion(new System.Version(8, 0, 31)));

            var options = optionsBuilder.Options;
            return new eSureHiDbContext(options);
        }

        private static string FindProjectRoot()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null)
            {
                if (dir.GetFiles("*.csproj").Length > 0)
                    return dir.FullName;
                dir = dir.Parent;
            }
            return System.AppContext.BaseDirectory;
        }
    }
}
