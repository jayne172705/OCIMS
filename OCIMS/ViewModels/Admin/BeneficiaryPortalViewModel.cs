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

        public Beneficiary? Beneficiary { get => _beneficiary; set => SetProperty(ref _beneficiary, value); }
        public string Email { get => _email; set => SetProperty(ref _email, value); }
        public string CurrentPassword { get => _currentPassword; set => SetProperty(ref _currentPassword, value); }
        public string NewPassword { get => _newPassword; set => SetProperty(ref _newPassword, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        public string DisplayName => Beneficiary?.FullName ?? AuthService.Instance.CurrentUser?.Username ?? "Beneficiary";
        public ObservableCollection<Claim> Claims { get; } = new();

        public RelayCommand RefreshCommand { get; }
        public RelayCommand SubmitClaimCommand { get; }
        public RelayCommand SaveEmailCommand { get; }
        public RelayCommand ChangePasswordCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

        public BeneficiaryPortalViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            SubmitClaimCommand = new RelayCommand(OpenSubmitClaim);
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

            var dialog = new Views.Admin.Dialogs.ClaimFormDialog(beneficiaryId: benId.Value);
            dialog.SetSaveCallback(async () => await LoadAsync());
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
