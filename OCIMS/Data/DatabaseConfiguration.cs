using System;
using System.IO;
using MySqlConnector;

namespace eSureHi.Data
{
    public enum MainDatabaseMode
    {
        Local,
        Network,
        Online
    }

    public class DatabaseConfiguration
    {
        public string Server   { get; set; } = "192.168.0.47";
        public int    Port     { get; set; } = 3306;
        public string Database { get; set; } = "ims_db";
        public string User     { get; set; } = "root";
        public string Password { get; set; } = "network@2026";
        // The main application data source selected from the login connection dialog.
        // Older configuration files do not contain this setting, so they continue to
        // use the original SQLite-first behavior.
        public MainDatabaseMode ActiveMode { get; set; } = MainDatabaseMode.Local;

        private static readonly string ConfigPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "eSureHiConfig.txt");

        public static DatabaseConfiguration Load()
        {
            var config = new DatabaseConfiguration();

            if (!File.Exists(ConfigPath))
            {
                try   { config.Save(); }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
                return config;
            }

            foreach (var line in File.ReadAllLines(ConfigPath))
            {
                var parts = line.Split('=', 2);
                if (parts.Length != 2) continue;

                var key   = parts[0].Trim();
                var value = parts[1].Trim();

                switch (key)
                {
                    case "Server":   config.Server   = value; break;
                    case "Port":
                        if (int.TryParse(value, out int port) && port > 0 && port <= 65535)
                            config.Port = port;
                        break;
                    case "Database": config.Database = value; break;
                    case "User":     config.User     = value; break;
                    case "Password": config.Password = value; break;
                    case "ActiveMode":
                        if (Enum.TryParse<MainDatabaseMode>(value, true, out var mode))
                            config.ActiveMode = mode;
                        break;
                }
            }

            return config;
        }

        public void Save()
        {
            File.WriteAllLines(ConfigPath, new[]
            {
                $"Server={Server}",
                $"Port={Port}",
                $"Database={Database}",
                $"User={User}",
                $"Password={Password}",
                $"ActiveMode={ActiveMode}"
            });
        }

        public bool UsesRemoteDatabase =>
            ActiveMode is MainDatabaseMode.Network or MainDatabaseMode.Online;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Server)   &&
            !string.IsNullOrWhiteSpace(Database) &&
            !string.IsNullOrWhiteSpace(User)     &&
            !string.IsNullOrWhiteSpace(Password);

        public string GetNormalizedServer()
        {
            var server = Server?.Trim() ?? string.Empty;
            return string.Equals(server, "localhost", StringComparison.OrdinalIgnoreCase)
                ? "127.0.0.1"
                : server;
        }

        public string ToConnectionString()
        {
            if (!IsConfigured)
                throw new InvalidOperationException(
                    "Database configuration is incomplete. " +
                    "Please set your connection settings before connecting.");

            var builder = new MySqlConnectionStringBuilder
            {
                Server = GetNormalizedServer(),
                Port = (uint)Port,
                Database = Database,
                UserID = User,
                Password = Password,
                ConnectionTimeout = 10,
                Pooling = true,
                MinimumPoolSize = 0,
                MaximumPoolSize = 10,
                ConnectionIdleTimeout = 30,
                SslMode = MySqlSslMode.None,
                AllowZeroDateTime = true,
                ConvertZeroDateTime = true
            };

            // SyncId is stored as CHAR(36) in the existing MySQL schemas and
            // represented as a string by the EF model. Without this setting,
            // newer MySqlConnector versions materialize CHAR(36) GUID values as
            // System.Guid, causing remote login and sync queries to fail.
            builder["GuidFormat"] = "None";

            return builder.ConnectionString;
        }
    }
}
