using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace eSureHi.ViewModels.Admin
{
    public class UseBenefitViewModel : ObservableObject
    {
        private readonly int _benefitId;

        // ── Display Info ───────────────────────────────────────────────
        public string EmployeeName { get; private set; } = string.Empty;
        public string PolicyName { get; private set; } = string.Empty;
        public string BenefitType { get; private set; } = string.Empty;
        public int YearPeriod { get; private set; }
        public decimal MaxBenefit { get; private set; }
        public decimal UsedBenefit { get; private set; }
        public decimal Remaining { get; private set; }

        // ── Input ──────────────────────────────────────────────────────
        private decimal _amountUsed;
        private DateTime? _usedDate = DateTime.Today;
        private string _notes = string.Empty;
        private string _sourceOfFunds = "Job Order";

        public decimal AmountUsed
        {
            get => _amountUsed;
            set => SetProperty(ref _amountUsed, value);
        }
        public DateTime? UsedDate
        {
            get => _usedDate;
            set => SetProperty(ref _usedDate, value);
        }
        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }
        public ObservableCollection<string> SourceOfFundsOptions { get; } = new()
        {
            "Job Order",
            "Casual",
            "Regular"
        };
        public string SourceOfFunds
        {
            get => _sourceOfFunds;
            set => SetProperty(ref _sourceOfFunds, value);
        }

        // ── State ──────────────────────────────────────────────────────
        private string _errorMessage = string.Empty;
        private bool _isBusy;

        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }
        public bool IsBusy { get => _isBusy; set { SetProperty(ref _isBusy, value); SaveCommand.RaiseCanExecuteChanged(); } }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public Action? CloseAction { get; set; }
        public Action? OnSaveSuccess { get; set; }

        // ── Constructor ────────────────────────────────────────────────
        public UseBenefitViewModel(int benefitId)
        {
            _benefitId = benefitId;
            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !IsBusy);
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
            _ = LoadAsync();
            _ = LoadSourceFundsAsync();
        }

        private async Task LoadSourceFundsAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var names = (await db.SourceFunds
                    .Where(f => f.Status == "Active" &&
                                (f.FundName == "Job Order" ||
                                 f.FundName == "Casual" ||
                                 f.FundName == "Regular"))
                    .Select(f => f.FundName)
                    .ToListAsync())
                    .OrderBy(GetSourceFundSortOrder)
                    .ToList();

                if (!names.Any())
                    return;

                SourceOfFundsOptions.Clear();
                foreach (var name in names)
                    SourceOfFundsOptions.Add(name);

                if (!SourceOfFundsOptions.Contains(SourceOfFunds))
                    SourceOfFunds = SourceOfFundsOptions.FirstOrDefault() ?? "Job Order";
            }
            catch
            {
            }
        }

        // ── Load ───────────────────────────────────────────────────────
        private static int GetSourceFundSortOrder(string name) => name switch
        {
            "Job Order" => 0,
            "Casual" => 1,
            "Regular" => 2,
            _ => 3
        };

        private async Task LoadAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var b = await db.Benefits
                    .Include(x => x.EmployeePolicy)
                        .ThenInclude(ep => ep!.Employee)
                    .Include(x => x.EmployeePolicy)
                        .ThenInclude(ep => ep!.Policy)
                    .FirstOrDefaultAsync(x => x.BenefitId == _benefitId);

                if (b is null) return;

                EmployeeName = b.EmployeePolicy?.Employee?.FullName ?? string.Empty;
                PolicyName = b.EmployeePolicy?.Policy?.PolicyName ?? string.Empty;
                BenefitType = b.BenefitType;
                YearPeriod = b.YearPeriod ?? DateTime.Today.Year;
                MaxBenefit = b.MaxBenefit;
                UsedBenefit = b.UsedBenefit;
                Remaining = b.Remaining;
                SourceOfFunds = string.IsNullOrWhiteSpace(b.SourceOfFunds)
                    ? SourceOfFunds
                    : b.SourceOfFunds;

                OnPropertyChanged(nameof(EmployeeName));
                OnPropertyChanged(nameof(PolicyName));
                OnPropertyChanged(nameof(BenefitType));
                OnPropertyChanged(nameof(YearPeriod));
                OnPropertyChanged(nameof(MaxBenefit));
                OnPropertyChanged(nameof(UsedBenefit));
                OnPropertyChanged(nameof(Remaining));
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Load failed: {ex.Message}";
            }
        }

        // ── Save ───────────────────────────────────────────────────────
        private async Task SaveOnlineAsync()
        {
            await SaveAsync();
        }

        private async Task SaveAsync()
        {
            if (AmountUsed <= 0)
            { ErrorMessage = "Amount used must be greater than zero."; return; }
            if (AmountUsed > Remaining)
            { ErrorMessage = $"Amount exceeds remaining benefit of ₱{Remaining:N2}."; return; }
            if (UsedDate is null)
            { ErrorMessage = "Date is required."; return; }

            IsBusy = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var b = await db.Benefits
                    .Include(x => x.EmployeePolicy)
                        .ThenInclude(ep => ep!.Employee)
                    .Include(x => x.EmployeePolicy)
                        .ThenInclude(ep => ep!.Policy)
                    .FirstOrDefaultAsync(x => x.BenefitId == _benefitId);
                if (b is null) { ErrorMessage = "Record not found."; return; }

                b.UsedBenefit += AmountUsed;
                b.Remaining = b.MaxBenefit - b.UsedBenefit;
                b.SourceOfFunds = SourceOfFunds;
                b.LastUsedDate = DateOnly.FromDateTime(UsedDate.Value);
                b.Notes = string.IsNullOrWhiteSpace(Notes)
                                      ? b.Notes : Notes.Trim();
                b.UpdatedAt = DateTime.Now;
                var localFundCoveredRelease = await ApplySourceFundUsageAsync(db, SourceOfFunds, AmountUsed);
                if (!localFundCoveredRelease)
                {
                    ErrorMessage = $"Local {SourceOfFunds} fund is missing or has insufficient remaining balance.";
                    return;
                }

                await db.SaveChangesAsync();

                await AuditService.LogUpdate("benefits", _benefitId,
                    $"Benefit used: {AmountUsed:N2} from {BenefitType}");

                // ── Write to GGMS ──────────────────────────────────────────────
                var (ggmsSuccess, ggmsMsg) = await GgmsService.RecordBenefitReleaseAsync(
                    benefitId: b.BenefitId,
                    beneficiaryIdentity: b.EmployeePolicy?.Employee?.EmployeeNo,
                    civilRegistryId: null,
                    amountReleased: AmountUsed,
                    benefitType: b.BenefitType,
                    policyName: b.EmployeePolicy?.Policy?.PolicyName ?? string.Empty,
                    firstName: b.EmployeePolicy?.Employee?.FirstName ?? string.Empty,
                    middleName: b.EmployeePolicy?.Employee?.MiddleName,
                    lastName: b.EmployeePolicy?.Employee?.LastName ?? string.Empty,
                    fullName: b.EmployeePolicy?.Employee?.FullName ?? string.Empty,
                    transactionDate: DateOnly.FromDateTime(UsedDate.Value),
                    sourceOfFunds: SourceOfFunds
                );

                if (!ggmsSuccess)
                {
                    ErrorMessage = $"GGMS Recording Failed: {ggmsMsg}";
                }

                OnSaveSuccess?.Invoke();
                CloseAction?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Save failed: {ex.Message}";
            }
            finally { IsBusy = false; }
        }

        private static async Task<bool> ApplySourceFundUsageAsync(eSureHiDbContext db, string? sourceOfFunds, decimal amount)
        {
            if (string.IsNullOrWhiteSpace(sourceOfFunds) || amount <= 0)
                return false;

            var fund = await db.SourceFunds.FirstOrDefaultAsync(f => f.FundName == sourceOfFunds);
            if (fund is null)
                return false;

            if (fund.RemainingAmount < amount)
                return false;

            fund.UsedAmount += amount;
            fund.UpdatedAt = DateTime.Now;
            return true;
        }
    }
}
