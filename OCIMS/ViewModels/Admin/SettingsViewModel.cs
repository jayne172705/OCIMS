using Microsoft.Win32;
using MySqlConnector;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace eSureHi.ViewModels.Admin
{
    public class SettingsViewModel : ObservableObject
    {
        // ── Navigation ────────────────────────────────────────────────
        private string _selectedSection = "Staff Accounts";
        public string SelectedSection
        {
            get => _selectedSection;
            set => SetProperty(ref _selectedSection, value);
        }

        public string[] SectionList { get; } = new[] 
        { 
            "Staff Accounts", "System Profile", "App Database", 
            "GGMS Settings", "CRS Settings", "Backups & Sync" 
        };

        // ── App Database Presets (Master Config) ───────────────────────
        private bool _isLocalSelected;
        private bool _isNetworkSelected;
        private bool _isRemoteSelected;

        public bool IsLocalSelected { get => _isLocalSelected; set => SetProperty(ref _isLocalSelected, value); }
        public bool IsNetworkSelected { get => _isNetworkSelected; set => SetProperty(ref _isNetworkSelected, value); }
        public bool IsRemoteSelected { get => _isRemoteSelected; set => SetProperty(ref _isRemoteSelected, value); }

        private string _appDbServer = string.Empty;
        private string _appDbPort = "3306";
        private string _appDbDatabase = string.Empty;
        private string _appDbUser = string.Empty;
        private string _appDbPassword = string.Empty;

        public string AppDbServer { get => _appDbServer; set => SetProperty(ref _appDbServer, value); }
        public string AppDbPort { get => _appDbPort; set => SetProperty(ref _appDbPort, value); }
        public string AppDbDatabase { get => _appDbDatabase; set => SetProperty(ref _appDbDatabase, value); }
        public string AppDbUser { get => _appDbUser; set => SetProperty(ref _appDbUser, value); }
        public string AppDbPassword { get => _appDbPassword; set => SetProperty(ref _appDbPassword, value); }

        public bool IsAppDbEditable => IsNetworkSelected;

        // ── Connection Status ──────────────────────────────────────────
        private string _connectionStatus = "Not tested";
        private bool _connectionOk;

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }
        public bool ConnectionOk
        {
            get => _connectionOk;
            set => SetProperty(ref _connectionOk, value);
        }

        public string CurrentServer =>
            $"{App.DbConfig.Server}:{App.DbConfig.Port} / {App.DbConfig.Database}";

        private string _ggmsServer = string.Empty;
        private string _ggmsPort = "3306";
        private string _ggmsDatabase = string.Empty;
        private string _ggmsUser = string.Empty;
        private string _ggmsPassword = string.Empty;
        private string _ggmsConnectionStatus = "Not tested";
        private bool _ggmsConnectionOk;

        public string GgmsServer { get => _ggmsServer; set => SetProperty(ref _ggmsServer, value); }
        public string GgmsPort { get => _ggmsPort; set => SetProperty(ref _ggmsPort, value); }
        public string GgmsDatabase { get => _ggmsDatabase; set => SetProperty(ref _ggmsDatabase, value); }
        public string GgmsUser { get => _ggmsUser; set => SetProperty(ref _ggmsUser, value); }
        public string GgmsPassword { get => _ggmsPassword; set => SetProperty(ref _ggmsPassword, value); }
        public string GgmsConnectionStatus { get => _ggmsConnectionStatus; set => SetProperty(ref _ggmsConnectionStatus, value); }
        public bool GgmsConnectionOk { get => _ggmsConnectionOk; set => SetProperty(ref _ggmsConnectionOk, value); }

        private string _crsServer = string.Empty;
        private string _crsPort = "3306";
        private string _crsDatabase = string.Empty;
        private string _crsUser = string.Empty;
        private string _crsPassword = string.Empty;
        private string _crsConnectionStatus = "Not tested";
        private bool _crsConnectionOk;

        public string CrsServer { get => _crsServer; set => SetProperty(ref _crsServer, value); }
        public string CrsPort { get => _crsPort; set => SetProperty(ref _crsPort, value); }
        public string CrsDatabase { get => _crsDatabase; set => SetProperty(ref _crsDatabase, value); }
        public string CrsUser { get => _crsUser; set => SetProperty(ref _crsUser, value); }
        public string CrsPassword { get => _crsPassword; set => SetProperty(ref _crsPassword, value); }
        public string CrsConnectionStatus { get => _crsConnectionStatus; set => SetProperty(ref _crsConnectionStatus, value); }
        public bool CrsConnectionOk { get => _crsConnectionOk; set => SetProperty(ref _crsConnectionOk, value); }

        // ── Backup Info ────────────────────────────────────────────────
        private BackupInfo _backupInfo = new();
        public BackupInfo BackupInfo
        {
            get => _backupInfo;
            set
            {
                SetProperty(ref _backupInfo, value);
                OnPropertyChanged(nameof(LastFullBackup));
                OnPropertyChanged(nameof(LastDifferentialBackup));
                OnPropertyChanged(nameof(LastIncrementalBackup));
                OnPropertyChanged(nameof(FullCount));
                OnPropertyChanged(nameof(DiffCount));
                OnPropertyChanged(nameof(IncCount));
                OnPropertyChanged(nameof(BackupPath));
            }
        }

        public string LastFullBackup =>
            BackupInfo.Metadata.LastFullBackup?.ToString("MMM dd, yyyy hh:mm tt")
            ?? "Never";
        public string LastDifferentialBackup =>
            BackupInfo.Metadata.LastDifferentialBackup?.ToString("MMM dd, yyyy hh:mm tt")
            ?? "Never";
        public string LastIncrementalBackup =>
            BackupInfo.Metadata.LastIncrementalBackup?.ToString("MMM dd, yyyy hh:mm tt")
            ?? "Never";
        public int FullCount => BackupInfo.FullBackupCount;
        public int DiffCount => BackupInfo.DifferentialBackupCount;
        public int IncCount => BackupInfo.IncrementalBackupCount;
        public string BackupPath =>
            string.IsNullOrWhiteSpace(BackupInfo.Metadata.DefaultBackupPath)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups")
                : BackupInfo.Metadata.DefaultBackupPath;

        // ── State ──────────────────────────────────────────────────────
        private bool _isBusy;
        private string _statusMessage = string.Empty;

        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public bool CanEditSettings => PermissionService.CanEditSettings;

        // ── About ──────────────────────────────────────────────────────
        public string AppVersion => "eSureHi v1.0.0";

        private string _orgName = "Municipality of Sulop";
        public string OrgName 
        { 
            get => _orgName; 
            set => SetProperty(ref _orgName, value); 
        }

        private string _systemDesc = "Office Constituent Insurance Management System\n" +
                                     "Manages insurance policies, claims, premiums,\n" +
                                     "benefits, and documents for municipal employees.";
        public string SystemDesc 
        { 
            get => _systemDesc; 
            set => SetProperty(ref _systemDesc, value); 
        }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand TestConnectionCommand { get; }
        public RelayCommand OpenConnectionCommand { get; }
        public RelayCommand BrowseBackupPathCommand { get; }
        public RelayCommand OpenBackupFolderCommand { get; }
        public RelayCommand SaveGgmsConfigCommand { get; }
        public RelayCommand TestGgmsConnectionCommand { get; }
        public RelayCommand SaveCrsConfigCommand { get; }
        public RelayCommand TestCrsConnectionCommand { get; }
        public RelayCommand FullBackupCommand { get; }
        public RelayCommand DifferentialBackupCommand { get; }
        public RelayCommand IncrementalBackupCommand { get; }
        public RelayCommand RestoreBackupCommand { get; }
        public RelayCommand TestSyncConnectionsCommand { get; }
        public RelayCommand DownloadOnlineCommand { get; }
        public RelayCommand UploadOfflineCommand { get; }
        public RelayCommand TwoWaySyncCommand { get; }
        public RelayCommand SeedAdminCommand { get; }
        public RelayCommand SeedDocumentTypesCommand { get; }
        public RelayCommand PurgeDemoDataCommand { get; }

        public RelayCommand<string> SelectSectionCommand { get; }
        public RelayCommand ApplyLocalPresetCommand { get; }
        public RelayCommand ApplyNetworkPresetCommand { get; }
        public RelayCommand ApplyRemotePresetCommand { get; }
        public RelayCommand SaveAppDbConfigCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

        // ── Constructor ────────────────────────────────────────────────
        public SettingsViewModel()
        {
            SelectSectionCommand = new RelayCommand<string>(s => SelectedSection = s ?? "Staff Accounts");
            ApplyLocalPresetCommand = new RelayCommand(ApplyLocalPreset, () => CanEditSettings);
            ApplyNetworkPresetCommand = new RelayCommand(ApplyNetworkPreset, () => CanEditSettings);
            ApplyRemotePresetCommand = new RelayCommand(ApplyRemotePreset, () => CanEditSettings);
            SaveAppDbConfigCommand = new RelayCommand(SaveAppDbConfig, () => CanEditSettings);
            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);

            TestConnectionCommand = new RelayCommand(async () => await TestConnectionAsync(), () => CanEditSettings);
            OpenConnectionCommand = new RelayCommand(OpenConnectionSettings, () => CanEditSettings);
            BrowseBackupPathCommand = new RelayCommand(BrowseBackupPath, () => CanEditSettings);
            OpenBackupFolderCommand = new RelayCommand(OpenBackupFolder);
            SaveGgmsConfigCommand = new RelayCommand(SaveGgmsConfig, () => CanEditSettings);
            TestGgmsConnectionCommand = new RelayCommand(async () => await TestGgmsConnectionAsync(), () => CanEditSettings);
            SaveCrsConfigCommand = new RelayCommand(SaveCrsConfig, () => CanEditSettings);
            TestCrsConnectionCommand = new RelayCommand(async () => await TestCrsConnectionAsync(), () => CanEditSettings);
            FullBackupCommand = new RelayCommand(async () => await RunBackupAsync("Full"), () => CanEditSettings);
            DifferentialBackupCommand = new RelayCommand(async () => await RunBackupAsync("Differential"), () => CanEditSettings);
            IncrementalBackupCommand = new RelayCommand(async () => await RunBackupAsync("Incremental"), () => CanEditSettings);
            RestoreBackupCommand = new RelayCommand(async () => await RestoreAsync(), () => CanEditSettings);
            TestSyncConnectionsCommand = new RelayCommand(async () => await TestSyncConnectionsAsync(), () => CanEditSettings);
            DownloadOnlineCommand = new RelayCommand(async () => await RunSyncAsync(SyncDirection.OnlineToOffline), () => CanEditSettings);
            UploadOfflineCommand = new RelayCommand(async () => await RunSyncAsync(SyncDirection.OfflineToOnline), () => CanEditSettings);
            TwoWaySyncCommand = new RelayCommand(async () => await RunSyncAsync(SyncDirection.TwoWay), () => CanEditSettings);
            SeedAdminCommand = new RelayCommand(async () => await SeedAdminAsync(), () => CanEditSettings);
            SeedDocumentTypesCommand = new RelayCommand(async () => await SeedDocumentTypesAsync(), () => CanEditSettings);
            PurgeDemoDataCommand = new RelayCommand(async () => await PurgeDemoDataAsync(), () => CanEditSettings);

            RefreshBackupInfo();
            LoadExternalConfigs();
        }

        private void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(new Views.Admin.UserControls.HomeView());
        }

        // ── Connection ─────────────────────────────────────────────────
        private async Task TestConnectionAsync()
        {
            IsBusy = true;
            ConnectionStatus = "Testing...";
            ConnectionOk = false;

            try
            {
                var connStr = App.DbConfig.ToConnectionString();
                using var conn = new MySqlConnection(connStr);
                await conn.OpenAsync();
                ConnectionStatus = $"✔ Connected — {App.DbConfig.Server}";
                ConnectionOk = true;
                OnPropertyChanged(nameof(CurrentServer));
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"✘ Failed: {ex.Message}";
                ConnectionOk = false;
            }
            finally { IsBusy = false; }
        }

        private void OpenConnectionSettings()
        {
            var dialog = new Views.Shared.ConnectionSettingsDialog();
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
            OnPropertyChanged(nameof(CurrentServer));
        }

        // ── External Configs ──────────────────────────────────────────
        private void LoadExternalConfigs()
        {
            // Main App DB
            var cfg = App.DbConfig;
            AppDbServer = cfg.Server;
            AppDbPort = cfg.Port.ToString();
            AppDbDatabase = cfg.Database;
            AppDbUser = cfg.User;
            AppDbPassword = cfg.Password;

            // Detect current mode
            if (AppDbServer == "127.0.0.1" || AppDbServer == "localhost") IsLocalSelected = true;
            else if (AppDbServer == "194.59.164.58") IsRemoteSelected = true;
            else IsNetworkSelected = true;

            var ggms = SharedDatabaseConfiguration.LoadGgms();
            GgmsServer = ggms.Server;
            GgmsPort = ggms.Port;
            GgmsDatabase = ggms.Database;
            GgmsUser = ggms.User;
            GgmsPassword = ggms.Password;

            var crs = SharedDatabaseConfiguration.LoadCrs();
            CrsServer = crs.Server;
            CrsPort = crs.Port;
            CrsDatabase = crs.Database;
            CrsUser = crs.User;
            CrsPassword = crs.Password;
        }

        private void ApplyLocalPreset()
        {
            IsLocalSelected = true; IsNetworkSelected = false; IsRemoteSelected = false;
            AppDbServer = "127.0.0.1"; AppDbPort = "3306"; AppDbDatabase = "ocims"; AppDbUser = "root"; AppDbPassword = "172705";
            OnPropertyChanged(nameof(IsAppDbEditable));
        }

        private void ApplyNetworkPreset()
        {
            IsLocalSelected = false; IsNetworkSelected = true; IsRemoteSelected = false;
            if (AppDbServer == "127.0.0.1" || AppDbServer == "194.59.164.58") AppDbServer = "";
            OnPropertyChanged(nameof(IsAppDbEditable));
        }

        private void ApplyRemotePreset()
        {
            IsLocalSelected = false; IsNetworkSelected = false; IsRemoteSelected = true;
            AppDbServer = "194.59.164.58"; AppDbPort = "3306"; AppDbDatabase = "u621755393_ims"; AppDbUser = "u621755393_ims_user"; AppDbPassword = "Ims@2026";
            OnPropertyChanged(nameof(IsAppDbEditable));
        }

        private void SaveAppDbConfig()
        {
            try
            {
                var cfg = new DatabaseConfiguration
                {
                    Server = AppDbServer.Trim(),
                    Port = int.TryParse(AppDbPort, out var p) ? p : 3306,
                    Database = AppDbDatabase.Trim(),
                    User = AppDbUser.Trim(),
                    Password = AppDbPassword.Trim()
                };
                cfg.Save();
                App.DbConfig = cfg;
                StatusMessage = "App Database settings saved masterfully.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save App DB settings: {ex.Message}");
            }
        }

        private static string NormalizePort(string? port) =>
            int.TryParse(port, out var parsed) && parsed > 0 && parsed <= 65535
                ? parsed.ToString()
                : "3306";

        private SharedDatabaseConfiguration BuildGgmsConfig() => new()
        {
            Server = GgmsServer.Trim(),
            Port = NormalizePort(GgmsPort),
            Database = GgmsDatabase.Trim(),
            User = GgmsUser.Trim(),
            Password = GgmsPassword.Trim()
        };

        private SharedDatabaseConfiguration BuildCrsConfig() => new()
        {
            Server = CrsServer.Trim(),
            Port = NormalizePort(CrsPort),
            Database = CrsDatabase.Trim(),
            User = CrsUser.Trim(),
            Password = CrsPassword.Trim()
        };

        private void SaveGgmsConfig()
        {
            try
            {
                SharedDatabaseConfiguration.SaveGgms(BuildGgmsConfig());
                GgmsConnectionStatus = "GGMS settings saved.";
                GgmsConnectionOk = false;
                StatusMessage = "GGMS settings saved.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save GGMS settings: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveCrsConfig()
        {
            try
            {
                SharedDatabaseConfiguration.SaveCrs(BuildCrsConfig());
                CrsConnectionStatus = "CRS settings saved.";
                CrsConnectionOk = false;
                StatusMessage = "CRS settings saved.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save CRS settings: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task TestGgmsConnectionAsync()
        {
            await TestExternalConnectionAsync(
                "GGMS",
                BuildGgmsConfig(),
                new[] { "yearlybudgets", "budget_allocations", "consolidated_transactions" },
                ok =>
                {
                    GgmsConnectionOk = ok;
                    if (ok) StatusMessage = "GGMS connection verified.";
                },
                message => GgmsConnectionStatus = message);
        }

        private async Task TestCrsConnectionAsync()
        {
            await TestExternalConnectionAsync(
                "CRS",
                BuildCrsConfig(),
                new[] { "val_beneficiaries" },
                ok =>
                {
                    CrsConnectionOk = ok;
                    if (ok) StatusMessage = "CRS connection verified.";
                },
                message => CrsConnectionStatus = message);
        }

        private async Task TestExternalConnectionAsync(
            string systemName,
            SharedDatabaseConfiguration config,
            string[] requiredTables,
            Action<bool> setSuccess,
            Action<string> setStatus)
        {
            IsBusy = true;
            setSuccess(false);
            setStatus($"Testing {systemName}...");

            try
            {
                var connStr = config.ToConnectionString();
                using var conn = new MySqlConnection(connStr);
                await conn.OpenAsync();

                foreach (var table in requiredTables)
                {
                    using var cmd = new MySqlCommand(
                        "SELECT COUNT(*) FROM information_schema.tables " +
                        "WHERE table_schema = DATABASE() AND table_name = @table", conn);
                    cmd.Parameters.AddWithValue("@table", table);
                    var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    if (count == 0)
                        throw new InvalidOperationException(
                            $"Required table '{table}' was not found in the configured database.");
                }

                setSuccess(true);
                setStatus($"Connected and schema verified for {systemName}.");
            }
            catch (Exception ex)
            {
                setSuccess(false);
                setStatus($"Failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void BrowseBackupPath()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Backup Folder",
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder",
                Filter = "Folders|no_files",
                ValidateNames = false
            };

            if (dlg.ShowDialog() == true)
            {
                var folder = Path.GetDirectoryName(dlg.FileName)!;
                BackupService.SetDefaultBackupPath(folder);
                RefreshBackupInfo();
            }
        }

        private void OpenBackupFolder()
        {
            try
            {
                if (!Directory.Exists(BackupPath))
                    Directory.CreateDirectory(BackupPath);

                Process.Start(new ProcessStartInfo
                {
                    FileName = BackupPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot open folder: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RunBackupAsync(string type)
        {
            var info = BackupService.GetBackupInfo();

            if (type != "Full" && info.Metadata.LastFullBackup is null)
            {
                MessageBox.Show(
                    "No full backup exists.\nCreate a full backup first.",
                    "Cannot Backup", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            StatusMessage = $"Running {type} backup...";

            try
            {
                var filePath = await Task.Run(() => type switch
                {
                    "Full" => BackupService.ExecuteFullBackup(),
                    "Differential" => BackupService.ExecuteDifferentialBackup(),
                    "Incremental" => BackupService.ExecuteIncrementalBackup(),
                    _ => throw new InvalidOperationException()
                });

                RefreshBackupInfo();
                StatusMessage = $"✔ {type} backup completed.";

                MessageBox.Show(
                    $"{type} backup created successfully.\n\n{filePath}",
                    "Backup Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"✘ Backup failed.";
                MessageBox.Show($"Backup failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        private async Task RestoreAsync()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Backup Files|*.json",
                Title = "Select eSureHi Backup File"
            };
            if (dlg.ShowDialog() != true) return;

            var filePath = dlg.FileName;
            IsBusy = true;
            StatusMessage = "Restoring...";

            try
            {
                await Task.Run(() => BackupService.RestoreFromBackup(filePath));
                StatusMessage = "✔ Restore completed.";
                MessageBox.Show(
                    $"Restore completed successfully.\n\nPlease restart eSureHi.",
                    "Restore Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = "✘ Restore failed.";
                MessageBox.Show($"Restore failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        private async Task TestSyncConnectionsAsync()
        {
            IsBusy = true;
            StatusMessage = "Testing online/offline sync connections...";
            try
            {
                var result = await OfflineOnlineSyncService.TestConnectionsAsync();
                StatusMessage = result.Message;
                MessageBox.Show(result.Message, "Offline / Online Sync",
                    MessageBoxButton.OK,
                    result.OnlineOk && result.OfflineOk
                        ? MessageBoxImage.Information
                        : MessageBoxImage.Warning);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RunSyncAsync(SyncDirection direction)
        {
            IsBusy = true;
            StatusMessage = "Sync running...";
            try
            {
                var result = await OfflineOnlineSyncService.SyncAsync(direction);
                StatusMessage = $"Sync complete. {result.Message}";
                MessageBox.Show(StatusMessage, "Offline / Online Sync",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Sync failed: {ex.Message}";
                MessageBox.Show(StatusMessage, "Offline / Online Sync",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SeedAdminAsync()
        {
            IsBusy = true;
            try
            {
                var (seeded, message) = await AuthService.Instance.SeedAdminAsync();
                StatusMessage = seeded ? "✔ Admin seeded." : "Admin already exists.";
                MessageBox.Show(message, "Seed Admin",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Seed failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        private async Task SeedDocumentTypesAsync()
        {
            IsBusy = true;
            try
            {
                await AuthService.Instance.SeedDocumentTypesAsync();
                StatusMessage = "✔ Document types seeded.";
                MessageBox.Show("Default document types have been added to the database.", "Seed Document Types",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Seed failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        private async Task PurgeDemoDataAsync()
        {
            var result = MessageBox.Show(
                "Are you sure you want to delete all seeded demo data? This will clear all pre-populated employees, claims, and beneficiaries, leaving only manual entries.",
                "Purge Demo Data",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            try
            {
                await DemoDataSeederService.PurgeDemoDataAsync();
                MessageBox.Show("Demo data successfully purged!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Purge failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RefreshBackupInfo()
        {
            BackupInfo = BackupService.GetBackupInfo();
        }
    }
}
