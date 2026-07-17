using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace eSureHi.ViewModels.Admin
{
    public class SchedulePreviewItem
    {
        public string EmployeeName { get; set; } = string.Empty;
        public string PolicyName { get; set; } = string.Empty;
        public string BillingMonth { get; set; } = string.Empty;
        public decimal EmployeeAmount { get; set; }
        public decimal EmployerAmount { get; set; }
        public decimal TotalAmount => EmployeeAmount + EmployerAmount;
        public DateOnly DueDate { get; set; }
        public bool AlreadyExists { get; set; }
    }

    public class GenerateScheduleViewModel : ObservableObject
    {
        // ── Date Range ─────────────────────────────────────────────────
        private DateTime _fromMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        private DateTime _toMonth = new(DateTime.Today.Year, 12, 1);

        public DateTime FromMonth
        {
            get => _fromMonth;
            set { SetProperty(ref _fromMonth, value); _ = PreviewAsync(); }
        }
        public DateTime ToMonth
        {
            get => _toMonth;
            set { SetProperty(ref _toMonth, value); _ = PreviewAsync(); }
        }

        // ── Preview ────────────────────────────────────────────────────
        public ObservableCollection<SchedulePreviewItem> PreviewItems { get; } = new();

        private int _newCount;
        private int _skipCount;
        private decimal _totalAmount;
        private bool _isLoading;
        private bool _hasPreview;

        public int NewCount { get => _newCount; set => SetProperty(ref _newCount, value); }
        public int SkipCount { get => _skipCount; set => SetProperty(ref _skipCount, value); }
        public decimal TotalAmount { get => _totalAmount; set => SetProperty(ref _totalAmount, value); }
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
        public bool HasPreview { get => _hasPreview; set => SetProperty(ref _hasPreview, value); }

        // ── State ──────────────────────────────────────────────────────
        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;

        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand GenerateCommand { get; }
        public RelayCommand CancelCommand { get; }

        public Action? CloseAction { get; set; }
        public Action? OnGenerated { get; set; }

        // ── Constructor ────────────────────────────────────────────────
        public GenerateScheduleViewModel()
        {
            GenerateCommand = new RelayCommand(
                async () => await GenerateAsync(),
                () => !IsLoading && NewCount > 0);
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());

            _ = PreviewAsync();
        }

        // ── Preview ────────────────────────────────────────────────────
        public async Task PreviewAsync()
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            PreviewItems.Clear();

            try
            {
                using var db = eSureHiDbContextFactory.Create();

                var assignments = await db.EmployeePolicies
                    .Include(ep => ep.Employee)
                    .Include(ep => ep.Policy)
                    .Where(ep => ep.AssignmentStatus == "Active")
                    .ToListAsync();

                if (!assignments.Any())
                {
                    ErrorMessage = "No active policy assignments found.";
                    HasPreview = false;
                    return;
                }

                var from = DateOnly.FromDateTime(
                    new DateTime(FromMonth.Year, FromMonth.Month, 1));
                var to = DateOnly.FromDateTime(
                    new DateTime(ToMonth.Year, ToMonth.Month, 1));

                if (from > to)
                {
                    ErrorMessage = "From month must be before or equal to To month.";
                    HasPreview = false;
                    return;
                }

                // Get existing premiums for this range
                var existing = await db.Premiums
                    .Where(p => p.BillingMonth >= from && p.BillingMonth <= to)
                    .Select(p => new { p.EpId, p.BillingMonth })
                    .ToListAsync();

                var current = from;
                while (current <= to)
                {
                    foreach (var ep in assignments)
                    {
                        bool exists = existing.Any(e =>
                            e.EpId == ep.EpId && e.BillingMonth == current);

                        PreviewItems.Add(new SchedulePreviewItem
                        {
                            EmployeeName = ep.Employee?.FullName ?? string.Empty,
                            PolicyName = ep.Policy?.PolicyName ?? string.Empty,
                            BillingMonth = current.ToString("MMM yyyy"),
                            EmployeeAmount = ep.EmployeeShare,
                            EmployerAmount = ep.EmployerShare,
                            DueDate = new DateOnly(
                                                current.Year,
                                                current.Month,
                                                DateTime.DaysInMonth(
                                                    current.Year, current.Month)),
                            AlreadyExists = exists
                        });
                    }
                    current = current.AddMonths(1);
                }

                NewCount = PreviewItems.Count(x => !x.AlreadyExists);
                SkipCount = PreviewItems.Count(x => x.AlreadyExists);
                TotalAmount = PreviewItems
                    .Where(x => !x.AlreadyExists)
                    .Sum(x => x.TotalAmount);
                HasPreview = PreviewItems.Any();

                GenerateCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Preview failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Generate ───────────────────────────────────────────────────
        private async Task GenerateAsync()
        {
            var result = MessageBox.Show(
                $"Generate {NewCount} premium record(s)?\n" +
                $"{SkipCount} existing record(s) will be skipped.",
                "Confirm Generate",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            IsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();

                var assignments = await db.EmployeePolicies
                    .Where(ep => ep.AssignmentStatus == "Active")
                    .ToListAsync();

                var from = DateOnly.FromDateTime(
                    new DateTime(FromMonth.Year, FromMonth.Month, 1));
                var to = DateOnly.FromDateTime(
                    new DateTime(ToMonth.Year, ToMonth.Month, 1));

                var existing = await db.Premiums
                    .Where(p => p.BillingMonth >= from && p.BillingMonth <= to)
                    .Select(p => new { p.EpId, p.BillingMonth })
                    .ToListAsync();

                int generated = 0;
                var current = from;

                while (current <= to)
                {
                    foreach (var ep in assignments)
                    {
                        bool exists = existing.Any(e =>
                            e.EpId == ep.EpId && e.BillingMonth == current);
                        if (exists) continue;

                        var dueDate = new DateOnly(
                            current.Year, current.Month,
                            DateTime.DaysInMonth(current.Year, current.Month));

                        var total = ep.EmployeeShare + ep.EmployerShare;

                        db.Premiums.Add(new Premium
                        {
                            EpId = ep.EpId,
                            BillingMonth = current,
                            EmployeeAmount = ep.EmployeeShare,
                            EmployerAmount = ep.EmployerShare,
                            DueDate = dueDate,
                            PaymentStatus = "Unpaid",
                            AmountPaid = 0,
                            Balance = total,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        });
                        generated++;
                    }
                    current = current.AddMonths(1);
                }

                await db.SaveChangesAsync();

                await AuditService.LogInsert("premiums", 0,
                $"Premium schedule generated — {generated} records");

                StatusMessage = $"✔ Successfully generated {generated} premium record(s).";
                OnGenerated?.Invoke();
                GenerateCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Generate failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
