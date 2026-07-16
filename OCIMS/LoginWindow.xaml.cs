using System.Windows;
using MySql.Data.MySqlClient;

namespace OCIMS
{
    public class UserAccount
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
    }

    public partial class LoginWindow : Window
    {
        public static UserAccount CurrentUser { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
        }

        private void LoginTab_Checked(object sender, RoutedEventArgs e)
        {
            if (LoginForm == null) return;
            LoginForm.Visibility = Visibility.Visible;
            SignupForm.Visibility = Visibility.Collapsed;
            LoginError.Visibility = Visibility.Collapsed;
        }

        private void SignupTab_Checked(object sender, RoutedEventArgs e)
        {
            if (SignupForm == null) return;
            SignupForm.Visibility = Visibility.Visible;
            LoginForm.Visibility = Visibility.Collapsed;
            SignupError.Visibility = Visibility.Collapsed;
            SignupSuccess.Visibility = Visibility.Collapsed;
        }

        // ── LOGIN ─────────────────────────────────────────────
        private void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            string username = LoginUsername.Text.Trim();
            string password = LoginPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowLoginError("Please enter your username and password.");
                return;
            }

            string dbError;
            UserAccount user = LoginFromDatabase(username, password, out dbError);

            if (dbError != null)
            {
                ShowLoginError("Cannot reach the database. Please check that MySQL is running.\n(" + dbError + ")");
                return;
            }

            if (user != null)
            {
                CurrentUser = user;
                var main = new MainWindow();
                main.Show();
                this.Close();
            }
            else
            {
                ShowLoginError("Invalid username or password. Please try again.");
            }
        }

        private UserAccount LoginFromDatabase(string username, string password, out string dbError)
        {
            dbError = null;
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        SELECT u.user_id, u.username, u.role, u.password_hash,
                               IFNULL(CONCAT(e.first_name,' ',e.last_name), u.username) AS full_name
                        FROM system_users u
                        LEFT JOIN employees e ON e.emp_id = u.emp_id
                        WHERE u.username  = @user
                          AND u.is_active = 1
                        LIMIT 1";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@user", username);

                        int uid = 0;
                        string storedHash = null;
                        UserAccount user = null;

                        using (var r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                uid = System.Convert.ToInt32(r["user_id"]);
                                storedHash = r["password_hash"].ToString();
                                user = new UserAccount
                                {
                                    UserId = uid,
                                    Username = r["username"].ToString(),
                                    FullName = r["full_name"].ToString(),
                                    Role = r["role"].ToString()
                                };
                            }
                        }

                        if (user == null || !PasswordHasher.Verify(password, storedHash))
                            return null;

                        // Upgrade legacy plaintext rows to bcrypt on successful login.
                        if (!PasswordHasher.IsHashed(storedHash))
                            UpdatePasswordHash(conn, uid, PasswordHasher.Hash(password));

                        UpdateLastLogin(conn, uid);
                        return user;
                    }
                }
            }
            catch (MySqlException ex)
            {
                dbError = ex.Message;
                return null;
            }
        }

        private void UpdatePasswordHash(MySqlConnection conn, int userId, string hash)
        {
            try
            {
                using (var cmd = new MySqlCommand(
                    "UPDATE system_users SET password_hash=@hash WHERE user_id=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@hash", hash);
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }

        private void UpdateLastLogin(MySqlConnection conn, int userId)
        {
            try
            {
                using (var cmd = new MySqlCommand(
                    "UPDATE system_users SET last_login=NOW() WHERE user_id=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }

        // ── SIGN UP ───────────────────────────────────────────
        private void SignupBtn_Click(object sender, RoutedEventArgs e)
        {
            string fullName = SignupFullName.Text.Trim();
            string username = SignupUsername.Text.Trim().ToLower();
            string password = SignupPassword.Password;
            string confirm = SignupConfirmPassword.Password;

            if (string.IsNullOrEmpty(fullName))
            { ShowSignupError("Please enter your full name."); return; }

            if (string.IsNullOrEmpty(username) || username.Length < 4)
            { ShowSignupError("Username must be at least 4 characters."); return; }

            if (string.IsNullOrEmpty(password) || password.Length < 6)
            { ShowSignupError("Password must be at least 6 characters."); return; }

            if (password != confirm)
            { ShowSignupError("Passwords do not match."); return; }

            string dbError;
            if (UsernameExistsInDb(username, out dbError))
            { ShowSignupError("Username '" + username + "' is already taken."); return; }

            if (dbError != null)
            { ShowSignupError("Cannot reach the database. Please check that MySQL is running."); return; }

            if (!SaveUserToDatabase(username, password, out dbError))
            { ShowSignupError("Could not create the account: " + dbError); return; }

            SignupError.Visibility = Visibility.Collapsed;
            SignupSuccess.Text = "✔ Account created! You can now sign in as '" + username + "'.";
            SignupSuccess.Visibility = Visibility.Visible;

            SignupFullName.Text = "";
            SignupUsername.Text = "";
            SignupPassword.Password = "";
            SignupConfirmPassword.Password = "";

            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = System.TimeSpan.FromSeconds(2);
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                LoginTab.IsChecked = true;
                LoginUsername.Text = username;
                LoginError.Visibility = Visibility.Collapsed;
            };
            timer.Start();
        }

        private bool SaveUserToDatabase(string username, string password, out string error)
        {
            error = null;
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        INSERT INTO system_users (username, password_hash, role, is_active)
                        VALUES (@user, @pass, 'Employee', 1)";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@user", username);
                        cmd.Parameters.AddWithValue("@pass", PasswordHasher.Hash(password));
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private bool UsernameExistsInDb(string username, out string dbError)
        {
            dbError = null;
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT COUNT(*) FROM system_users WHERE username=@user";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@user", username);
                        return System.Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
            }
            catch (System.Exception ex)
            {
                dbError = ex.Message;
                return false;
            }
        }

        private void ShowLoginError(string message)
        {
            LoginError.Text = "⚠ " + message;
            LoginError.Visibility = Visibility.Visible;
        }

        private void ShowSignupError(string message)
        {
            SignupError.Text = "⚠ " + message;
            SignupError.Visibility = Visibility.Visible;
            SignupSuccess.Visibility = Visibility.Collapsed;
        }
    }
}
