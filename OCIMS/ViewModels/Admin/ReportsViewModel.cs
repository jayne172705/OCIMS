using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;

namespace eSureHi.ViewModels.Admin
{
    public class ReportsViewModel : ObservableObject
    {
        // ══════════════════════════════════════════════════════════════
        // REPORT 1 — Employee Coverage
        // ══════════════════════════════════════════════════════════════
        public ObservableCollection<VwEmployeeCoverage> CoverageData { get; } = new();
        public ObservableCollection<string> PolicyTypeFilter { get; } = new();

        private string _coveragePolicyType = "All";
        private string _coverageStatus = "All";
        private string _coverageSearch = string.Empty;
        private bool _coverageLoading;
        private int _coverageCount;

        public string CoveragePolicyType
        {
            get => _coveragePolicyType;
            set { SetProperty(ref _coveragePolicyType, value); _ = LoadCoverageAsync(); }
        }
        public string CoverageStatus
        {
            get => _coverageStatus;
            set { SetProperty(ref _coverageStatus, value); _ = LoadCoverageAsync(); }
        }
        public string CoverageSearch
        {
            get => _coverageSearch;
            set { SetProperty(ref _coverageSearch, value); _ = LoadCoverageAsync(); }
        }
        public bool CoverageLoading { get => _coverageLoading; set => SetProperty(ref _coverageLoading, value); }
        public int CoverageCount { get => _coverageCount; set => SetProperty(ref _coverageCount, value); }

        public string[] AssignmentStatusOptions { get; } =
            { "All", "Active", "Terminated", "Expired", "On-Hold" };

        // ══════════════════════════════════════════════════════════════
        // REPORT 2 — Claims Summary
        // ══════════════════════════════════════════════════════════════
        public ObservableCollection<VwClaimsSummary> ClaimsData { get; } = new();

        private string _claimsSearch = string.Empty;
        private bool _claimsLoading;
        private int _claimsCount;
        private bool _claimsWithOnly;

        public string ClaimsSearch
        {
            get => _claimsSearch;
            set { SetProperty(ref _claimsSearch, value); _ = LoadClaimsAsync(); }
        }
        public bool ClaimsWithOnly
        {
            get => _claimsWithOnly;
            set { SetProperty(ref _claimsWithOnly, value); _ = LoadClaimsAsync(); }
        }
        public bool ClaimsLoading { get => _claimsLoading; set => SetProperty(ref _claimsLoading, value); }
        public int ClaimsCount { get => _claimsCount; set => SetProperty(ref _claimsCount, value); }

        // Totals
        private decimal _totalClaimed;
        private decimal _totalApproved;
        private decimal _totalReleased;

        public decimal TotalClaimed { get => _totalClaimed; set => SetProperty(ref _totalClaimed, value); }
        public decimal TotalApproved { get => _totalApproved; set => SetProperty(ref _totalApproved, value); }
        public decimal TotalReleased { get => _totalReleased; set => SetProperty(ref _totalReleased, value); }

        // ══════════════════════════════════════════════════════════════
        // REPORT 3 — Premium Status
        // ══════════════════════════════════════════════════════════════
        public ObservableCollection<VwPremiumStatus> PremiumData { get; } = new();

        private string _premiumSearch = string.Empty;
        private string _premiumStatus = "All";
        private DateTime? _premiumMonthFrom;
        private DateTime? _premiumMonthTo;
        private bool _premiumLoading;
        private int _premiumCount;

        public string PremiumSearch
        {
            get => _premiumSearch;
            set { SetProperty(ref _premiumSearch, value); _ = LoadPremiumAsync(); }
        }
        public string PremiumStatus
        {
            get => _premiumStatus;
            set { SetProperty(ref _premiumStatus, value); _ = LoadPremiumAsync(); }
        }
        public DateTime? PremiumMonthFrom
        {
            get => _premiumMonthFrom;
            set { SetProperty(ref _premiumMonthFrom, value); _ = LoadPremiumAsync(); }
        }
        public DateTime? PremiumMonthTo
        {
            get => _premiumMonthTo;
            set { SetProperty(ref _premiumMonthTo, value); _ = LoadPremiumAsync(); }
        }
        public bool PremiumLoading { get => _premiumLoading; set => SetProperty(ref _premiumLoading, value); }
        public int PremiumCount { get => _premiumCount; set => SetProperty(ref _premiumCount, value); }

        public string[] PremiumStatusOptions { get; } =
            { "All", "Unpaid", "Paid", "Partial", "Late", "Waived" };

        private decimal _premiumTotalDue;
        private decimal _premiumTotalPaid;
        private decimal _premiumTotalBalance;

        public decimal PremiumTotalDue { get => _premiumTotalDue; set => SetProperty(ref _premiumTotalDue, value); }
        public decimal PremiumTotalPaid { get => _premiumTotalPaid; set => SetProperty(ref _premiumTotalPaid, value); }
        public decimal PremiumTotalBalance { get => _premiumTotalBalance; set => SetProperty(ref _premiumTotalBalance, value); }

        // ══════════════════════════════════════════════════════════════
        // REPORT 4 — Benefit Utilization
        // ══════════════════════════════════════════════════════════════
        public ObservableCollection<Benefit> BenefitData { get; } = new();

        private string _benefitSearch = string.Empty;
        private string _benefitType = "All";
        private int _benefitYear = DateTime.Today.Year;
        private bool _benefitLoading;
        private int _benefitCount;

        public string BenefitSearch
        {
            get => _benefitSearch;
            set { SetProperty(ref _benefitSearch, value); _ = LoadBenefitAsync(); }
        }
        public string BenefitType
        {
            get => _benefitType;
            set { SetProperty(ref _benefitType, value); _ = LoadBenefitAsync(); }
        }
        public int BenefitYear
        {
            get => _benefitYear;
            set { SetProperty(ref _benefitYear, value); _ = LoadBenefitAsync(); }
        }
        public bool BenefitLoading { get => _benefitLoading; set => SetProperty(ref _benefitLoading, value); }
        public int BenefitCount { get => _benefitCount; set => SetProperty(ref _benefitCount, value); }

        public string[] BenefitTypeOptions { get; } =
            { "All", "Medical", "Dental", "Vision", "Life", "Accident", "Optical", "Others" };

        public int[] YearOptions { get; } = Enumerable.Range(DateTime.Today.Year - 3, 6).ToArray();

        private decimal _benefitTotalMax;
        private decimal _benefitTotalUsed;
        private decimal _benefitTotalRemaining;

        public decimal BenefitTotalMax { get => _benefitTotalMax; set => SetProperty(ref _benefitTotalMax, value); }
        public decimal BenefitTotalUsed { get => _benefitTotalUsed; set => SetProperty(ref _benefitTotalUsed, value); }
        public decimal BenefitTotalRemaining { get => _benefitTotalRemaining; set => SetProperty(ref _benefitTotalRemaining, value); }

        // ══════════════════════════════════════════════════════════════
        // COMMANDS
        // ══════════════════════════════════════════════════════════════
        public RelayCommand RefreshCoverageCommand { get; }
        public RelayCommand ExportCoveragePdfCommand { get; }
        public RelayCommand ExportCoverageXlsCommand { get; }
        public RelayCommand ExportCoverageWordCommand { get; }

        public RelayCommand RefreshClaimsCommand { get; }
        public RelayCommand ExportClaimsPdfCommand { get; }
        public RelayCommand ExportClaimsXlsCommand { get; }
        public RelayCommand ExportClaimsWordCommand { get; }

        public RelayCommand RefreshPremiumCommand { get; }
        public RelayCommand ExportPremiumPdfCommand { get; }
        public RelayCommand ExportPremiumXlsCommand { get; }
        public RelayCommand ExportPremiumWordCommand { get; }

        public RelayCommand RefreshBenefitCommand { get; }
        public RelayCommand ExportBenefitPdfCommand { get; }
        public RelayCommand ExportBenefitXlsCommand { get; }
        public RelayCommand ExportBenefitWordCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

        private int _selectedReportTab;
        public int SelectedReportTab
        {
            get => _selectedReportTab;
            set => SetProperty(ref _selectedReportTab, value);
        }

        // ══════════════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════
        public ReportsViewModel()
        {
            RefreshCoverageCommand = new RelayCommand(async () => await LoadCoverageAsync());
            ExportCoveragePdfCommand = new RelayCommand(() =>
                ReportExportService.ExportEmployeeCoveragePdf(
                    CoverageData, BuildTitle("Employee Coverage Report")));
            ExportCoverageXlsCommand = new RelayCommand(() =>
                ReportExportService.ExportEmployeeCoverageExcel(
                    CoverageData, BuildTitle("Employee Coverage Report")));
            ExportCoverageWordCommand = new RelayCommand(() =>
                ReportExportService.ExportEmployeeCoverageWord(
                    CoverageData, BuildTitle("Employee Coverage Report")));

            RefreshClaimsCommand = new RelayCommand(async () => await LoadClaimsAsync());
            ExportClaimsPdfCommand = new RelayCommand(() =>
                ReportExportService.ExportClaimsSummaryPdf(
                    ClaimsData, BuildTitle("Claims Summary Report")));
            ExportClaimsXlsCommand = new RelayCommand(() =>
                ReportExportService.ExportClaimsSummaryExcel(
                    ClaimsData, BuildTitle("Claims Summary Report")));
            ExportClaimsWordCommand = new RelayCommand(() =>
                ReportExportService.ExportClaimsSummaryWord(
                    ClaimsData, BuildTitle("Claims Summary Report")));

            RefreshPremiumCommand = new RelayCommand(async () => await LoadPremiumAsync());
            ExportPremiumPdfCommand = new RelayCommand(() =>
                ReportExportService.ExportPremiumStatusPdf(
                    PremiumData, BuildTitle("Premium Status Report")));
            ExportPremiumXlsCommand = new RelayCommand(() =>
                ReportExportService.ExportPremiumStatusExcel(
                    PremiumData, BuildTitle("Premium Status Report")));
            ExportPremiumWordCommand = new RelayCommand(() =>
                ReportExportService.ExportPremiumStatusWord(
                    PremiumData, BuildTitle("Premium Status Report")));

            RefreshBenefitCommand = new RelayCommand(async () => await LoadBenefitAsync());
            ExportBenefitPdfCommand = new RelayCommand(() =>
                ReportExportService.ExportBenefitUtilizationPdf(
                    BenefitData, BuildTitle("Benefit Utilization Report")));
            ExportBenefitXlsCommand = new RelayCommand(() =>
                ReportExportService.ExportBenefitUtilizationExcel(
                    BenefitData, BuildTitle("Benefit Utilization Report")));
            ExportBenefitWordCommand = new RelayCommand(() =>
                ReportExportService.ExportBenefitUtilizationWord(
                    BenefitData, BuildTitle("Benefit Utilization Report")));
            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);

            // Load all on startup
            _ = LoadCoverageAsync();
            _ = LoadClaimsAsync();
            _ = LoadPremiumAsync();
            _ = LoadBenefitAsync();
        }

        // ══════════════════════════════════════════════════════════════
        // LOAD METHODS
        // ══════════════════════════════════════════════════════════════

        private async Task LoadCoverageAsync()
        {
            CoverageLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var hasBarangay = await HasColumnAsync("vw_employee_coverage", "barangay");
                var q = hasBarangay
                    ? db.VwEmployeeCoverage.FromSqlRaw(@"
                        SELECT ep_id, emp_id, employee_no, full_name, dept_name, barangay,
                               policy_name, policy_type, coverage_limit, employee_share,
                               employer_share, start_date, end_date, assignment_status
                        FROM vw_employee_coverage")
                    : db.VwEmployeeCoverage.FromSqlRaw(@"
                        SELECT ep_id, emp_id, employee_no, full_name, dept_name, '' AS barangay,
                               policy_name, policy_type, coverage_limit, employee_share,
                               employer_share, start_date, end_date, assignment_status
                        FROM vw_employee_coverage");

                if (CoveragePolicyType != "All")
                    q = q.Where(r => r.PolicyType == CoveragePolicyType);
                if (CoverageStatus != "All")
                    q = q.Where(r => r.AssignmentStatus == CoverageStatus);

                var data = await q.OrderBy(r => r.FullName).ToListAsync();

                if (!string.IsNullOrWhiteSpace(CoverageSearch))
                {
                    var s = CoverageSearch.Trim().ToLower();
                    data = data.Where(r =>
                        (r.FullName?.ToLower().Contains(s) ?? false) ||
                        (r.EmployeeNo?.ToLower().Contains(s) ?? false) ||
                        (r.Barangay?.ToLower().Contains(s) ?? false) ||
                        (r.PolicyName?.ToLower().Contains(s) ?? false)).ToList();
                }

                CoverageData.Clear();
                foreach (var r in data) CoverageData.Add(r);
                CoverageCount = CoverageData.Count;
            }
            catch
            {
                await LoadCoverageFromTablesAsync();
            }
            finally { CoverageLoading = false; }
        }

        private async Task LoadClaimsAsync()
        {
            ClaimsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var hasBarangay = await HasColumnAsync("vw_claims_summary", "barangay");
                var q = hasBarangay
                    ? db.VwClaimsSummary.FromSqlRaw(@"
                        SELECT emp_id, employee_no, full_name, dept_name, barangay,
                               total_claims, total_claimed, total_approved, total_released,
                               pending_claims, approved_claims, rejected_claims
                        FROM vw_claims_summary")
                    : db.VwClaimsSummary.FromSqlRaw(@"
                        SELECT emp_id, employee_no, full_name, dept_name, '' AS barangay,
                               total_claims, total_claimed, total_approved, total_released,
                               pending_claims, approved_claims, rejected_claims
                        FROM vw_claims_summary");

                var data = await q
                    .OrderBy(r => r.Barangay)
                    .ThenBy(r => r.FullName)
                    .ToListAsync();

                if (ClaimsWithOnly)
                    data = data.Where(r => r.TotalClaims > 0).ToList();

                if (!string.IsNullOrWhiteSpace(ClaimsSearch))
                {
                    var s = ClaimsSearch.Trim().ToLower();
                    data = data.Where(r =>
                        (r.FullName?.ToLower().Contains(s) ?? false) ||
                        (r.EmployeeNo?.ToLower().Contains(s) ?? false) ||
                        (r.Barangay?.ToLower().Contains(s) ?? false) ||
                        (r.DeptName?.ToLower().Contains(s) ?? false)).ToList();
                }

                ClaimsData.Clear();
                foreach (var r in data) ClaimsData.Add(r);
                ClaimsCount = ClaimsData.Count;
                TotalClaimed = ClaimsData.Sum(r => r.TotalClaimed);
                TotalApproved = ClaimsData.Sum(r => r.TotalApproved);
                TotalReleased = ClaimsData.Sum(r => r.TotalReleased);
            }
            catch
            {
                await LoadClaimsFromTablesAsync();
            }
            finally { ClaimsLoading = false; }
        }

        private async Task LoadPremiumAsync()
        {
            PremiumLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var q = db.VwPremiumStatus.AsQueryable();

                if (PremiumStatus != "All")
                    q = q.Where(r => r.PaymentStatus == PremiumStatus);

                if (PremiumMonthFrom.HasValue)
                {
                    var from = new DateOnly(PremiumMonthFrom.Value.Year,
                                            PremiumMonthFrom.Value.Month, 1);
                    q = q.Where(r => r.BillingMonth >= from);
                }
                if (PremiumMonthTo.HasValue)
                {
                    var to = new DateOnly(PremiumMonthTo.Value.Year,
                                          PremiumMonthTo.Value.Month, 1);
                    q = q.Where(r => r.BillingMonth <= to);
                }

                var data = await q.OrderBy(r => r.BillingMonth)
                                  .ThenBy(r => r.EmployeeName)
                                  .ToListAsync();

                if (!string.IsNullOrWhiteSpace(PremiumSearch))
                {
                    var s = PremiumSearch.Trim().ToLower();
                    data = data.Where(r =>
                        (r.EmployeeName?.ToLower().Contains(s) ?? false) ||
                        (r.PolicyName?.ToLower().Contains(s) ?? false)).ToList();
                }

                PremiumData.Clear();
                foreach (var r in data) PremiumData.Add(r);
                PremiumCount = PremiumData.Count;
                PremiumTotalDue = PremiumData.Sum(r => r.TotalAmount);
                PremiumTotalPaid = PremiumData.Sum(r => r.AmountPaid);
                PremiumTotalBalance = PremiumData.Sum(r => r.Balance);
            }
            catch
            {
                await LoadPremiumFromTablesAsync();
            }
            finally { PremiumLoading = false; }
        }

        private async Task LoadBenefitAsync()
        {
            BenefitLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var q = db.Benefits
                    .Include(b => b.EmployeePolicy)
                        .ThenInclude(ep => ep!.Employee)
                    .Include(b => b.EmployeePolicy)
                        .ThenInclude(ep => ep!.Policy)
                    .Where(b => b.YearPeriod == BenefitYear)
                    .AsQueryable();

                if (BenefitType != "All")
                    q = q.Where(b => b.BenefitType == BenefitType);

                var data = await q.OrderBy(b => b.EmployeePolicy!.Employee!.LastName).ToListAsync();

                if (!string.IsNullOrWhiteSpace(BenefitSearch))
                {
                    var s = BenefitSearch.Trim().ToLower();
                    data = data.Where(b =>
                        (b.EmployeePolicy?.Employee?.FullName.ToLower().Contains(s) ?? false) ||
                        (b.EmployeePolicy?.Policy?.PolicyName.ToLower().Contains(s) ?? false)).ToList();
                }

                BenefitData.Clear();
                foreach (var b in data) BenefitData.Add(b);
                BenefitCount = BenefitData.Count;
                BenefitTotalMax = BenefitData.Sum(b => b.MaxBenefit);
                BenefitTotalUsed = BenefitData.Sum(b => b.UsedBenefit);
                BenefitTotalRemaining = BenefitData.Sum(b => b.Remaining);
            }
            catch (Exception ex)
            {
                App.ReportError("Load Benefit Report Failed", ex);
            }
            finally { BenefitLoading = false; }
        }

        // ── Helpers ────────────────────────────────────────────────────
        private async Task LoadCoverageFromTablesAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var policies = await db.EmployeePolicies
                    .Include(ep => ep.Employee)
                        .ThenInclude(e => e!.Department)
                    .Include(ep => ep.Policy)
                    .OrderBy(ep => ep.Employee!.LastName)
                    .ThenBy(ep => ep.Employee!.FirstName)
                    .ToListAsync();

                var data = policies
                    .Select(ep => new VwEmployeeCoverage
                    {
                        EpId = ep.EpId,
                        EmpId = ep.EmpId,
                        EmployeeNo = ep.Employee != null ? ep.Employee.EmployeeNo : string.Empty,
                        FullName = ep.Employee != null ? ep.Employee.FullName : string.Empty,
                        DeptName = ep.Employee != null && ep.Employee.Department != null ? ep.Employee.Department.DeptName : string.Empty,
                        Barangay = ep.Employee != null ? ep.Employee.Barangay : string.Empty,
                        PolicyName = ep.Policy != null ? ep.Policy.PolicyName : string.Empty,
                        PolicyType = ep.Policy != null ? ep.Policy.PolicyType : string.Empty,
                        CoverageLimit = ep.CoverageLimit,
                        EmployeeShare = ep.EmployeeShare,
                        EmployerShare = ep.EmployerShare,
                        StartDate = ep.StartDate,
                        EndDate = ep.EndDate,
                        AssignmentStatus = ep.AssignmentStatus
                    })
                    .ToList();

                if (CoveragePolicyType != "All")
                    data = data.Where(r => r.PolicyType == CoveragePolicyType).ToList();
                if (CoverageStatus != "All")
                    data = data.Where(r => r.AssignmentStatus == CoverageStatus).ToList();
                if (!string.IsNullOrWhiteSpace(CoverageSearch))
                {
                    var s = CoverageSearch.Trim().ToLower();
                    data = data.Where(r =>
                        (r.FullName?.ToLower().Contains(s) ?? false) ||
                        (r.EmployeeNo?.ToLower().Contains(s) ?? false) ||
                        (r.Barangay?.ToLower().Contains(s) ?? false) ||
                        (r.PolicyName?.ToLower().Contains(s) ?? false)).ToList();
                }

                CoverageData.Clear();
                foreach (var r in data) CoverageData.Add(r);
                CoverageCount = CoverageData.Count;
            }
            catch (Exception ex)
            {
                App.ReportError("Load Coverage Report Failed", ex);
            }
        }

        private async Task LoadClaimsFromTablesAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var employees = await db.Employees
                    .Include(e => e.Department)
                    .OrderBy(e => e.Barangay)
                    .ThenBy(e => e.LastName)
                    .ThenBy(e => e.FirstName)
                    .ToListAsync();

                var claims = await db.Claims.ToListAsync();
                var data = employees.Select(e =>
                {
                    var employeeClaims = claims.Where(c => c.EmpId == e.EmpId).ToList();
                    return new VwClaimsSummary
                    {
                        EmpId = e.EmpId,
                        EmployeeNo = e.EmployeeNo,
                        FullName = e.FullName,
                        DeptName = e.Department?.DeptName ?? string.Empty,
                        Barangay = e.Barangay ?? string.Empty,
                        TotalClaims = employeeClaims.Count,
                        TotalClaimed = employeeClaims.Sum(c => c.AmountClaimed),
                        TotalApproved = employeeClaims.Sum(c => c.AmountApproved),
                        TotalReleased = employeeClaims.Sum(c => c.AmountReleased),
                        PendingClaims = employeeClaims.Count(c => c.ClaimStatus is "Submitted" or "Under Review"),
                        ApprovedClaims = employeeClaims.Count(c => c.ClaimStatus is "Approved" or "Partially Approved" or "Released"),
                        RejectedClaims = employeeClaims.Count(c => c.ClaimStatus == "Rejected")
                    };
                }).ToList();

                if (ClaimsWithOnly)
                    data = data.Where(r => r.TotalClaims > 0).ToList();
                if (!string.IsNullOrWhiteSpace(ClaimsSearch))
                {
                    var s = ClaimsSearch.Trim().ToLower();
                    data = data.Where(r =>
                        (r.FullName?.ToLower().Contains(s) ?? false) ||
                        (r.EmployeeNo?.ToLower().Contains(s) ?? false) ||
                        (r.Barangay?.ToLower().Contains(s) ?? false) ||
                        (r.DeptName?.ToLower().Contains(s) ?? false)).ToList();
                }

                ClaimsData.Clear();
                foreach (var r in data) ClaimsData.Add(r);
                ClaimsCount = ClaimsData.Count;
                TotalClaimed = ClaimsData.Sum(r => r.TotalClaimed);
                TotalApproved = ClaimsData.Sum(r => r.TotalApproved);
                TotalReleased = ClaimsData.Sum(r => r.TotalReleased);
            }
            catch (Exception ex)
            {
                App.ReportError("Load Claims Report Failed", ex);
            }
        }

        private async Task LoadPremiumFromTablesAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var query = db.Premiums
                    .Include(p => p.EmployeePolicy)
                        .ThenInclude(ep => ep!.Employee)
                    .Include(p => p.EmployeePolicy)
                        .ThenInclude(ep => ep!.Policy)
                    .AsQueryable();

                if (PremiumStatus != "All")
                    query = query.Where(p => p.PaymentStatus == PremiumStatus);
                if (PremiumMonthFrom.HasValue)
                {
                    var from = new DateOnly(PremiumMonthFrom.Value.Year, PremiumMonthFrom.Value.Month, 1);
                    query = query.Where(p => p.BillingMonth >= from);
                }
                if (PremiumMonthTo.HasValue)
                {
                    var to = new DateOnly(PremiumMonthTo.Value.Year, PremiumMonthTo.Value.Month, 1);
                    query = query.Where(p => p.BillingMonth <= to);
                }

                var premiums = await query
                    .OrderBy(p => p.BillingMonth)
                    .ThenBy(p => p.EmployeePolicy!.Employee!.LastName)
                    .ThenBy(p => p.EmployeePolicy!.Employee!.FirstName)
                    .ToListAsync();

                var data = premiums.Select(p => new VwPremiumStatus
                {
                    PremiumId = p.PremiumId,
                    BillingMonth = p.BillingMonth,
                    DueDate = p.DueDate,
                    EmployeeName = p.EmployeePolicy?.Employee?.FullName ?? string.Empty,
                    PolicyName = p.EmployeePolicy?.Policy?.PolicyName ?? string.Empty,
                    TotalAmount = p.TotalAmount,
                    AmountPaid = p.AmountPaid,
                    Balance = p.Balance,
                    PaymentStatus = p.PaymentStatus
                }).ToList();

                if (!string.IsNullOrWhiteSpace(PremiumSearch))
                {
                    var s = PremiumSearch.Trim().ToLower();
                    data = data.Where(r =>
                        (r.EmployeeName?.ToLower().Contains(s) ?? false) ||
                        (r.PolicyName?.ToLower().Contains(s) ?? false)).ToList();
                }

                PremiumData.Clear();
                foreach (var r in data) PremiumData.Add(r);
                PremiumCount = PremiumData.Count;
                PremiumTotalDue = PremiumData.Sum(r => r.TotalAmount);
                PremiumTotalPaid = PremiumData.Sum(r => r.AmountPaid);
                PremiumTotalBalance = PremiumData.Sum(r => r.Balance);
            }
            catch (Exception ex)
            {
                App.ReportError("Load Premium Report Failed", ex);
            }
        }

        private static string BuildTitle(string name)
            => $"{name} — As of {DateTime.Now:MMMM dd, yyyy}";
        private static async Task<bool> HasColumnAsync(string tableName, string columnName)
        {
            await using var db = eSureHiDbContextFactory.Create();
            var conn = db.Database.GetDbConnection();
            await db.Database.OpenConnectionAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tableName.Replace("'", "''")}') WHERE name = @column_name;";
            var parameter = cmd.CreateParameter();
            parameter.ParameterName = "@column_name";
            parameter.Value = columnName;
            cmd.Parameters.Add(parameter);

            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        private void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(new Views.Admin.UserControls.HomeView());
        }
    }
}
