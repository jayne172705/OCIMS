using MySql.Data.MySqlClient;

namespace OCIMS
{
    public static class DatabaseHelper
    {
        // I-change ang YOUR_PASSWORD_HERE sa imong MySQL root password
        private static string ConnectionString =
            "Server=localhost;" +
            "Database=ocims_db;" +
            "Uid=root;" +
            "Pwd=172705;";

        public static MySqlConnection GetConnection()
        {
            return new MySqlConnection(ConnectionString);
        }

        public static bool TestConnection()
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}