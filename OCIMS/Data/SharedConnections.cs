using System.IO;
using System;
using MySqlConnector;

namespace eSureHi.Data
{
    /// <summary>
    /// Read-only stubs for GGMS and CRS cross-system databases.
    /// Do NOT activate until Steps 1–23 are complete.
    /// </summary>
    public class SharedDatabaseConfiguration
    {
        public string Server { get; set; } = string.Empty;
        public string Port { get; set; } = "3306";
        public string Database { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public static SharedDatabaseConfiguration LoadGgms() =>
            LoadFrom("GgmsConfig.txt");

        public static SharedDatabaseConfiguration LoadCrs() =>
            LoadFrom("CrsConfig.txt");

        public static void SaveGgms(SharedDatabaseConfiguration config) =>
            config.SaveTo("GgmsConfig.txt");

        public static void SaveCrs(SharedDatabaseConfiguration config) =>
            config.SaveTo("CrsConfig.txt");

        private static SharedDatabaseConfiguration LoadFrom(string fileName)
        {
            var config = new SharedDatabaseConfiguration();

            var path = GetConfigPath(fileName);

            if (!File.Exists(path)) return config;

            foreach (var line in File.ReadAllLines(path))
            {
                var parts = line.Split('=', 2);
                if (parts.Length != 2) continue;
                var key = parts[0].Trim();
                var value = parts[1].Trim();
                switch (key)
                {
                    case "Server": config.Server = value; break;
                    case "Port": config.Port = value; break;
                    case "Database": config.Database = value; break;
                    case "User": config.User = value; break;
                    case "Password": config.Password = value; break;
                }
            }
            return config;
        }

        public void SaveTo(string fileName)
        {
            var path = GetConfigPath(fileName);
            File.WriteAllLines(path, new[]
            {
                $"Server={Server}",
                $"Port={Port}",
                $"Database={Database}",
                $"User={User}",
                $"Password={Password}"
            });
        }

        public bool IsConfigured =>
            !LooksLikePlaceholder(Server) &&
            !LooksLikePlaceholder(Database) &&
            !LooksLikePlaceholder(User) &&
            !LooksLikePlaceholder(Password);

        private static string GetConfigPath(string fileName) =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

        private static bool LooksLikePlaceholder(string? value)
        {
            var trimmed = value?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(trimmed) ||
                   trimmed.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
        }

        private string GetNormalizedServer()
        {
            var server = Server?.Trim() ?? string.Empty;
            return string.Equals(server, "localhost", StringComparison.OrdinalIgnoreCase)
                ? "127.0.0.1"
                : server;
        }

        // Connection Timeout=5 for cross-system connections per spec
        public string ToConnectionString()
        {
            if (!IsConfigured)
                throw new InvalidOperationException(
                    "External system configuration is incomplete. Please save valid connection settings first.");

            var port = uint.TryParse(Port, out var parsedPort) && parsedPort > 0 && parsedPort <= 65535
                ? parsedPort
                : 3306;

            var builder = new MySqlConnectionStringBuilder
            {
                Server = GetNormalizedServer(),
                Port = port,
                Database = Database,
                UserID = User,
                Password = Password,
                ConnectionTimeout = 5,
                Pooling = true,
                MinimumPoolSize = 0,
                MaximumPoolSize = 5,
                ConnectionIdleTimeout = 30,
                SslMode = MySqlSslMode.None,
                AllowZeroDateTime = true,
                ConvertZeroDateTime = true
            };

            return builder.ConnectionString;
        }
    }
}
