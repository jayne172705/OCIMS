using System;
using System.Threading.Tasks;
using eSureHi.Data;
using eSureHi.Helpers;
using MySqlConnector;

namespace eSureHi.ViewModels.Shared
{
    public class ConnectionSettingsViewModel : ObservableObject
    {
        private string _eSureHiServer = "localhost";
        private string _eSureHiPort = "3306";
        private string _eSureHiDatabase = "";
        private string _eSureHiUser = "";
        private string _eSureHiPassword = "";
        private const string CrsDatabaseName = "u621755393_crs";
        private const string CrsUserName = "u621755393_crs_user";
        private const string CrsPasswordValue = "Crs@2026";

        public string eSureHiServer { get => _eSureHiServer; set => SetProperty(ref _eSureHiServer, value); }
        public string eSureHiPort { get => _eSureHiPort; set => SetProperty(ref _eSureHiPort, value); }
        public string eSureHiDatabase { get => _eSureHiDatabase; set => SetProperty(ref _eSureHiDatabase, value); }
        public string eSureHiUser { get => _eSureHiUser; set => SetProperty(ref _eSureHiUser, value); }
        public string eSureHiPassword { get => _eSureHiPassword; set => SetProperty(ref _eSureHiPassword, value); }

        private string _networkIp = "";
        public string NetworkIp
        {
            get => _networkIp;
            set
            {
                if (SetProperty(ref _networkIp, value))
                {
                    if (IsNetworkSelected)
                        eSureHiServer = value.Trim();

                    OnPropertyChanged(nameof(IsNetworkServerPlaceholderVisible));
                }
            }
        }

        private string _statusMessage = string.Empty;
        private bool _isBusy = false;
        private bool _testSuccess = false;
        private bool _isNetworkSelected = false;

        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public bool TestSuccess { get => _testSuccess; set => SetProperty(ref _testSuccess, value); }
        public bool IsNetworkSelected
        {
            get => _isNetworkSelected;
            set
            {
                if (SetProperty(ref _isNetworkSelected, value))
                {
                    OnPropertyChanged(nameof(IsNetworkServerPlaceholderVisible));
                    OnPropertyChanged(nameof(IsCredentialsEditable));
                }
            }
        }

        private bool _isLocalSelected = false;
        public bool IsLocalSelected
        {
            get => _isLocalSelected;
            set => SetProperty(ref _isLocalSelected, value);
        }

        private bool _isRemoteSelected = false;
        public bool IsRemoteSelected
        {
            get => _isRemoteSelected;
            set => SetProperty(ref _isRemoteSelected, value);
        }

        public bool IsCredentialsEditable => IsNetworkSelected;

        public bool IsNetworkServerPlaceholderVisible =>
            IsNetworkSelected && string.IsNullOrWhiteSpace(NetworkIp);
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    TestConnectionCommand.RaiseCanExecuteChanged();
                    SaveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public RelayCommand PresetLocalCommand { get; }
        public RelayCommand PresetNetworkCommand { get; }
        public RelayCommand PresetRemoteCommand { get; }
        public RelayCommand TestConnectionCommand { get; private set; }
        public RelayCommand SaveCommand { get; private set; }

        public Action? CloseAction { get; set; }
        public Action<string>? UpdatePasswordBox { get; set; }

        public ConnectionSettingsViewModel()
        {
            PresetLocalCommand = new RelayCommand(ApplyLocal);
            PresetNetworkCommand = new RelayCommand(ApplyNetwork);
            PresetRemoteCommand = new RelayCommand(ApplyRemote);
            TestConnectionCommand = new RelayCommand(
                async () => await TestConnectionAsync(),
                () => !IsBusy);
            SaveCommand = new RelayCommand(Save, () => !IsBusy);
            LoadCurrent();
        }

        private void LoadCurrent()
        {
            var cfg = App.DbConfig;
            eSureHiServer = cfg.Server;
            eSureHiPort = cfg.Port.ToString();
            eSureHiDatabase = cfg.Database;
            eSureHiUser = cfg.User;
            eSureHiPassword = cfg.Password;
        }

        private void ApplyLocal()
        {
            IsNetworkSelected = false;
            eSureHiServer = "127.0.0.1";
            eSureHiPort = "3306";
            eSureHiDatabase = "ocims";
            eSureHiUser = "root";
            eSureHiPassword = "172705";
            StatusMessage = "Local offline database selected. Click Test Connection or Save.";
        }

        private void ApplyNetwork()
        {
            IsNetworkSelected = true;
            if (!string.IsNullOrWhiteSpace(NetworkIp))
                eSureHiServer = NetworkIp.Trim();

            eSureHiPort = "3306";
            eSureHiDatabase = CrsDatabaseName;
            eSureHiUser = CrsUserName;
            eSureHiPassword = CrsPasswordValue;
            StatusMessage = "Network CRS selected. Enter the CRS server/IP, then test or save. Login still uses your saved eSureHi database; Beneficiaries will use this CRS source.";
        }

        private void ApplyRemote()
        {
            IsNetworkSelected = false;
            eSureHiServer = "194.59.164.58";
            eSureHiPort = "3306";
            eSureHiDatabase = "u621755393_ims";
            eSureHiUser = "u621755393_ims_user";
            eSureHiPassword = "Ims@2026";
            StatusMessage = "Remote online database selected. Click Test Connection or Save.";
        }

        private int ParsePort()
        {
            return int.TryParse(eSureHiPort, out int p) && p > 0 && p <= 65535
                ? p
                : 3306;
        }

        private async Task TestConnectionAsync()
        {
            if (IsNetworkSelected)
            {
                if (string.IsNullOrWhiteSpace(NetworkIp))
                {
                    StatusMessage = "Please enter the network server/IP before testing.";
                    return;
                }

                eSureHiServer = NetworkIp.Trim();
            }

            if (string.IsNullOrWhiteSpace(eSureHiServer) ||
                string.IsNullOrWhiteSpace(eSureHiDatabase) ||
                string.IsNullOrWhiteSpace(eSureHiUser))
            {
                StatusMessage = "Please fill in Server, Database, and User before testing.";
                return;
            }

            IsBusy = true;
            TestSuccess = false;
            StatusMessage = "Testing connection...";

            try
            {
                var connStr = new MySqlConnectionStringBuilder
                {
                    Server = eSureHiServer.Trim(),
                    Port = (uint)ParsePort(),
                    Database = eSureHiDatabase.Trim(),
                    UserID = eSureHiUser.Trim(),
                    Password = eSureHiPassword,
                    ConnectionTimeout = 10,
                    SslMode = MySqlSslMode.None,
                    AllowZeroDateTime = true,
                    ConvertZeroDateTime = true
                }.ConnectionString;

                using var conn = new MySqlConnection(connStr);
                await conn.OpenAsync();

                if (IsNetworkSelected)
                {
                    using var cmd = new MySqlCommand(
                        "SELECT COUNT(*) FROM val_beneficiaries LIMIT 1",
                        conn);
                    await cmd.ExecuteScalarAsync();
                }

                TestSuccess = true;
                StatusMessage = IsNetworkSelected
                    ? "CRS connection successful. Beneficiaries can load the CRS master list."
                    : "Connection successful!";
            }
            catch (Exception ex)
            {
                TestSuccess = false;
                StatusMessage = $"Failed: {ex.Message}";
                System.Windows.MessageBox.Show($"Connection failed:\n{ex.Message}", "Connection Test");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void Save()
        {
            if (IsNetworkSelected)
            {
                if (string.IsNullOrWhiteSpace(NetworkIp))
                {
                    StatusMessage = "Please enter the network server/IP before saving.";
                    return;
                }

                eSureHiServer = NetworkIp.Trim();
            }

            if (string.IsNullOrWhiteSpace(eSureHiServer) ||
                string.IsNullOrWhiteSpace(eSureHiDatabase) ||
                string.IsNullOrWhiteSpace(eSureHiUser))
            {
                StatusMessage = "Server, Database, and User fields are required.";
                return;
            }

            if (IsNetworkSelected)
            {
                var crsConfig = new SharedDatabaseConfiguration
                {
                    Server = eSureHiServer.Trim(),
                    Port = ParsePort().ToString(),
                    Database = eSureHiDatabase.Trim(),
                    User = eSureHiUser.Trim(),
                    Password = eSureHiPassword.Trim()
                };

                SharedDatabaseConfiguration.SaveCrs(crsConfig);
                StatusMessage = "Network CRS settings saved. Beneficiaries will load from this CRS source after login.";
                CloseAction?.Invoke();
                return;
            }

            var cfg = new DatabaseConfiguration
            {
                Server = eSureHiServer.Trim(),
                Port = ParsePort(),
                Database = eSureHiDatabase.Trim(),
                User = eSureHiUser.Trim(),
                Password = eSureHiPassword.Trim()
            };

            cfg.Save();
            App.DbConfig = cfg;
            StatusMessage = "Settings saved.";
            CloseAction?.Invoke();
        }
    }
}
