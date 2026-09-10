using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using eSureHi.Data;
using eSureHi.Models;
using eSureHi.Services;
using Microsoft.EntityFrameworkCore;

namespace eSureHi.ViewModels.Admin
{
    /// <summary>
    /// Backs the digital ID card popup shown when a beneficiary is scanned / searched in
    /// Distribution. Loads identity + family + release history and runs the monthly-limit /
    /// duplicate-release check that routes the beneficiary to Release or On Hold.
    /// </summary>
    public class DistributionIdCardViewModel : ObservableObject
    {
        public int BenId { get; }

        public string FullName { get; private set; } = "Unknown";
        public string BeneficiaryId { get; private set; } = string.Empty;
        public string Program { get; private set; } = "—";
        public string Relationship { get; private set; } = "—";
        public string Address { get; private set; } = "—";
        public string DateOfBirth { get; private set; } = "—";

        private decimal _programLimit;
        public decimal ProgramLimit { get => _programLimit; private set => SetProperty(ref _programLimit, value); }

        private decimal _programRemaining;
        public decimal ProgramRemaining { get => _programRemaining; private set => SetProperty(ref _programRemaining, value); }

        private decimal _totalClaimed;
        public decimal TotalClaimed { get => _totalClaimed; private set => SetProperty(ref _totalClaimed, value); }

        private decimal _totalApproved;
        public decimal TotalApproved { get => _totalApproved; private set => SetProperty(ref _totalApproved, value); }

        public ObservableCollection<FamilyMemberDisplay> FamilyMembers { get; } = new();

        private int _releasesThisMonth;
        public int ReleasesThisMonth { get => _releasesThisMonth; private set => SetProperty(ref _releasesThisMonth, value); }

        private int _totalReleases;
        public int TotalReleases { get => _totalReleases; private set => SetProperty(ref _totalReleases, value); }

        private string _lastReleaseText = "No prior release on record";
        public string LastReleaseText { get => _lastReleaseText; private set => SetProperty(ref _lastReleaseText, value); }

        private bool _isEligible = true;
        public bool IsEligible { get => _isEligible; private set { SetProperty(ref _isEligible, value); OnPropertyChanged(nameof(IsNotEligible)); } }
        public bool IsNotEligible => !IsEligible;

        private string _statusBanner = "Checking eligibility…";
        public string StatusBanner { get => _statusBanner; private set => SetProperty(ref _statusBanner, value); }

        private string _statusBannerColor = "#64748B";
        public string StatusBannerColor { get => _statusBannerColor; private set => SetProperty(ref _statusBannerColor, value); }

        private string _statusBannerBackground = "#F1F5F9";
        public string StatusBannerBackground { get => _statusBannerBackground; private set => SetProperty(ref _statusBannerBackground, value); }

        private string _statusBannerIcon = "ShieldCheck";
        public string StatusBannerIcon { get => _statusBannerIcon; private set => SetProperty(ref _statusBannerIcon, value); }

        /// <summary>Remark stored on the record when routed to On Hold.</summary>
        public string HoldReason { get; private set; } = string.Empty;

        public string CurrentStatus { get; }

        public DistributionIdCardViewModel(int benId, string currentStatus = "Unreleased")
        {
            BenId = benId;
            CurrentStatus = currentStatus;
        }

        public async Task LoadAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();

                var beneficiary = await db.Beneficiaries.FirstOrDefaultAsync(b => b.BenId == BenId);
                if (beneficiary == null)
                {
                    SetVerdict(false, "Beneficiary record not found.", "#991B1B", "#FEE2E2", "AlertCircle");
                    HoldReason = "Beneficiary record not found.";
                    return;
                }

                FullName = beneficiary.FullName;
                BeneficiaryId = beneficiary.BeneficiaryId ?? beneficiary.BenId.ToString();
                Program = string.IsNullOrWhiteSpace(beneficiary.SourceOfFunds) ? "—" : beneficiary.SourceOfFunds!;
                Relationship = string.IsNullOrWhiteSpace(beneficiary.Relationship) ? "—" : beneficiary.Relationship!;
                DateOfBirth = beneficiary.DateOfBirth?.ToString("MMM dd, yyyy") ?? "—";
                OnPropertyChanged(nameof(FullName));
                OnPropertyChanged(nameof(BeneficiaryId));
                OnPropertyChanged(nameof(Program));
                OnPropertyChanged(nameof(Relationship));
                OnPropertyChanged(nameof(DateOfBirth));

                // ── Family + address via the CRS cache (shared lookup logic) ──
                var snapshot = await HouseholdLookupService.GetHouseholdSnapshotAsync(db, beneficiary);
                Address = snapshot.Address ?? "—";
                OnPropertyChanged(nameof(Address));
                foreach (var member in snapshot.Members)
                    FamilyMembers.Add(member);

                // ── Check if they have filed a claim and calculate totals ──
                var claims = await db.Claims
                    .Where(c => c.BenId == BenId || (c.BenId == null && c.EmpId == beneficiary.EmpId))
                    .ToListAsync();
                bool hasClaim = claims.Any();
                TotalClaimed = claims.Sum(c => c.AmountClaimed);
                TotalApproved = claims.Sum(c => c.AmountApproved);
                OnPropertyChanged(nameof(TotalClaimed));
                OnPropertyChanged(nameof(TotalApproved));

                // ── Load Program Benefit Limit ──
                var benefit = await db.Benefits
                    .Include(b => b.EmployeePolicy)
                    .Where(b => b.EmployeePolicy != null && b.EmployeePolicy.EmpId == beneficiary.EmpId && b.YearPeriod == DateTime.Today.Year)
                    .FirstOrDefaultAsync();
                if (benefit != null)
                {
                    ProgramLimit = benefit.MaxBenefit;
                    ProgramRemaining = benefit.Remaining;
                }
                else
                {
                    ProgramLimit = 0;
                    ProgramRemaining = 0;
                }
                OnPropertyChanged(nameof(ProgramLimit));
                OnPropertyChanged(nameof(ProgramRemaining));

                // ── Release history + monthly-limit check ──
                var releases = await db.DistributionRecords
                    .Include(r => r.Batch)
                    .Where(r => r.BeneficiaryId == BenId && r.Status == "Released")
                    .OrderByDescending(r => r.ProcessedAt)
                    .ToListAsync();

                TotalReleases = releases.Count;

                var now = DateTime.Now;
                var monthStart = new DateTime(now.Year, now.Month, 1);
                var thisMonth = releases
                    .Where(r => r.ProcessedAt.HasValue && r.ProcessedAt.Value >= monthStart)
                    .ToList();
                ReleasesThisMonth = thisMonth.Count;

                var last = releases.FirstOrDefault();
                if (last != null)
                {
                    var when = last.ProcessedAt?.ToString("MMM dd, yyyy") ?? "unknown date";
                    var batchName = last.Batch?.ProjectTitle ?? "a past batch";
                    LastReleaseText = $"Last released {when} under \"{batchName}\"";
                }

                if (CurrentStatus == "Released")
                {
                    SetVerdict(false,
                        "RELEASED — Benefit has been successfully released/claimed.",
                        "#166534", "#DCFCE7", "CheckCircle");
                    return;
                }

                if (!hasClaim)
                {
                    HoldReason = "No claim has been filed for this beneficiary.";
                    SetVerdict(false,
                        "ON HOLD — File claim first.",
                        "#B45309", "#FEF3C7", "AlertCircle");
                }
                else if (thisMonth.Any())
                {
                    var when = thisMonth.First().ProcessedAt?.ToString("MMM dd, yyyy") ?? "this month";
                    HoldReason = $"Already released this month ({when}) — monthly limit reached.";
                    SetVerdict(false,
                        "ON HOLD — monthly release limit already reached.",
                        "#991B1B", "#FEE2E2", "AlertCircle");
                }
                else
                {
                    SetVerdict(true,
                        "ELIGIBLE — no release recorded this month.",
                        "#166534", "#DCFCE7", "ShieldCheck");
                }
            }
            catch (Exception ex)
            {
                // Fail closed: if the history query didn't run, the monthly-limit check
                // didn't run either, so the card must not offer Release.
                HoldReason = "Could not verify release history — database unavailable.";
                SetVerdict(false,
                    $"UNAVAILABLE — could not verify eligibility. {ex.Message}",
                    "#991B1B", "#FEE2E2", "AlertCircle");
            }
        }

        private void SetVerdict(bool eligible, string banner, string color, string background, string icon)
        {
            IsEligible = eligible;
            StatusBanner = banner;
            StatusBannerColor = color;
            StatusBannerBackground = background;
            StatusBannerIcon = icon;
        }

        public void SetReleasedVerdict(string message)
        {
            IsEligible = false;
            StatusBanner = message;
            StatusBannerColor = "#166534";
            StatusBannerBackground = "#DCFCE7";
            StatusBannerIcon = "CheckCircle";
        }
    }
}
