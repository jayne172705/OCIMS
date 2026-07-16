using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using MySql.Data.MySqlClient;

namespace OCIMS
{
    public static class DatabaseHelper
    {
        // Connection string lives in App.config (<connectionStrings> / "OcimsDb").
        private static string ConnectionString
        {
            get
            {
                var cs = ConfigurationManager.ConnectionStrings["OcimsDb"];
                if (cs == null || string.IsNullOrWhiteSpace(cs.ConnectionString))
                    throw new InvalidOperationException(
                        "Connection string 'OcimsDb' is missing from App.config.");
                return cs.ConnectionString;
            }
        }

        public static MySqlConnection GetConnection()
        {
            return new MySqlConnection(ConnectionString);
        }

        public static bool TestConnection(out string error)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();
                    error = null;
                    return true;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TestConnection()
        {
            string ignored;
            return TestConnection(out ignored);
        }

        /// <summary>
        /// Creates any missing OCIMS tables from the embedded Data\schema.sql
        /// (every statement is idempotent), widens system_users.password_hash
        /// for bcrypt hashes, and seeds a default admin account when no users
        /// exist. Called once at startup; safe to run repeatedly.
        /// </summary>
        public static void EnsureSchema()
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                foreach (var statement in LoadSchemaStatements())
                    Execute(conn, statement);

                // bcrypt hashes are 60 chars; older schemas may have a narrower column.
                TryExecute(conn, "ALTER TABLE system_users MODIFY password_hash VARCHAR(100) NOT NULL");

                SeedAdminIfEmpty(conn);
            }
        }

        private static List<string> LoadSchemaStatements()
        {
            var statements = new List<string>();
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("OCIMS.Data.schema.sql"))
            using (var reader = new System.IO.StreamReader(stream))
            {
                var current = new StringBuilder();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.TrimStart().StartsWith("--")) continue;
                    current.AppendLine(line);
                    if (line.TrimEnd().EndsWith(";"))
                    {
                        var sql = current.ToString().Trim().TrimEnd(';');
                        if (sql.Length > 0) statements.Add(sql);
                        current.Clear();
                    }
                }
            }
            return statements;
        }

        private static void SeedAdminIfEmpty(MySqlConnection conn)
        {
            using (var count = new MySqlCommand("SELECT COUNT(*) FROM system_users", conn))
            {
                if (Convert.ToInt32(count.ExecuteScalar()) > 0) return;
            }

            using (var cmd = new MySqlCommand(@"
                INSERT INTO system_users (username, password_hash, role, is_active)
                VALUES ('admin', @pass, 'HR Admin', 1)", conn))
            {
                cmd.Parameters.AddWithValue("@pass", PasswordHasher.Hash("admin123"));
                cmd.ExecuteNonQuery();
            }
        }

        private static void Execute(MySqlConnection conn, string sql)
        {
            using (var cmd = new MySqlCommand(sql, conn))
                cmd.ExecuteNonQuery();
        }

        private static void TryExecute(MySqlConnection conn, string sql)
        {
            try { Execute(conn, sql); }
            catch { /* column may already be wide enough / table shape differs */ }
        }
    }
}
