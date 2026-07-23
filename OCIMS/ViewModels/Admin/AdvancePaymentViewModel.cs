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
    public class AdvancePaymentViewModel : ObservableObject
    {
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { SetProperty(ref _searchText, value); _ = SearchAsync(); }
        }

        public ObservableCollection<Beneficiary> SearchResults { get; } = new();
        public bool HasSearchResults => SearchResults.Count > 0;

        private Beneficiary? _selectedBeneficiary;
        public Beneficiary? SelectedBeneficiary
        {
            get => _selectedBeneficiary;
            set
            {
                SetProperty(ref _selectedBeneficiary, value);
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(SelectedFamilyLabel));
                if (value != null) Amount = value.Contribution;
                RecordPaymentCommand.RaiseCanExecuteChanged();
            }
        }
        public bool HasSelection => SelectedBeneficiary is not null;
        public string SelectedFamilyLabel =>
            SelectedBeneficiary is null
                ? string.Empty
                : $"{SelectedBeneficiary.FullName} · {(string.IsNullOrWhiteSpace(SelectedBeneficiary.SourceOfFunds) ? "No Program" : SelectedBeneficiary.SourceOfFunds)}";

        public int[] MonthsOptions { get; } = { 1, 2, 3, 4, 5, 6 };

        private int _selectedMonths = 1;
        public int SelectedMonths
        {
            get => _selectedMonths;
            set => SetProperty(ref _selectedMonths, value);
        }

        private decimal _amount;
        public decimal Amount
        {
            get => _amount;
            set { SetProperty(ref _amount, value); RecordPaymentCommand.RaiseCanExecuteChanged(); }
        }

        public ObservableCollection<Payment> RecentPayments { get; } = new();
        public bool HasRecentPayments => RecentPayments.Count > 0;

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        private string _statusMessage = string.Empty;
        public string StatusMessage { get => _statusMessage; set { SetProperty(ref _statusMessage, value); OnPropertyChanged(nameof(HasStatusMessage)); } }
        public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

        public RelayCommand<Beneficiary> SelectBeneficiaryCommand { get; }
        public RelayCommand RecordPaymentCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

        public AdvancePaymentViewModel()
        {
            SelectBeneficiaryCommand = new RelayCommand<Beneficiary>(b => SelectedBeneficiary = b);
            RecordPaymentCommand = new RelayCommand(async () => await RecordPaymentAsync(),
                () => SelectedBeneficiary is not null && Amount > 0);
            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);

            _ = LoadRecentAsync();
        }

        private async Task SearchAsync()
        {
            SearchResults.Clear();
            if (string.IsNullOrWhiteSpace(SearchText) || SearchText.Trim().Length < 2)
            {
                OnPropertyChanged(nameof(HasSearchResults));
                return;
            }

            var s = SearchText.Trim().ToLower();
            using var db = eSureHiDbContextFactory.Create();
            var matches = await db.Beneficiaries
                .Where(b => b.IsActive &&
                    ((b.FirstName + " " + b.LastName).ToLower().Contains(s) ||
                     (b.BeneficiaryId != null && b.BeneficiaryId.ToLower().Contains(s))))
                .OrderBy(b => b.FirstName)
                .Take(20)
                .ToListAsync();

            foreach (var m in matches) SearchResults.Add(m);
            OnPropertyChanged(nameof(HasSearchResults));
        }

        private async Task LoadRecentAsync()
        {
            using var db = eSureHiDbContextFactory.Create();
            var recent = await db.Payments
                .Where(p => p.PaymentType == "Advance")
                .OrderByDescending(p => p.CreatedAt)
                .Take(15)
                .ToListAsync();

            RecentPayments.Clear();
            foreach (var p in recent) RecentPayments.Add(p);
            OnPropertyChanged(nameof(HasRecentPayments));
        }

        private async Task RecordPaymentAsync()
        {
            if (SelectedBeneficiary is null || Amount <= 0) return;

            IsBusy = true;
            StatusMessage = string.Empty;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var beneficiary = await db.Beneficiaries.FirstOrDefaultAsync(b => b.BenId == SelectedBeneficiary.BenId);
                if (beneficiary is null)
                {
                    StatusMessage = "Selected beneficiary no longer exists.";
                    return;
                }

                string? familyId = null;
                if (!string.IsNullOrWhiteSpace(beneficiary.CivilRegistryId))
                {
                    var cacheRow = await db.CrsBeneficiaryCache
                        .FirstOrDefaultAsync(c => c.CivilRegistryId == beneficiary.CivilRegistryId);
                    familyId = cacheRow?.FamilyId;
                }

                var startMonth = new DateOnly(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(1);
                var username = AuthService.Instance.CurrentUser?.Username ?? "system";
                var created = new System.Collections.Generic.List<Payment>();

                for (int i = 0; i < SelectedMonths; i++)
                {
                    var payment = new Payment
                    {
                        BeneficiaryId = beneficiary.BenId,
                        FamilyId = familyId,
                        MemberName = beneficiary.FullName,
                        Relationship = beneficiary.Relationship,
                        BillingMonth = startMonth.AddMonths(i),
                        Amount = Amount,
                        Status = "Approved",
                        PaymentType = "Advance",
                        SourceOfFunds = beneficiary.SourceOfFunds,
                        PaidAt = DateOnly.FromDateTime(DateTime.Now),
                        CreatedBy = username,
                        Remarks = $"Advance payment {i + 1} of {SelectedMonths} month(s)"
                    };
                    db.Payments.Add(payment);
                    created.Add(payment);
                }

                await db.SaveChangesAsync();
                foreach (var p in created)
                    await AuditService.LogInsert("payments", p.PaymentId,
                        $"Advance payment recorded for {p.MemberName} — {p.BillingMonth:MMM yyyy} (₱{p.Amount:N2})");

                StatusMessage = $"Recorded {SelectedMonths} month(s) of advance payment for {beneficiary.FullName}.";
                await LoadRecentAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Record payment failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        private void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(new Views.Admin.UserControls.HomeView());
        }
    }
}
