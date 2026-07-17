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
using eSureHi.Views.Admin.UserControls;

namespace eSureHi.ViewModels.Admin
{
    public class ManageAccountRow
    {
        public int UserId { get; init; }
        public string Username { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public DateTime? LastLogin { get; init; }
        public string LinkedName { get; init; } = string.Empty;
        public string LinkedType { get; init; } = string.Empty;
        public string AccountStatus => IsActive ? "Active" : "Inactive";
        public string LastLoginText => LastLogin?.ToString("MMM dd, yyyy hh:mm tt") ?? "Never";
        public bool CanManageAccess => PermissionService.IsControllableRole(Role);
    }

    public class ManageAccountsViewModel : ObservableObject
    {
        private readonly ObservableCollection<ManageAccountRow> _allAccounts = new();

        public ObservableCollection<ManageAccountRow> DisplayedAccounts { get; } = new();
        public ObservableCollection<string> RoleOptions { get; } = new();
        public string[] ActivityOptions { get; } = { "All Accounts", "Active", "Inactive" };

        private ManageAccountRow? _selectedAccount;
        public ManageAccountRow? SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                if (SetProperty(ref _selectedAccount, value))
                {
                    ToggleActiveCommand.RaiseCanExecuteChanged();
                    ManageAccessCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplyFilter();
            }
        }

        private string _selectedRole = "All Roles";
        public string SelectedRole
        {
            get => _selectedRole;
            set
            {
                if (SetProperty(ref _selectedRole, value))
                    ApplyFilter();
            }
        }

        private string _selectedActivity = "All Accounts";
        public string SelectedActivity
        {
            get => _selectedActivity;
            set
            {
                if (SetProperty(ref _selectedActivity, value))
                    ApplyFilter();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        private int _filteredCount;
        public int FilteredCount
        {
            get => _filteredCount;
            set => SetProperty(ref _filteredCount, value);
        }

        private int _activeCount;
        public int ActiveCount
        {
            get => _activeCount;
            set => SetProperty(ref _activeCount, value);
        }

        private int _inactiveCount;
        public int InactiveCount
        {
            get => _inactiveCount;
            set => SetProperty(ref _inactiveCount, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand ToggleActiveCommand { get; }
        public RelayCommand ManageAccessCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

        public ManageAccountsViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            ToggleActiveCommand = new RelayCommand(async () => await ToggleActiveAsync(), CanToggleActive);
            ManageAccessCommand = new RelayCommand(async () => await OpenManageAccessAsync(), CanManageAccess);
            BackToDashboardCommand = new RelayCommand(() =>
                NavigationService.Instance.NavigateTo(new DashboardView()));

            RoleOptions.Add("All Roles");
            _ = LoadAsync();
        }

        private bool CanToggleActive() =>
            SelectedAccount is not null &&
            SelectedAccount.UserId != AuthService.Instance.CurrentUser?.UserId &&
            !string.Equals(SelectedAccount.Role, "Super Admin", StringComparison.OrdinalIgnoreCase);

        private bool CanManageAccess() =>
            SelectedAccount?.CanManageAccess == true;

        public async Task LoadAsync()
        {
            IsLoading = true;
            StatusMessage = string.Empty;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var users = await db.SystemUsers
                    .Include(u => u.Employee)
                    .Include(u => u.Beneficiary)
                    .OrderBy(u => u.Username)
                    .ToListAsync();

                _allAccounts.Clear();
                foreach (var user in users)
                {
                    _allAccounts.Add(new ManageAccountRow
                    {
                        UserId = user.UserId,
                        Username = user.Username,
                        Role = user.Role,
                        IsActive = user.IsActive,
                        LastLogin = user.LastLogin,
                        LinkedName = user.Employee?.FullName ??
                                     user.Beneficiary?.FullName ??
                                     "System account",
                        LinkedType = user.Employee is not null
                            ? "Employee"
                            : user.Beneficiary is not null
                                ? "Beneficiary"
                                : "System"
                    });
                }

                TotalCount = _allAccounts.Count;
                ActiveCount = _allAccounts.Count(account => account.IsActive);
                InactiveCount = _allAccounts.Count(account => !account.IsActive);

                var currentRole = SelectedRole;
                RoleOptions.Clear();
                RoleOptions.Add("All Roles");
                foreach (var role in _allAccounts
                             .Select(account => account.Role)
                             .Where(role => !string.IsNullOrWhiteSpace(role))
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .OrderBy(role => role))
                {
                    RoleOptions.Add(role);
                }

                SelectedRole = RoleOptions.Contains(currentRole) ? currentRole : "All Roles";
                ApplyFilter();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load failed: {ex.GetBaseException().Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilter()
        {
            var query = _allAccounts.AsEnumerable();
            var search = SearchText.Trim();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(account =>
                    account.Username.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    account.LinkedName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    account.Role.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedRole != "All Roles")
            {
                query = query.Where(account =>
                    string.Equals(account.Role, SelectedRole, StringComparison.OrdinalIgnoreCase));
            }

            query = SelectedActivity switch
            {
                "Active" => query.Where(account => account.IsActive),
                "Inactive" => query.Where(account => !account.IsActive),
                _ => query
            };

            DisplayedAccounts.Clear();
            foreach (var account in query)
                DisplayedAccounts.Add(account);

            FilteredCount = DisplayedAccounts.Count;
        }

        private async Task ToggleActiveAsync()
        {
            if (SelectedAccount is null)
                return;

            var nextState = !SelectedAccount.IsActive;
            var result = MessageBox.Show(
                $"{(nextState ? "Activate" : "Deactivate")} account '{SelectedAccount.Username}'?",
                "Manage Account",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var user = await db.SystemUsers.FindAsync(SelectedAccount.UserId);
                if (user is null)
                    return;

                user.IsActive = nextState;
                await db.SaveChangesAsync();
                StatusMessage = $"Account '{user.Username}' marked {(nextState ? "active" : "inactive")}.";
                await LoadAsync();
                SelectedAccount = DisplayedAccounts.FirstOrDefault(account => account.UserId == user.UserId);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Status update failed: {ex.GetBaseException().Message}";
            }
        }

        private async Task OpenManageAccessAsync()
        {
            if (SelectedAccount is null)
                return;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var user = await db.SystemUsers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(account => account.UserId == SelectedAccount.UserId);

                if (user is null)
                    return;

                if (!PermissionService.IsControllableRole(user.Role))
                {
                    MessageBox.Show(
                        $"{user.Role} accounts do not use the per-user access dialog.",
                        "Manage Access",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var dialog = new Views.Admin.Dialogs.UserPermissionsDialog(user);
                dialog.SetSaveCallback(async () => await LoadAsync());
                if (App.ActiveShell is not null && App.ActiveShell != dialog)
                    dialog.Owner = App.ActiveShell;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Open access settings failed: {ex.GetBaseException().Message}";
            }
        }
    }
}
