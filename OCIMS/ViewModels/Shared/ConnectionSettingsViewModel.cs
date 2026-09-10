using System;
using System.Threading.Tasks;
using eSureHi.Data;
using eSureHi.Helpers;
using MySqlConnector;

namespace eSureHi.ViewModels.Shared
{
    public class ConnectionSettingsViewModel : ObservableObject
    {
        // ── IMS_DB (main) prefilled ──
        private string _eSureHiServer = "192.168.0.47";
        private string _eSureHiPort = "3306";
        private string _eSureHiDatabase = "ims_db";
        private string _eSureHiUser = "root";
        private string _eSureHiPassword = "network@2026";

        // ── GGMS_DB prefilled ──
        private string _ggmsServer = "192.168.0.47";
        private string _ggmsPort = "3306";
        private string _ggmsDatabase = "ggms_db";
        private string _ggmsUser = "root";
        private string _ggmsPassword = "network@2026";

        // ── CRS_DB prefilled ──
        private string _crsServer = "192.168.0.47";
        private string _crsPort = "3306";
        private string _crsDatabase = "crs_db";
        private string _crsUser = "root";
        private string _crsPassword = "network@2026";

        // Prefilled presets: Network = office LAN (default), Online = Hostinger cloud.
        // All 3 remote DBs share host/user/pass, different database:
        // IMS_DB=ims_db, GGMS_DB=ggms_db, CRS_DB=crs_db. All editable.
        private const string NetworkServer = "192.168.0.47";
        private const string NetworkPort = "3306";
        private const string NetworkUser = "root";
        private const string NetworkPassword = "network@2026";
        private const string NetworkImsDatabase = "ims_db";
        private const string NetworkGgmsDatabase = "ggms_db";
        private const string NetworkCrsDatabase = "crs_db";

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

        public string GgmsServer { get => _ggmsServer; set => SetProperty(ref _ggmsServer, value); }
        public string GgmsPort { get => _ggmsPort; set => SetProperty(ref _ggmsPort, value); }
        public string GgmsDatabase { get => _ggmsDatabase; set => SetProperty(ref _ggmsDatabase, value); }
        public string GgmsUser { get => _ggmsUser; set => SetProperty(ref _ggmsUser, value); }
        public string GgmsPassword { get => _ggmsPassword; set => SetProperty(ref _ggmsPassword, value); }

        public string CrsServer { get => _crsServer; set => SetProperty(ref _crsServer, value); }
        public string CrsPort { get => _crsPort; set => SetProperty(ref _crsPort, value); }
        public string CrsDatabase { get => _crsDatabase; set => SetProperty(ref _crsDatabase, value); }
        public string CrsUser { get => _crsUser; set => SetProperty(ref _crsUser, value); }
        public string CrsPassword { get => _crsPassword; set => SetProperty(ref _crsPassword, value); }

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
        private bool _isNetworkSelected = true;

        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public bool TestSuccess { get => _testSuccess; set => SetProperty(ref _testSuccess, value); }

        // Per-connection test status (3 testable connections)
        private string _imsTestMessage = string.Empty;
        private bool _imsTestSuccess;
        public string ImsTestMessage { get => _imsTestMessage; set => SetProperty(ref _imsTestMessage, value); }
        public bool ImsTestSuccess { get => _imsTestSuccess; set => SetProperty(ref _imsTestSuccess, value); }

        private string _ggmsTestMessage = string.Empty;
        private bool _ggmsTestSuccess;
        public string GgmsTestMessage { get => _ggmsTestMessage; set => SetProperty(ref _ggmsTestMessage, value); }
        public bool GgmsTestSuccess { get => _ggmsTestSuccess; set => SetProperty(ref _ggmsTestSuccess, value); }

        private string _crsTestMessage = string.Empty;
        private bool _crsTestSuccess;
        public string CrsTestMessage { get => _crsTestMessage; set => SetProperty(ref _crsTestMessage, value); }
        public bool CrsTestSuccess { get => _crsTestSuccess; set => SetProperty(ref _crsTestSuccess, value); }

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

        // All presets are prefilled and fully editable.
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
                    TestGgmsConnectionCommand.RaiseCanExecuteChanged();
                    TestCrsConnectionCommand.RaiseCanExecuteChanged();
                    TestAllCommand.RaiseCanExecuteChanged();
                    SaveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public RelayCommand PresetLocalCommand { get; }
        public RelayCommand PresetNetworkCommand { get; }
        public RelayCommand PresetRemoteCommand { get; }
        public RelayCommand TestConnectionCommand { get; private set; }
        public RelayCommand TestGgmsConnectionCommand { get; private set; }
        public RelayCommand TestCrsConnectionCommand { get; private set; }
        public RelayCommand TestAllCommand { get; private set; }
        public RelayCommand SaveCommand { get; private set; }

        public Action? CloseAction { get; set; }
        public Action<string>? UpdatePasswordBox { get; set; }

        public ConnectionSettingsViewModel()
        {
            PresetLocalCommand = new RelayCommand(ApplyLocal);
            PresetNetworkCommand = new RelayCommand(ApplyNetwork);
            PresetRemoteCommand = new RelayCommand(ApplyRemote);
            TestConnectionCommand = new RelayCommand(
                async () => await TestImsAsync(),
                () => !IsBusy);
            TestGgmsConnectionCommand = new RelayCommand(
                async () => await TestGgmsAsync(),
                () => !IsBusy);
            TestCrsConnectionCommand = new RelayCommand(
                async () => await TestCrsAsync(),
                () => !IsBusy);
            TestAllCommand = new RelayCommand(
                async () => await TestAllAsync(),
                () => !IsBusy);
            SaveCommand = new RelayCommand(Save, () => !IsBusy);
            LoadCurrent();
        }

        private void LoadCurrent()
        {
            // IMS_DB — fall back to network prefilled when empty.
            var cfg = App.DbConfig;
            eSureHiServer = string.IsNullOrWhiteSpace(cfg.Server) ? NetworkServer : cfg.Server;
            eSureHiPort = cfg.Port == 0 ? NetworkPort : cfg.Port.ToString();
            eSureHiDatabase = string.IsNullOrWhiteSpace(cfg.Database) ? NetworkImsDatabase : cfg.Database;
            eSureHiUser = string.IsNullOrWhiteSpace(cfg.User) ? NetworkUser : cfg.User;
            eSureHiPassword = string.IsNullOrWhiteSpace(cfg.Password) ? NetworkPassword : cfg.Password;

            // GGMS_DB — LoadGgms already returns network preset when file missing.
            var ggms = SharedDatabaseConfiguration.LoadGgms();
            GgmsServer = string.IsNullOrWhiteSpace(ggms.Server) ? NetworkServer : ggms.Server;
            GgmsPort = string.IsNullOrWhiteSpace(ggms.Port) ? NetworkPort : ggms.Port;
            GgmsDatabase = string.IsNullOrWhiteSpace(ggms.Database) ? NetworkGgmsDatabase : ggms.Database;
            GgmsUser = string.IsNullOrWhiteSpace(ggms.User) ? NetworkUser : ggms.User;
            GgmsPassword = string.IsNullOrWhiteSpace(ggms.Password) ? NetworkPassword : ggms.Password;

            // CRS_DB — LoadCrs already returns network preset when file missing.
            var crs = SharedDatabaseConfiguration.LoadCrs();
            CrsServer = string.IsNullOrWhiteSpace(crs.Server) ? NetworkServer : crs.Server;
            CrsPort = string.IsNullOrWhiteSpace(crs.Port) ? NetworkPort : crs.Port;
            CrsDatabase = string.IsNullOrWhiteSpace(crs.Database) ? NetworkCrsDatabase : crs.Database;
            CrsUser = string.IsNullOrWhiteSpace(crs.User) ? NetworkUser : crs.User;
            CrsPassword = string.IsNullOrWhiteSpace(crs.Password) ? NetworkPassword : crs.Password;

            // Detect current mode for highlight; default to Network.
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
            // IMS_DB
            eSureHiServer = NetworkServer;
            eSureHiPort = NetworkPort;
            eSureHiDatabase = NetworkImsDatabase;
            eSureHiUser = NetworkUser;
            eSureHiPassword = NetworkPassword;
            // GGMS_DB
            GgmsServer = NetworkServer;
            GgmsPort = NetworkPort;
            GgmsDatabase = NetworkGgmsDatabase;
            GgmsUser = NetworkUser;
            GgmsPassword = NetworkPassword;
            // CRS_DB
            CrsServer = NetworkServer;
            CrsPort = NetworkPort;
            CrsDatabase = NetworkCrsDatabase;
            CrsUser = NetworkUser;
            CrsPassword = NetworkPassword;
            UpdatePasswordBox?.Invoke(eSureHiPassword);
            StatusMessage = "Network databases selected (ims_db / ggms_db / crs_db). Test each or Save.";
        }

        private void ApplyRemote()
        {
            IsNetworkSelected = false;
            IsLocalSelected = false;
            IsRemoteSelected = true;
            NetworkIp = string.Empty;
            // Online = IMS_DB only. GGMS/CRS stay on network (untouched).
            eSureHiServer = OnlineServer;
            eSureHiPort = OnlinePort;
            eSureHiDatabase = OnlineDatabase;
            eSureHiUser = OnlineUser;
            eSureHiPassword = OnlinePassword;
            UpdatePasswordBox?.Invoke(eSureHiPassword);
            StatusMessage = "Online database selected for IMS. GGMS/CRS stay on network. Test each or Save.";
        }

        private int ParsePort(string port)
        {
            return int.TryParse(port, out int p) && p > 0 && p <= 65535
                ? p
                : 3306;
        }

        private static string BuildConnStr(string server, string port, string database, string user, string password, int timeout)
        {
            return new MySqlConnectionStringBuilder
            {
                Server = server.Trim(),
                Port = (uint)(int.TryParse(port, out int p) && p > 0 && p <= 65535 ? p : 3306),
                Database = database.Trim(),
                UserID = user.Trim(),
                Password = password,
                ConnectionTimeout = (uint)timeout,
                SslMode = MySqlSslMode.None,
                AllowZeroDateTime = true,
                ConvertZeroDateTime = true
            }.ConnectionString;
        }

        private async Task TestImsAsync()
        {
            ImsTestMessage = "Testing IMS_DB...";
            ImsTestSuccess = false;
            TestSuccess = false;
            StatusMessage = "Testing IMS_DB...";
            IsBusy = true;
            try
            {
                var connStr = BuildConnStr(eSureHiServer, eSureHiPort, eSureHiDatabase, eSureHiUser, eSureHiPassword, 10);
                using var conn = new MySqlConnection(connStr);
                await conn.OpenAsync();
                ImsTestSuccess = true;
                TestSuccess = true;
                ImsTestMessage = "IMS_DB connected!";
                StatusMessage = "IMS_DB connected!";
            }
            catch (Exception ex)
            {
                ImsTestMessage = $"IMS_DB failed: {ex.Message}";
                StatusMessage = ImsTestMessage;
            }
            finally { IsBusy = false; }
        }

        private async Task TestGgmsAsync()
        {
            GgmsTestMessage = "Testing GGMS_DB...";
            GgmsTestSuccess = false;
            StatusMessage = "Testing GGMS_DB...";
            IsBusy = true;
            try
            {
                var connStr = BuildConnStr(GgmsServer, GgmsPort, GgmsDatabase, GgmsUser, GgmsPassword, 5);
                using var conn = new MySqlConnection(connStr);
                await conn.OpenAsync();
                GgmsTestSuccess = true;
                GgmsTestMessage = "GGMS_DB connected!";
                StatusMessage = "GGMS_DB connected!";
            }
            catch (Exception ex)
            {
                GgmsTestMessage = $"GGMS_DB failed: {ex.Message}";
                StatusMessage = GgmsTestMessage;
            }
            finally { IsBusy = false; }
        }

        private async Task TestCrsAsync()
        {
            CrsTestMessage = "Testing CRS_DB...";
            CrsTestSuccess = false;
            StatusMessage = "Testing CRS_DB...";
            IsBusy = true;
            try
            {
                var connStr = BuildConnStr(CrsServer, CrsPort, CrsDatabase, CrsUser, CrsPassword, 5);
                using var conn = new MySqlConnection(connStr);
                await conn.OpenAsync();
                CrsTestSuccess = true;
                CrsTestMessage = "CRS_DB connected!";
                StatusMessage = "CRS_DB connected!";
            }
            catch (Exception ex)
            {
                CrsTestMessage = $"CRS_DB failed: {ex.Message}";
                StatusMessage = CrsTestMessage;
            }
            finally { IsBusy = false; }
        }

        private async Task TestAllAsync()
        {
            await TestImsAsync();
            if (IsBusy) return;
            // Online = IMS only. Network = all 3.
            if (IsNetworkSelected)
            {
                await TestGgmsAsync();
                if (IsBusy) return;
                await TestCrsAsync();
                StatusMessage = $"IMS:{(ImsTestSuccess ? "OK" : "FAIL")} GGMS:{(GgmsTestSuccess ? "OK" : "FAIL")} CRS:{(CrsTestSuccess ? "OK" : "FAIL")}";
                TestSuccess = ImsTestSuccess && GgmsTestSuccess && CrsTestSuccess;
            }
            else
            {
                StatusMessage = ImsTestSuccess ? "Online IMS_DB connected!" : ImsTestMessage;
            }
        }

        private async Task TestConnectionAsync()
        {
            await TestImsAsync();
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(eSureHiServer) ||
                string.IsNullOrWhiteSpace(eSureHiDatabase) ||
                string.IsNullOrWhiteSpace(eSureHiUser))
            {
                StatusMessage = "IMS_DB: Server, Database, and User fields are required.";
                return;
            }

            // Save all 3 connections.
            var cfg = new DatabaseConfiguration
            {
                Server = eSureHiServer.Trim(),
                Port = ParsePort(eSureHiPort),
                Database = eSureHiDatabase.Trim(),
                User = eSureHiUser.Trim(),
                Password = eSureHiPassword.Trim()
            };
            cfg.Save();
            App.DbConfig = cfg;

            SharedDatabaseConfiguration.SaveGgms(new SharedDatabaseConfiguration
            {
                Server = GgmsServer.Trim(),
                Port = GgmsPort.Trim(),
                Database = GgmsDatabase.Trim(),
                User = GgmsUser.Trim(),
                Password = GgmsPassword.Trim()
            });
            SharedDatabaseConfiguration.SaveCrs(new SharedDatabaseConfiguration
            {
                Server = CrsServer.Trim(),
                Port = CrsPort.Trim(),
                Database = CrsDatabase.Trim(),
                User = CrsUser.Trim(),
                Password = CrsPassword.Trim()
            });

            StatusMessage = "All 3 settings saved (ims_db / ggms_db / crs_db).";
            CloseAction?.Invoke();
        }
    }
}
