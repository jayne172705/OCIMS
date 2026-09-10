using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;

namespace eSureHi.ViewModels.Admin
{
    public class BeneficiaryPortalViewModel : ObservableObject
    {
        private Beneficiary? _beneficiary;
        private string _email = string.Empty;
        private string _currentPassword = string.Empty;
        private string _newPassword = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _isBusy;
        private bool _hasActiveClaim;

        public Beneficiary? Beneficiary { get => _beneficiary; set => SetProperty(ref _beneficiary, value); }
        public string Email { get => _email; set => SetProperty(ref _email, value); }
        public string CurrentPassword { get => _currentPassword; set => SetProperty(ref _currentPassword, value); }
        public string NewPassword { get => _newPassword; set => SetProperty(ref _newPassword, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        public bool HasActiveClaim { get => _hasActiveClaim; set => SetProperty(ref _hasActiveClaim, value); }
        public string ActiveClaimStatusText => "You already have a claim in progress. You can file a new one once it's resolved.";
        public string DisplayName => Beneficiary?.FullName ?? AuthService.Instance.CurrentUser?.Username ?? "Beneficiary";
        public ObservableCollection<Claim> Claims { get; } = new();

        public RelayCommand RefreshCommand { get; }
        public RelayCommand SubmitClaimCommand { get; }
        public RelayCommand ViewDistributionStatusCommand { get; }
        public RelayCommand SaveEmailCommand { get; }
        public RelayCommand ChangePasswordCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

        public BeneficiaryPortalViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            SubmitClaimCommand = new RelayCommand(OpenSubmitClaim);
            ViewDistributionStatusCommand = new RelayCommand(OpenDistributionStatus);
            SaveEmailCommand = new RelayCommand(async () => await SaveEmailAsync());
            ChangePasswordCommand = new RelayCommand(async () => await ChangePasswordAsync());
            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);
            _ = LoadAsync();
        }

        private void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(new Views.Admin.UserControls.HomeView());
        }

        public async Task LoadAsync()
        {
            IsBusy = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var benId = AuthService.Instance.CurrentUser?.BenId ?? 0;
                Beneficiary = await db.Beneficiaries
                    .Include(b => b.Employee)
                    .FirstOrDefaultAsync(b => b.BenId == benId);

                Email = Beneficiary?.Email ?? string.Empty;
                OnPropertyChanged(nameof(DisplayName));

                Claims.Clear();
                var claims = await db.Claims
                    .Include(c => c.Policy)
                    .Where(c => c.BenId == benId)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();
                foreach (var claim in claims)
                    Claims.Add(claim);

                if (Beneficiary != null && Beneficiary.IsPrimary)
                {
                    HasActiveClaim = await db.Claims.AnyAsync(c =>
                        (c.BenId == benId || (c.BenId == null && c.EmpId == Beneficiary.EmpId)) &&
                        (c.ClaimStatus == "Submitted" || c.ClaimStatus == "Under Review" || c.ClaimStatus == "Approved" || c.ClaimStatus == "Partially Approved"));
                }
                else
                {
                    HasActiveClaim = await db.Claims.AnyAsync(c =>
                        c.BenId == benId &&
                        (c.ClaimStatus == "Submitted" || c.ClaimStatus == "Under Review" || c.ClaimStatus == "Approved" || c.ClaimStatus == "Partially Approved"));
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OpenSubmitClaim()
        {
            var benId = AuthService.Instance.CurrentUser?.BenId;
            if (benId is null)
            {
                StatusMessage = "Your account is not linked to a beneficiary record.";
                return;
            }

            var dialog = new Views.Admin.Dialogs.ClaimVerificationDialog(benId.Value);
            dialog.SetSaveCallback(async () => await LoadAsync());
            if (App.ActiveShell is not null && App.ActiveShell != dialog)
                dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        private void OpenDistributionStatus()
        {
            var benId = AuthService.Instance.CurrentUser?.BenId;
            if (benId is null)
            {
                StatusMessage = "Your account is not linked to a beneficiary record.";
                return;
            }

            var dialog = new Views.Admin.Dialogs.DistributionStatusDialog(benId.Value);
            if (App.ActiveShell is not null && App.ActiveShell != dialog)
                dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        private async Task SaveEmailAsync()
        {
            var benId = AuthService.Instance.CurrentUser?.BenId;
            if (benId is null) return;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var beneficiary = await db.Beneficiaries.FindAsync(benId.Value);
                if (beneficiary is null)
                {
                    StatusMessage = "Beneficiary record not found.";
                    return;
                }

                beneficiary.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
                await db.SaveChangesAsync();
                StatusMessage = "Email updated.";
                await LoadAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Email update failed: {ex.Message}";
            }
        }

        private async Task ChangePasswordAsync()
        {
            var userId = AuthService.Instance.CurrentUser?.UserId ?? 0;
            var (success, message) = await AuthService.Instance.ChangePasswordAsync(userId, CurrentPassword, NewPassword);
            StatusMessage = message;
            if (success)
            {
                CurrentPassword = string.Empty;
                NewPassword = string.Empty;
            }
        }
    }
}
