using System.Threading.Tasks;
using System.Windows;
using eSureHi.Helpers;
using eSureHi.Services;

namespace eSureHi.ViewModels.Shared
{
    public class LoginViewModel : ObservableObject
    {
        // ── Fields ─────────────────────────────────────────────────────
        private string _username = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isBusy = false;
        private string _careSuggestion = string.Empty;
        private bool _isDatabaseConnected = true;
        private string _statusMessage = string.Empty;

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        // Password is not directly bound for security; GetPassword callback is used instead.
        private string _password = string.Empty;
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                SetProperty(ref _isBusy, value);
                LoginCommand.RaiseCanExecuteChanged();
            }
        }

        public bool IsDatabaseConnected
        {
            get => _isDatabaseConnected;
            set => SetProperty(ref _isDatabaseConnected, value);
        }

        public string CareSuggestion
        {
            get => _careSuggestion;
            set => SetProperty(ref _careSuggestion, value);
        }

        private string _currentDatabaseInfo = string.Empty;
        public string CurrentDatabaseInfo
        {
            get => _currentDatabaseInfo;
            set => SetProperty(ref _currentDatabaseInfo, value);
        }

        public string SystemSerial { get; } = "ESHI-2026";

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand LoginCommand { get; }
        public RelayCommand OpenSettingsCommand { get; }
        public RelayCommand BootstrapCommand { get; }

        // ── Callbacks ──────────────────────────────────────────────────
        public System.Action? OnLoginSuccess { get; set; }
        public System.Func<string>? GetPassword { get; set; }

        // ── Constructor ────────────────────────────────────────────────
        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(
                async () => await LoginAsync(),
                () => !IsBusy);

            OpenSettingsCommand = new RelayCommand(OpenSettings);
            
            BootstrapCommand = new RelayCommand(async () => await RunBootstrapAsync());

            var suggestions = new[]
            {
                "Better Service, Better Care",
                "Your well-being is important—take care of yourself today.",
                "A five-minute screen break works wonders for your eyes.",
                "Smarter Public Service System",
                "Don't forget to maintain a good posture while working."
            };
            CareSuggestion = suggestions[new System.Random().Next(suggestions.Length)];

            RefreshDatabaseInfo();
        }

        public void RefreshDatabaseInfo()
        {
            var cfg = App.DbConfig;
            CurrentDatabaseInfo = !string.IsNullOrWhiteSpace(cfg.Server) &&
                                   !string.IsNullOrWhiteSpace(cfg.Database)
                ? $"{cfg.Server}:{cfg.Port} / {cfg.Database}"
                : "No database configured.";
        }

        private async Task RunBootstrapAsync()
        {
            StatusMessage = "Checking system state...";
            // Placeholder for upcoming bootstrap logic (e.g. checking migrations, initial user)
            await Task.Delay(500);
            StatusMessage = string.Empty;
        }

        // ── Login ──────────────────────────────────────────────────────
        private async Task LoginAsync()
        {
            ErrorMessage = string.Empty;
            IsBusy = true;

            var pwd = GetPassword?.Invoke() ?? Password;

            var (success, message) =
                await AuthService.Instance.LoginAsync(Username.Trim(), pwd);

            IsBusy = false;

            if (!success)
            {
                ErrorMessage = message;
                return;
            }

            OnLoginSuccess?.Invoke();
        }

        // ── Connection Settings ────────────────────────────────────────
        private void OpenSettings()
        {
            var otpDialog = new Views.Shared.OtpVerificationDialog();
            if (otpDialog.ShowDialog() == true)
            {
                var dialog = new Views.Shared.ConnectionSettingsDialog();
                dialog.ShowDialog();
            }
        }
    }
}
