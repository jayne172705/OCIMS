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
        // REPORT 5 — Monthly Payment
        // ══════════════════════════════════════════════════════════════
        public ObservableCollection<Payment> MonthlyPaymentData { get; } = new();

        private string _monthlyPaymentSearch = string.Empty;
        private string _monthlyPaymentStatus = "All";
        private string _monthlyPaymentGroup = "All";
        private string _monthlyPaymentMonth = "All";
        private int? _monthlyPaymentYear;
        private bool _monthlyPaymentLoading;
        private int _monthlyPaymentCount;

        public string MonthlyPaymentSearch
        {
            get => _monthlyPaymentSearch;
            set { SetProperty(ref _monthlyPaymentSearch, value); _ = LoadMonthlyPaymentAsync(); }
        }
        public string MonthlyPaymentStatus
        {
            get => _monthlyPaymentStatus;
            set { SetProperty(ref _monthlyPaymentStatus, value); _ = LoadMonthlyPaymentAsync(); }
        }
        public string MonthlyPaymentGroup
        {
            get => _monthlyPaymentGroup;
            set { SetProperty(ref _monthlyPaymentGroup, value); _ = LoadMonthlyPaymentAsync(); }
        }
        public string MonthlyPaymentMonth
        {
            get => _monthlyPaymentMonth;
            set { SetProperty(ref _monthlyPaymentMonth, value); _ = LoadMonthlyPaymentAsync(); }
        }
        public int? MonthlyPaymentYear
        {
            get => _monthlyPaymentYear;
            set { SetProperty(ref _monthlyPaymentYear, value); _ = LoadMonthlyPaymentAsync(); }
        }
        public bool MonthlyPaymentLoading { get => _monthlyPaymentLoading; set => SetProperty(ref _monthlyPaymentLoading, value); }
        public int MonthlyPaymentCount { get => _monthlyPaymentCount; set => SetProperty(ref _monthlyPaymentCount, value); }

        public string[] PaymentStatusOptions { get; } = { "All", "Pending", "Approved", "Rejected" };
        public string[] PaymentGroupOptions { get; } = { "All", "Job Order", "Casual", "Regular", "Captain" };
        public string[] MonthOptions { get; } =
            { "All", "January", "February", "March", "April", "May", "June",
              "July", "August", "September", "October", "November", "December" };

        private decimal _monthlyPaymentTotalAmount;
        private decimal _monthlyPaymentApprovedAmount;
        private decimal _monthlyPaymentPendingAmount;

        public decimal MonthlyPaymentTotalAmount { get => _monthlyPaymentTotalAmount; set => SetProperty(ref _monthlyPaymentTotalAmount, value); }
        public decimal MonthlyPaymentApprovedAmount { get => _monthlyPaymentApprovedAmount; set => SetProperty(ref _monthlyPaymentApprovedAmount, value); }
        public decimal MonthlyPaymentPendingAmount { get => _monthlyPaymentPendingAmount; set => SetProperty(ref _monthlyPaymentPendingAmount, value); }

        // ══════════════════════════════════════════════════════════════
        // REPORT 6 — Monthly Claims
        // ══════════════════════════════════════════════════════════════
        public ObservableCollection<Claim> MonthlyClaimsData { get; } = new();

        private string _monthlyClaimsSearch = string.Empty;
        private string _monthlyClaimsStatus = "All";
        private string _monthlyClaimsMonth = "All";
        private int? _monthlyClaimsYear;
        private bool _monthlyClaimsLoading;
        private int _monthlyClaimsCount;

        public string MonthlyClaimsSearch
        {
            get => _monthlyClaimsSearch;
            set { SetProperty(ref _monthlyClaimsSearch, value); _ = LoadMonthlyClaimsAsync(); }
        }
        public string MonthlyClaimsStatus
        {
            get => _monthlyClaimsStatus;
            set { SetProperty(ref _monthlyClaimsStatus, value); _ = LoadMonthlyClaimsAsync(); }
        }
        public string MonthlyClaimsMonth
        {
            get => _monthlyClaimsMonth;
            set { SetProperty(ref _monthlyClaimsMonth, value); _ = LoadMonthlyClaimsAsync(); }
        }
        public int? MonthlyClaimsYear
        {
            get => _monthlyClaimsYear;
            set { SetProperty(ref _monthlyClaimsYear, value); _ = LoadMonthlyClaimsAsync(); }
        }
        public bool MonthlyClaimsLoading { get => _monthlyClaimsLoading; set => SetProperty(ref _monthlyClaimsLoading, value); }
        public int MonthlyClaimsCount { get => _monthlyClaimsCount; set => SetProperty(ref _monthlyClaimsCount, value); }

        public string[] ClaimStatusOptions { get; } =
            { "All", "Draft", "Submitted", "Under Review", "Approved", "Partially Approved", "Released", "Rejected" };

        private decimal _monthlyClaimsTotalClaimed;
        private decimal _monthlyClaimsTotalApproved;
        private decimal _monthlyClaimsTotalReleased;

        public decimal MonthlyClaimsTotalClaimed { get => _monthlyClaimsTotalClaimed; set => SetProperty(ref _monthlyClaimsTotalClaimed, value); }
        public decimal MonthlyClaimsTotalApproved { get => _monthlyClaimsTotalApproved; set => SetProperty(ref _monthlyClaimsTotalApproved, value); }
        public decimal MonthlyClaimsTotalReleased { get => _monthlyClaimsTotalReleased; set => SetProperty(ref _monthlyClaimsTotalReleased, value); }

        // ══════════════════════════════════════════════════════════════
        // REPORT 7 — Member Reports
        // ══════════════════════════════════════════════════════════════
        public ObservableCollection<MemberReportRow> MemberReportsData { get; } = new();

        private string _memberReportsSearch = string.Empty;
        private string _memberReportsProgram = "All";
        private string _memberReportsStatus = "All";
        private bool _memberReportsLoading;
        private int _memberReportsCount;

        public string MemberReportsSearch
        {
            get => _memberReportsSearch;
            set { SetProperty(ref _memberReportsSearch, value); _ = LoadMemberReportsAsync(); }
        }
        public string MemberReportsProgram
        {
            get => _memberReportsProgram;
            set { SetProperty(ref _memberReportsProgram, value); _ = LoadMemberReportsAsync(); }
        }
        public string MemberReportsStatus
        {
            get => _memberReportsStatus;
            set { SetProperty(ref _memberReportsStatus, value); _ = LoadMemberReportsAsync(); }
        }
        public bool MemberReportsLoading { get => _memberReportsLoading; set => SetProperty(ref _memberReportsLoading, value); }
        public int MemberReportsCount { get => _memberReportsCount; set => SetProperty(ref _memberReportsCount, value); }

        public string[] MemberProgramOptions { get; } = { "All", "Job Order", "Casual", "Regular", "Captain" };
        public string[] MemberStatusOptions { get; } =
            { "All", "Pending", "Under Review", "Verified", "Approved", "Released", "Rejected", "Archived" };

        private decimal _memberReportsTotalClaimed;
        private decimal _memberReportsTotalPayments;

        public decimal MemberReportsTotalClaimed { get => _memberReportsTotalClaimed; set => SetProperty(ref _memberReportsTotalClaimed, value); }
        public decimal MemberReportsTotalPayments { get => _memberReportsTotalPayments; set => SetProperty(ref _memberReportsTotalPayments, value); }

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

        public RelayCommand RefreshMonthlyPaymentCommand { get; }
        public RelayCommand ExportMonthlyPaymentPdfCommand { get; }
        public RelayCommand ExportMonthlyPaymentXlsCommand { get; }
        public RelayCommand ExportMonthlyPaymentWordCommand { get; }

        public RelayCommand RefreshMonthlyClaimsCommand { get; }
        public RelayCommand ExportMonthlyClaimsPdfCommand { get; }
        public RelayCommand ExportMonthlyClaimsXlsCommand { get; }
        public RelayCommand ExportMonthlyClaimsWordCommand { get; }

        public RelayCommand RefreshMemberReportsCommand { get; }
        public RelayCommand ExportMemberReportsPdfCommand { get; }
        public RelayCommand ExportMemberReportsXlsCommand { get; }
        public RelayCommand ExportMemberReportsWordCommand { get; }

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

            RefreshMonthlyPaymentCommand = new RelayCommand(async () => await LoadMonthlyPaymentAsync());
            ExportMonthlyPaymentPdfCommand = new RelayCommand(() =>
                ReportExportService.ExportMonthlyPaymentPdf(
                    MonthlyPaymentData, BuildTitle("Monthly Payment Report")));
            ExportMonthlyPaymentXlsCommand = new RelayCommand(() =>
                ReportExportService.ExportMonthlyPaymentExcel(
                    MonthlyPaymentData, BuildTitle("Monthly Payment Report")));
            ExportMonthlyPaymentWordCommand = new RelayCommand(() =>
                ReportExportService.ExportMonthlyPaymentWord(
                    MonthlyPaymentData, BuildTitle("Monthly Payment Report")));

            RefreshMonthlyClaimsCommand = new RelayCommand(async () => await LoadMonthlyClaimsAsync());
            ExportMonthlyClaimsPdfCommand = new RelayCommand(() =>
                ReportExportService.ExportMonthlyClaimsPdf(
                    MonthlyClaimsData, BuildTitle("Monthly Claims Report")));
            ExportMonthlyClaimsXlsCommand = new RelayCommand(() =>
                ReportExportService.ExportMonthlyClaimsExcel(
                    MonthlyClaimsData, BuildTitle("Monthly Claims Report")));
            ExportMonthlyClaimsWordCommand = new RelayCommand(() =>
                ReportExportService.ExportMonthlyClaimsWord(
                    MonthlyClaimsData, BuildTitle("Monthly Claims Report")));

            RefreshMemberReportsCommand = new RelayCommand(async () => await LoadMemberReportsAsync());
            ExportMemberReportsPdfCommand = new RelayCommand(() =>
                ReportExportService.ExportMemberReportsPdf(
                    MemberReportsData, BuildTitle("Member Report")));
            ExportMemberReportsXlsCommand = new RelayCommand(() =>
                ReportExportService.ExportMemberReportsExcel(
                    MemberReportsData, BuildTitle("Member Report")));
            ExportMemberReportsWordCommand = new RelayCommand(() =>
                ReportExportService.ExportMemberReportsWord(
                    MemberReportsData, BuildTitle("Member Report")));

            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);

            // Load all on startup
            _ = LoadCoverageAsync();
            _ = LoadClaimsAsync();
            _ = LoadPremiumAsync();
            _ = LoadBenefitAsync();
            _ = LoadMonthlyPaymentAsync();
            _ = LoadMonthlyClaimsAsync();
            _ = LoadMemberReportsAsync();
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

        private async Task LoadMonthlyPaymentAsync()
        {
            MonthlyPaymentLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var q = db.Payments.AsQueryable();

                if (MonthlyPaymentStatus != "All")
                    q = q.Where(p => p.Status == MonthlyPaymentStatus);
                if (MonthlyPaymentGroup != "All")
                    q = q.Where(p => p.SourceOfFunds == MonthlyPaymentGroup);
                if (MonthlyPaymentMonth != "All")
                {
                    int monthNum = Array.IndexOf(MonthOptions, MonthlyPaymentMonth);
                    if (monthNum > 0) q = q.Where(p => p.BillingMonth.Month == monthNum);
                }
                if (MonthlyPaymentYear.HasValue)
                    q = q.Where(p => p.BillingMonth.Year == MonthlyPaymentYear.Value);

                var data = await q.OrderByDescending(p => p.BillingMonth)
                                   .ThenBy(p => p.MemberName)
                                   .ToListAsync();

                if (!string.IsNullOrWhiteSpace(MonthlyPaymentSearch))
                {
                    var s = MonthlyPaymentSearch.Trim().ToLower();
                    data = data.Where(p =>
                        p.MemberName.ToLower().Contains(s) ||
                        (p.FamilyId?.ToLower().Contains(s) ?? false)).ToList();
                }

                MonthlyPaymentData.Clear();
                foreach (var p in data) MonthlyPaymentData.Add(p);
                MonthlyPaymentCount = MonthlyPaymentData.Count;
                MonthlyPaymentTotalAmount = MonthlyPaymentData.Sum(p => p.Amount);
                MonthlyPaymentApprovedAmount = MonthlyPaymentData.Where(p => p.Status == "Approved").Sum(p => p.Amount);
                MonthlyPaymentPendingAmount = MonthlyPaymentData.Where(p => p.Status == "Pending").Sum(p => p.Amount);
            }
            catch (Exception ex)
            {
                App.ReportError("Load Monthly Payment Report Failed", ex);
            }
            finally { MonthlyPaymentLoading = false; }
        }

        private async Task LoadMonthlyClaimsAsync()
        {
            MonthlyClaimsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var q = db.Claims
                    .Include(c => c.Employee)
                    .Include(c => c.Beneficiary)
                    .AsQueryable();

                if (MonthlyClaimsStatus != "All")
                    q = q.Where(c => c.ClaimStatus == MonthlyClaimsStatus);
                if (MonthlyClaimsMonth != "All")
                {
                    int monthNum = Array.IndexOf(MonthOptions, MonthlyClaimsMonth);
                    if (monthNum > 0) q = q.Where(c => c.ClaimDate.HasValue && c.ClaimDate.Value.Month == monthNum);
                }
                if (MonthlyClaimsYear.HasValue)
                    q = q.Where(c => c.ClaimDate.HasValue && c.ClaimDate.Value.Year == MonthlyClaimsYear.Value);

                var data = await q.OrderByDescending(c => c.ClaimDate).ToListAsync();

                if (!string.IsNullOrWhiteSpace(MonthlyClaimsSearch))
                {
                    var s = MonthlyClaimsSearch.Trim().ToLower();
                    data = data.Where(c =>
                        (c.Employee?.FullName?.ToLower().Contains(s) ?? false) ||
                        (c.Beneficiary?.FullName.ToLower().Contains(s) ?? false) ||
                        c.ClaimNo.ToLower().Contains(s)).ToList();
                }

                MonthlyClaimsData.Clear();
                foreach (var c in data) MonthlyClaimsData.Add(c);
                MonthlyClaimsCount = MonthlyClaimsData.Count;
                MonthlyClaimsTotalClaimed = MonthlyClaimsData.Sum(c => c.AmountClaimed);
                MonthlyClaimsTotalApproved = MonthlyClaimsData.Sum(c => c.AmountApproved);
                MonthlyClaimsTotalReleased = MonthlyClaimsData.Sum(c => c.AmountReleased);
            }
            catch (Exception ex)
            {
                App.ReportError("Load Monthly Claims Report Failed", ex);
            }
            finally { MonthlyClaimsLoading = false; }
        }

        private async Task LoadMemberReportsAsync()
        {
            MemberReportsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var q = db.Beneficiaries.Include(b => b.Employee).AsQueryable();

                if (MemberReportsProgram != "All")
                    q = q.Where(b => b.SourceOfFunds == MemberReportsProgram);
                if (MemberReportsStatus != "All")
                    q = q.Where(b => b.WorkflowStatus == MemberReportsStatus);

                var beneficiaries = await q.OrderByDescending(b => b.CreatedAt).ToListAsync();
                var claims = await db.Claims.ToListAsync();
                var payments = await db.Payments.ToListAsync();

                var data = beneficiaries.Select(b =>
                {
                    var benClaims = claims.Where(c => c.BenId == b.BenId).ToList();
                    var benPayments = payments.Where(p => p.BeneficiaryId == b.BenId).ToList();
                    return new MemberReportRow
                    {
                        BeneficiaryCode = b.BeneficiaryId ?? string.Empty,
                        FullName = b.FullName,
                        HouseholdId = b.Employee?.EmployeeNo ?? string.Empty,
                        Relationship = b.Relationship ?? string.Empty,
                        Program = b.SourceOfFunds ?? string.Empty,
                        Status = b.WorkflowStatus,
                        TotalClaims = benClaims.Count,
                        TotalClaimed = benClaims.Sum(c => c.AmountClaimed),
                        TotalPayments = benPayments.Count,
                        TotalPaymentAmount = benPayments.Sum(p => p.Amount),
                        CreatedAt = b.CreatedAt
                    };
                }).ToList();

                if (!string.IsNullOrWhiteSpace(MemberReportsSearch))
                {
                    var s = MemberReportsSearch.Trim().ToLower();
                    data = data.Where(r =>
                        r.FullName.ToLower().Contains(s) ||
                        r.BeneficiaryCode.ToLower().Contains(s) ||
                        r.HouseholdId.ToLower().Contains(s)).ToList();
                }

                MemberReportsData.Clear();
                foreach (var r in data) MemberReportsData.Add(r);
                MemberReportsCount = MemberReportsData.Count;
                MemberReportsTotalClaimed = MemberReportsData.Sum(r => r.TotalClaimed);
                MemberReportsTotalPayments = MemberReportsData.Sum(r => r.TotalPaymentAmount);
            }
            catch (Exception ex)
            {
                App.ReportError("Load Member Reports Failed", ex);
            }
            finally { MemberReportsLoading = false; }
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
