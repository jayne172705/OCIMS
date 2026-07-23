namespace eSureHi.Helpers
{
    public enum StatusSeverity
    {
        Info,
        Warning,
        Error
    }

    public class StatusIndicator : ObservableObject
    {
        private string _message = string.Empty;
        private StatusSeverity _severity = StatusSeverity.Info;

        public string Message
        {
            get => _message;
            set
            {
                if (SetProperty(ref _message, value))
                    OnPropertyChanged(nameof(HasMessage));
            }
        }

        public StatusSeverity Severity
        {
            get => _severity;
            set => SetProperty(ref _severity, value);
        }

        public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

        public void Set(string message, StatusSeverity severity = StatusSeverity.Warning)
        {
            Severity = severity;
            Message = message;
        }

        public void Clear() => Set(string.Empty, StatusSeverity.Info);
    }
}
