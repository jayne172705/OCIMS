using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;

namespace eSureHi.ViewModels.Admin
{
    // A single distribution-batch line item shown to the beneficiary — read-only.
    public class DistributionStatusItem
    {
        public string StatusDisplay { get; init; } = "Unreleased";
        public string StatusBackground { get; init; } = "#F1F5F9";
        public string StatusForeground { get; init; } = "#64748B";
        public string ProjectTitle { get; init; } = "—";
        public string AmountText { get; init; } = "₱0.00";
        public string FundSourceText { get; init; } = "—";
        public string DateReleasedText { get; init; } = "Not yet released";
        public string RemarksText { get; init; } = string.Empty;
    }

    // Backs the beneficiary's own read-only Distribution Status dialog. No batch
    // management actions live here — it only ever queries records for this BenId.
    public class DistributionStatusViewModel : ObservableObject
    {
        public int BenId { get; }
        public ObservableCollection<DistributionStatusItem> Records { get; } = [];

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        private bool _hasRecords;
        public bool HasRecords { get => _hasRecords; set => SetProperty(ref _hasRecords, value); }

        private string _errorMessage = string.Empty;
        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

        public RelayCommand CloseCommand { get; }
        public Action? CloseAction { get; set; }

        public DistributionStatusViewModel(int benId)
        {
            BenId = benId;
            CloseCommand = new RelayCommand(() => CloseAction?.Invoke());
            _ = LoadAsync();
        }

        public async Task LoadAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var records = await db.DistributionRecords
                    .Include(r => r.Batch).ThenInclude(b => b!.SourceFund)
                    .Where(r => r.BeneficiaryId == BenId)
                    .OrderByDescending(r => r.ProcessedAt ?? r.Batch!.CreatedAt)
                    .ToListAsync();

                Records.Clear();
                foreach (var r in records)
                {
                    var (display, bg, fg) = r.Status switch
                    {
                        "Released" => ("Released", "#DCFCE7", "#166534"),
                        "Pending"  => ("On Hold",  "#FEE2E2", "#991B1B"),
                        "Rejected" => ("Rejected", "#FEE2E2", "#991B1B"),
                        _          => ("Unreleased", "#F1F5F9", "#64748B")
                    };

                    Records.Add(new DistributionStatusItem
                    {
                        StatusDisplay = display,
                        StatusBackground = bg,
                        StatusForeground = fg,
                        ProjectTitle = string.IsNullOrWhiteSpace(r.Batch?.ProjectTitle) ? "—" : r.Batch!.ProjectTitle,
                        AmountText = $"₱{r.Batch?.AmountPerBeneficiary ?? 0:N2}",
                        FundSourceText = string.IsNullOrWhiteSpace(r.Batch?.SourceFund?.FundName) ? "—" : r.Batch!.SourceFund!.FundName,
                        DateReleasedText = r.ProcessedAt.HasValue ? r.ProcessedAt.Value.ToString("MMM dd, yyyy") : "Not yet released",
                        RemarksText = r.Remarks ?? string.Empty
                    });
                }
                HasRecords = Records.Count > 0;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load distribution records: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
