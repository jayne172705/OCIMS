using System.Collections.Generic;
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

        // In-memory fallback
        private static List<UserAccount> _localUsers = new List<UserAccount>
        {
            new UserAccount { UserId=1, Username="admin", FullName="HR Admin", Role="HR Admin" }
        };
        private static List<(string Username, string Password)> _localPasswords =
            new List<(string, string)> { ("admin", "admin123") };

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

            UserAccount user = LoginFromDatabase(username, password);
            if (user == null) user = LoginFromMemory(username, password);

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

        private UserAccount LoginFromDatabase(string username, string password)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        SELECT u.user_id, u.username, u.role,
                               IFNULL(CONCAT(e.first_name,' ',e.last_name), u.username) AS full_name
                        FROM system_users u
                        LEFT JOIN employees e ON e.emp_id = u.emp_id
                        WHERE u.username     = @user
                          AND u.password_hash = @pass
                          AND u.is_active    = 1
                        LIMIT 1";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@user", username);
                        cmd.Parameters.AddWithValue("@pass", password);

                        using (var r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                int uid = System.Convert.ToInt32(r["user_id"]);
                                UpdateLastLogin(uid);
                                return new UserAccount
                                {
                                    UserId = uid,
                                    Username = r["username"].ToString(),
                                    FullName = r["full_name"].ToString(),
                                    Role = r["role"].ToString()
                                };
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private void UpdateLastLogin(int userId)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "UPDATE system_users SET last_login=NOW() WHERE user_id=@id";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", userId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        private UserAccount LoginFromMemory(string username, string password)
        {
            foreach (var cred in _localPasswords)
            {
                if (cred.Username == username && cred.Password == password)
                {
                    foreach (var u in _localUsers)
                        if (u.Username == username) return u;
                }
            }
            return null;
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

            if (username == "admin")
            { ShowSignupError("That username is not allowed."); return; }

            if (string.IsNullOrEmpty(password) || password.Length < 6)
            { ShowSignupError("Password must be at least 6 characters."); return; }

            if (password != confirm)
            { ShowSignupError("Passwords do not match."); return; }

            if (UsernameExistsInDb(username))
            { ShowSignupError("Username '" + username + "' is already taken."); return; }

            foreach (var u in _localUsers)
                if (u.Username == username)
                { ShowSignupError("Username '" + username + "' is already taken."); return; }

            // Save to MySQL
            SaveUserToDatabase(username, password, fullName);

            // Save to memory fallback
            _localUsers.Add(new UserAccount
            {
                UserId = _localUsers.Count + 1,
                Username = username,
                FullName = fullName,
                Role = "Employee"
            });
            _localPasswords.Add((username, password));

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

        private bool SaveUserToDatabase(string username, string password, string fullName)
        {
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
                        cmd.Parameters.AddWithValue("@pass", password);
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch { return false; }
        }

        private bool UsernameExistsInDb(string username)
        {
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
            catch { return false; }
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
