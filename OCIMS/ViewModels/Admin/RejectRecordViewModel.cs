using CommunityToolkit.Mvvm.ComponentModel;

namespace eSureHi.ViewModels.Admin
{
    /// <summary>
    /// Backs the small reject-confirmation dialog on the Admin distribution board.
    /// Holds nothing but the beneficiary being rejected and the reason typed by the
    /// reviewer — the caller reads <see cref="Reason"/> back after the dialog closes.
    /// </summary>
    public class RejectRecordViewModel : ObservableObject
    {
        public string BeneficiaryName { get; }

        private string _reason = string.Empty;
        public string Reason { get => _reason; set => SetProperty(ref _reason, value); }

        public RejectRecordViewModel(string beneficiaryName)
        {
            BeneficiaryName = beneficiaryName;
        }
    }
}
