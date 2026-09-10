using System;
using System.Threading.Tasks;
using eSureHi.Data;
using eSureHi.Helpers;
using MySqlConnector;

namespace eSureHi.ViewModels.Shared
{
    public class ConnectionSettingsViewModel : ObservableObject
    {
        private string _eSureHiServer = "194.59.164.58";
        private string _eSureHiPort = "3306";
        private string _eSureHiDatabase = "u621755393_ims";
        private string _eSureHiUser = "u621755393_ims_user";
        private string _eSureHiPassword = "Ims@2026";
        // Prefilled presets: Network = office LAN, Online = Hostinger cloud.
        // Local preset is hidden per requirements.
        private const string NetworkServer = "192.168.0.42";
        private const string NetworkPort = "3306";
        private const string NetworkDatabase = "ims_db";
        private const string NetworkUser = "root";
        private const string NetworkPassword = "network@2026";

        private const string OnlineServer = "194.59.164.58";
        private const string OnlinePort = "3306";
        private const string OnlineDatabase = "u621755393_ims";
        private const string OnlineUser = "u621755393_ims_user";
        private const string OnlinePassword = "Ims@2026";

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

        // Both presets are prefilled and fully editable.
        public bool IsCredentialsEditable => true;

        public bool IsNetworkServerPlaceholderVisible => false;
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
            eSureHiServer = string.IsNullOrWhiteSpace(cfg.Server) ? OnlineServer : cfg.Server;
            eSureHiPort = cfg.Port == 0 ? OnlinePort : cfg.Port.ToString();
            eSureHiDatabase = string.IsNullOrWhiteSpace(cfg.Database) ? OnlineDatabase : cfg.Database;
            eSureHiUser = string.IsNullOrWhiteSpace(cfg.User) ? OnlineUser : cfg.User;
            eSureHiPassword = string.IsNullOrWhiteSpace(cfg.Password) ? OnlinePassword : cfg.Password;

            // Detect current mode for highlight; default to Online.
            if (string.Equals(eSureHiServer.Trim(), OnlineServer, StringComparison.OrdinalIgnoreCase))
            {
                IsNetworkSelected = false;
                IsRemoteSelected = true;
            }
            else
            {
                IsNetworkSelected = true;
                IsRemoteSelected = false;
            }
            IsLocalSelected = false;
        }

        private void ApplyLocal()
        {
            // Local preset hidden — default to Network prefilled credentials.
            ApplyNetwork();
        }

        private void ApplyNetwork()
        {
            IsNetworkSelected = true;
            IsLocalSelected = false;
            IsRemoteSelected = false;
            NetworkIp = string.Empty;
            eSureHiServer = NetworkServer;
            eSureHiPort = NetworkPort;
            eSureHiDatabase = NetworkDatabase;
            eSureHiUser = NetworkUser;
            eSureHiPassword = NetworkPassword;
            UpdatePasswordBox?.Invoke(eSureHiPassword);
            StatusMessage = "Network database selected. Click Test Connection or Save.";
        }

        private void ApplyRemote()
        {
            IsNetworkSelected = false;
            IsLocalSelected = false;
            IsRemoteSelected = true;
            NetworkIp = string.Empty;
            eSureHiServer = OnlineServer;
            eSureHiPort = OnlinePort;
            eSureHiDatabase = OnlineDatabase;
            eSureHiUser = OnlineUser;
            eSureHiPassword = OnlinePassword;
            UpdatePasswordBox?.Invoke(eSureHiPassword);
            StatusMessage = "Online database selected. Click Test Connection or Save.";
        }

        private int ParsePort()
        {
            return int.TryParse(eSureHiPort, out int p) && p > 0 && p <= 65535
                ? p
                : 3306;
        }

        private async Task TestConnectionAsync()
        {
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

                TestSuccess = true;
                StatusMessage = "Connection successful!";
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
            if (string.IsNullOrWhiteSpace(eSureHiServer) ||
                string.IsNullOrWhiteSpace(eSureHiDatabase) ||
                string.IsNullOrWhiteSpace(eSureHiUser))
            {
                StatusMessage = "Server, Database, and User fields are required.";
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
