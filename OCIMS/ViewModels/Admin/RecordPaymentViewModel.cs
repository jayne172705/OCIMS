using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace eSureHi.ViewModels.Admin
{
    public class RecordPaymentViewModel : ObservableObject
    {
        private readonly int _premiumId;

        // ── Display Info (read-only) ───────────────────────────────────
        public string EmployeeName { get; private set; } = string.Empty;
        public string PolicyName { get; private set; } = string.Empty;
        public string BillingMonth { get; private set; } = string.Empty;
        public decimal TotalAmount { get; private set; }
        public decimal AlreadyPaid { get; private set; }
        public decimal Balance { get; private set; }

        // ── Payment Fields ─────────────────────────────────────────────
        private decimal _amountPaid;
        private string _paymentMode = "Payroll Deduction";
        private DateTime? _paidDate = DateTime.Today;
        private string _referenceNo = string.Empty;
        private decimal _lateFee;
        private string _remarks = string.Empty;

        public decimal AmountPaid
        {
            get => _amountPaid;
            set => SetProperty(ref _amountPaid, value);
        }
        public string PaymentMode
        {
            get => _paymentMode;
            set => SetProperty(ref _paymentMode, value);
        }
        public DateTime? PaidDate
        {
            get => _paidDate;
            set => SetProperty(ref _paidDate, value);
        }
        public string ReferenceNo
        {
            get => _referenceNo;
            set => SetProperty(ref _referenceNo, value);
        }
        public decimal LateFee
        {
            get => _lateFee;
            set => SetProperty(ref _lateFee, value);
        }
        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }

        public string[] PaymentModes { get; } =
            { "Payroll Deduction", "Bank Transfer", "Cash", "Check", "Online" };

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
        public RecordPaymentViewModel(int premiumId)
        {
            _premiumId = premiumId;
            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !IsBusy);
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
            _ = LoadAsync();
        }

        // ── Load ───────────────────────────────────────────────────────
        private async Task LoadAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var p = await db.Premiums
                    .Include(x => x.EmployeePolicy)
                        .ThenInclude(ep => ep!.Employee)
                    .Include(x => x.EmployeePolicy)
                        .ThenInclude(ep => ep!.Policy)
                    .FirstOrDefaultAsync(x => x.PremiumId == _premiumId);

                if (p is null) return;

                EmployeeName = p.EmployeePolicy?.Employee?.FullName ?? string.Empty;
                PolicyName = p.EmployeePolicy?.Policy?.PolicyName ?? string.Empty;
                BillingMonth = p.BillingMonth.ToString("MMMM yyyy");
                TotalAmount = p.TotalAmount;
                AlreadyPaid = p.AmountPaid;
                Balance = p.Balance;
                AmountPaid = p.Balance; // pre-fill with remaining balance

                OnPropertyChanged(nameof(EmployeeName));
                OnPropertyChanged(nameof(PolicyName));
                OnPropertyChanged(nameof(BillingMonth));
                OnPropertyChanged(nameof(TotalAmount));
                OnPropertyChanged(nameof(AlreadyPaid));
                OnPropertyChanged(nameof(Balance));
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Load failed: {ex.Message}";
            }
        }

        // ── Save ───────────────────────────────────────────────────────
        private async Task SaveAsync()
        {
            if (AmountPaid <= 0)
            { ErrorMessage = "Amount paid must be greater than zero."; return; }
            if (LateFee < 0)
            { ErrorMessage = "Late fee cannot be negative."; return; }
            if (PaidDate is null)
            { ErrorMessage = "Payment date is required."; return; }
            if (AmountPaid > Balance + LateFee)
            { ErrorMessage = $"Amount paid exceeds the amount due of ₱{Balance + LateFee:N2}."; return; }

            IsBusy = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var p = await db.Premiums.FindAsync(_premiumId);
                if (p is null) { ErrorMessage = "Record not found."; return; }

                decimal newTotalPaid = p.AmountPaid + AmountPaid;
                decimal totalDue = p.TotalAmount + LateFee;
                decimal newBalance = totalDue - newTotalPaid;

                p.AmountPaid = newTotalPaid;
                p.Balance = Math.Max(0, newBalance);
                p.PaidDate = DateOnly.FromDateTime(PaidDate.Value);
                p.PaymentMode = PaymentMode;
                p.ReferenceNo = string.IsNullOrWhiteSpace(ReferenceNo)
                                      ? null : ReferenceNo.Trim();
                p.LateFee = LateFee;
                p.Remarks = string.IsNullOrWhiteSpace(Remarks)
                                      ? null : Remarks.Trim();
                p.UpdatedAt = DateTime.Now;

                // Determine payment status
                if (newBalance <= 0)
                    p.PaymentStatus = "Paid";
                else if (newTotalPaid > 0)
                    p.PaymentStatus = "Partial";

                // Mark as Late if paid after due date
                if (p.PaidDate > p.DueDate && p.PaymentStatus == "Paid")
                    p.PaymentStatus = "Late";

                await db.SaveChangesAsync();

                await AuditService.LogUpdate("premiums", _premiumId,
                    $"Premium payment recorded: ₱{AmountPaid:N2}");

                OnSaveSuccess?.Invoke();
                CloseAction?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Save failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
