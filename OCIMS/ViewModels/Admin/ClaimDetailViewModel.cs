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
    public class ClaimDetailViewModel : ObservableObject
    {
        // ── Claim ──────────────────────────────────────────────────────
        private Claim? _claim;
        public Claim? Claim
        {
            get => _claim;
            set
            {
                SetProperty(ref _claim, value);
                OnPropertyChanged(nameof(ClaimStatus));
                OnPropertyChanged(nameof(CanSubmit));
                OnPropertyChanged(nameof(CanReview));
                OnPropertyChanged(nameof(CanApprove));
                OnPropertyChanged(nameof(CanReject));
                OnPropertyChanged(nameof(CanRelease));
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(ShowApprovalFields));
                OnPropertyChanged(nameof(ShowRejectionReason));
            }
        }

        public string ClaimStatus => Claim?.ClaimStatus ?? string.Empty;

        // ── Read Only Mode ─────────────────────────────────────────────
        public bool IsReadOnly { get; set; } = false;

        // ── Workflow Visibility ────────────────────────────────────────
        public bool CanSubmit => !IsReadOnly && ClaimStatus == "Draft";
        public bool CanReview => !IsReadOnly && ClaimStatus == "Submitted";
        public bool CanApprove => !IsReadOnly && ClaimStatus == WorkflowStatuses.UnderReview;
        public bool CanReject => !IsReadOnly && ClaimStatus == WorkflowStatuses.UnderReview;
        public bool CanRelease => !IsReadOnly && (ClaimStatus == WorkflowStatuses.Approved || ClaimStatus == "Partially Approved");
        public bool CanCancel => !IsReadOnly && (ClaimStatus == "Draft" || ClaimStatus == "Submitted");
        public bool ShowApprovalFields => CanApprove;
        public bool ShowRejectionReason => ClaimStatus == WorkflowStatuses.Rejected;

        // ── Approval Fields ────────────────────────────────────────────
        private decimal _amountApproved;
        private string _rejectionReason = string.Empty;
        private decimal _amountReleased;
        private string _sourceOfFunds = "Job Order";

        public decimal AmountApproved
        {
            get => _amountApproved;
            set => SetProperty(ref _amountApproved, value);
        }
        public string RejectionReason
        {
            get => _rejectionReason;
            set => SetProperty(ref _rejectionReason, value);
        }
        public decimal AmountReleased
        {
            get => _amountReleased;
            set => SetProperty(ref _amountReleased, value);
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

        // ── Documents ──────────────────────────────────────────────────
        public ObservableCollection<ClaimDocument> Documents { get; } = new();
        public bool HasDocuments => Documents.Count > 0;
        public bool HasNoDocuments => Documents.Count == 0;

        // ── Transactions ───────────────────────────────────────────────
        public ObservableCollection<DocumentTransaction> Transactions { get; } = new();
        public bool HasTransactions => Transactions.Count > 0;

        // ── State ──────────────────────────────────────────────────────
        private string _statusMessage = string.Empty;
        private bool _isBusy;
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand SubmitCommand { get; }
        public RelayCommand ReviewCommand { get; }
        public RelayCommand ApproveCommand { get; }
        public RelayCommand RejectCommand { get; }
        public RelayCommand ReleaseCommand { get; }
        public RelayCommand CancelClaimCommand { get; }
        public RelayCommand CloseCommand { get; }

        // ── Callbacks ──────────────────────────────────────────────────
        public Action? CloseAction { get; set; }
        public Action? OnStatusChanged { get; set; }

        // ── Constructor ────────────────────────────────────────────────
        public ClaimDetailViewModel()
        {
            SubmitCommand = new RelayCommand(async () => await TransitionAsync("Submitted"));
            ReviewCommand = new RelayCommand(async () => await TransitionAsync("Under Review"));
            ApproveCommand = new RelayCommand(async () => await ApproveAsync());
            RejectCommand = new RelayCommand(async () => await RejectAsync());
            ReleaseCommand = new RelayCommand(async () => await ReleaseAsync());
            CancelClaimCommand = new RelayCommand(async () => await TransitionAsync("Cancelled"));
            CloseCommand = new RelayCommand(() => CloseAction?.Invoke());
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

        public async Task LoadAsync(int claimId)
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                Claim = await db.Claims
                    .Include(c => c.Employee).ThenInclude(e => e!.Department)
                    .Include(c => c.Policy)
                    .Include(c => c.Beneficiary)
                    .FirstOrDefaultAsync(c => c.ClaimId == claimId);

                if (Claim is null) return;

                AmountApproved = Claim.AmountApproved;
                AmountReleased = Claim.AmountReleased;
                SourceOfFunds = string.IsNullOrWhiteSpace(Claim.SourceOfFunds)
                    ? SourceOfFunds
                    : Claim.SourceOfFunds;
                RejectionReason = Claim.RejectionReason ?? string.Empty;

                var txs = await db.DocumentTransactions
                    .Where(t => (t.TransactionType == "Beneficiary Insurance Claim" ||
                                 t.TransactionType == "Insurance Claim Workflow") &&
                                t.TransactionNo.Contains(Claim.ClaimNo))
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();
                Transactions.Clear();
                foreach (var t in txs) Transactions.Add(t);
                OnPropertyChanged(nameof(HasTransactions));

                var docs = await db.ClaimDocuments
                    .Where(d => d.ClaimId == claimId)
                    .ToListAsync();
                Documents.Clear();
                foreach (var d in docs) Documents.Add(d);
                OnPropertyChanged(nameof(HasDocuments));
                OnPropertyChanged(nameof(HasNoDocuments));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load failed: {ex.Message}";
            }
        }

        // ── Status Transitions ─────────────────────────────────────────
        private async Task TransitionAsync(string newStatus)
        {
            if (Claim is null) return;

            string confirmMsg = newStatus switch
            {
                "Submitted" => "Submit this claim for review?",
                WorkflowStatuses.UnderReview => "Mark this claim as Under Review?",
                "Cancelled" => "Cancel this claim? This cannot be undone.",
                _ => $"Change status to {newStatus}?"
            };

            var result = MessageBox.Show(confirmMsg, "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            IsBusy = true;
            try
            {
                var (success, message) = await WorkflowService.UpdateClaimStatusAsync(
                    Claim.ClaimId, newStatus);

                if (!success)
                {
                    StatusMessage = message;
                    return;
                }

                await LoadAsync(Claim.ClaimId);
                OnStatusChanged?.Invoke();
            }
            catch (Exception ex) { StatusMessage = $"Failed: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        // ── Approve ────────────────────────────────────────────────────
        private async Task ApproveAsync()
        {
            if (Claim is null) return;
            if (AmountApproved <= 0)
            { StatusMessage = "Enter the approved amount first."; return; }

            string status = AmountApproved >= Claim.AmountClaimed
                ? WorkflowStatuses.Approved : "Partially Approved";

            var result = MessageBox.Show(
                $"Approve this claim for ₱{AmountApproved:N2}?\nStatus will be set to: {status}",
                "Confirm Approval", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            IsBusy = true;
            try
            {
                var (success, message) = await WorkflowService.UpdateClaimStatusAsync(
                    Claim.ClaimId, status, amountApproved: AmountApproved);

                if (!success)
                {
                    StatusMessage = message;
                    return;
                }

                await LoadAsync(Claim.ClaimId);
                OnStatusChanged?.Invoke();
            }
            catch (Exception ex) { StatusMessage = $"Failed: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        // ── Reject ─────────────────────────────────────────────────────
        private async Task RejectAsync()
        {
            if (Claim is null) return;
            if (string.IsNullOrWhiteSpace(RejectionReason))
            { StatusMessage = "Please provide a rejection reason."; return; }

            var result = MessageBox.Show(
                "Reject this beneficiary claim?",
                "Confirm Rejection", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            IsBusy = true;
            try
            {
                var (success, message) = await WorkflowService.UpdateClaimStatusAsync(
                    Claim.ClaimId, WorkflowStatuses.Rejected, remarks: RejectionReason);

                if (!success)
                {
                    StatusMessage = message;
                    return;
                }

                await LoadAsync(Claim.ClaimId);
                OnStatusChanged?.Invoke();
            }
            catch (Exception ex) { StatusMessage = $"Failed: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        // ── Release ────────────────────────────────────────────────────
        private async Task ReleaseOnlineAsync()
        {
            if (Claim is null) return;
            if (AmountReleased <= 0)
            { StatusMessage = "Enter the released amount first."; return; }

            var online = await GgmsService.CheckReleaseConnectionAsync();
            if (!online.IsOnline)
            {
                StatusMessage = online.Message;
                MessageBox.Show(
                    "Funds releasing requires an online GGMS/Hostinger connection.",
                    "Online Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Release ₱{AmountReleased:N2} for this claim?",
                "Confirm Release", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            IsBusy = true;
            try
            {
                var (success, message) = await WorkflowService.UpdateClaimStatusAsync(
                    Claim.ClaimId, 
                    WorkflowStatuses.Released, 
                    amountReleased: AmountReleased,
                    sourceOfFunds: SourceOfFunds);

                if (!success)
                {
                    StatusMessage = message;
                    MessageBox.Show(message, "Release Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // ── Write to GGMS ──────────────────────────────────────────────
                var (ggmsSuccess, ggmsMsg) = await GgmsService.RecordClaimReleaseAsync(
                    claimId: Claim.ClaimId,
                    beneficiaryIdentity: Claim.Beneficiary?.BeneficiaryId,
                    civilRegistryId: Claim.Beneficiary?.CivilRegistryId,
                    amountReleased: AmountReleased,
                    claimType: Claim.ClaimType ?? "Insurance Claim",
                    firstName: Claim.Beneficiary?.FirstName ?? Claim.Employee?.FirstName ?? string.Empty,
                    middleName: Claim.Employee?.MiddleName,
                    lastName: Claim.Beneficiary?.LastName ?? Claim.Employee?.LastName ?? string.Empty,
                    recipientName: Claim.Beneficiary?.FullName ?? Claim.Employee?.FullName ?? string.Empty,
                    claimNo: Claim.ClaimNo,
                    purpose: "Insurance Claim",
                    sourceOfFunds: SourceOfFunds
                );

                if (!ggmsSuccess)
                {
                    StatusMessage = $"GGMS Error: {ggmsMsg}";
                    MessageBox.Show(StatusMessage, "GGMS Recording Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    StatusMessage = "Released successfully and recorded in GGMS.";
                }

                await LoadAsync(Claim.ClaimId);
                OnStatusChanged?.Invoke();
            }
            catch (Exception ex) { StatusMessage = $"Failed: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        private async Task ReleaseAsync()
        {
            if (Claim is null) return;
            if (AmountReleased <= 0)
            { StatusMessage = "Enter the released amount first."; return; }

            var online = await GgmsService.CheckReleaseConnectionAsync();
            if (!online.IsOnline)
            {
                StatusMessage = online.Message;
                MessageBox.Show(
                    "Funds releasing requires an online GGMS/Hostinger connection. This claim was not released locally.",
                    "Online Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Release ₱{AmountReleased:N2} for this claim?",
                "Confirm Release", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            IsBusy = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var c = await db.Claims.FindAsync(Claim.ClaimId);
                if (c is null) return;
                c.ClaimStatus = "Released";
                c.AmountReleased = AmountReleased;
                c.SourceOfFunds = SourceOfFunds;
                c.ReleasedDate = DateTime.Now;
                c.UpdatedAt = DateTime.Now;
                var localFundCoveredRelease = await ApplySourceFundUsageAsync(db, SourceOfFunds, AmountReleased);
                if (!localFundCoveredRelease)
                {
                    StatusMessage = $"Local {SourceOfFunds} fund is missing or has insufficient remaining balance.";
                    MessageBox.Show(StatusMessage, "Local Fund Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                await db.SaveChangesAsync();

                // In ReleaseAsync — after SaveChangesAsync:
                await AuditService.LogUpdate("claims", c.ClaimId,
                    $"Claim {c.ClaimNo} released ₱{AmountReleased:N2}");

                // ── Write to GGMS ──────────────────────────────────────────────
                var (ggmsSuccess, ggmsMsg) = await GgmsService.RecordClaimReleaseAsync(
                    claimId: c.ClaimId,
                    beneficiaryIdentity: Claim.Beneficiary?.BeneficiaryId,
                    civilRegistryId: Claim.Beneficiary?.CivilRegistryId,
                    amountReleased: AmountReleased,
                    claimType: c.ClaimType ?? "Insurance Claim",
                    firstName: Claim.Beneficiary?.FirstName ?? Claim.Employee?.FirstName ?? string.Empty,
                    middleName: Claim.Employee?.MiddleName,
                    lastName: Claim.Beneficiary?.LastName ?? Claim.Employee?.LastName ?? string.Empty,
                    recipientName: Claim.Beneficiary?.FullName ?? Claim.Employee?.FullName ?? string.Empty,
                    claimNo: c.ClaimNo,
                    purpose: "Insurance Claim",
                    sourceOfFunds: SourceOfFunds
                );

                if (!ggmsSuccess)
                {
                    StatusMessage = $"GGMS Error: {ggmsMsg}";
                    MessageBox.Show(StatusMessage, "GGMS Recording Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    StatusMessage = $"Released from {SourceOfFunds} and recorded in GGMS.";
                }

                await LoadAsync(Claim.ClaimId);
                OnStatusChanged?.Invoke();
            }
            catch (Exception ex) { StatusMessage = $"Failed: {ex.Message}"; }
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
