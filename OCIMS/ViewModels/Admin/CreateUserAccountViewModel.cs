using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace eSureHi.ViewModels.Admin
{
    public class CreateUserAccountViewModel : ObservableObject
    {
        private readonly int _empId;
        private readonly int? _benId;
        private readonly string _empName;

        public string DialogTitle => $"Create User Account — {_empName}";

        // ── Fields ─────────────────────────────────────────────────────
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _role = "Employee";

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }
        public string Role
        {
            get => _role;
            set => SetProperty(ref _role, value);
        }

        public string[] RoleOptions { get; } = BuildRoleOptions();

        // ── State ──────────────────────────────────────────────────────
        private string _errorMessage = string.Empty;
        private bool _isBusy;

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }
        public bool IsBusy
        {
            get => _isBusy;
            set { SetProperty(ref _isBusy, value); SaveCommand.RaiseCanExecuteChanged(); }
        }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public Action? CloseAction { get; set; }
        public Action? OnSaveSuccess { get; set; }

        // ── Callback for PasswordBox ───────────────────────────────────
        public Func<string>? GetPassword { get; set; }

        // ── Constructor ────────────────────────────────────────────────
        public CreateUserAccountViewModel(int empId, string empName)
        {
            _empId = empId;
            _empName = empName;

            // Auto-suggest username from name
            Username = empName.Split(' ')[0].ToLower();

            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !IsBusy);
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
        }

        public CreateUserAccountViewModel(int beneficiaryId, string beneficiaryName, bool isBeneficiary)
            : this(0, beneficiaryName)
        {
            _benId = beneficiaryId;
            Role = "Beneficiary";
            Username = beneficiaryName.Split(' ')[0].ToLower();
        }

        // ── Save ───────────────────────────────────────────────────────
        private async Task SaveAsync()
        {
            var pwd = GetPassword?.Invoke() ?? Password;

            if (string.IsNullOrWhiteSpace(Username))
            { ErrorMessage = "Username is required."; return; }
            if (string.IsNullOrWhiteSpace(pwd) || pwd.Length < 6)
            { ErrorMessage = "Password must be at least 6 characters."; return; }
            if (!RoleOptions.Contains(Role))
            { ErrorMessage = "You are not allowed to create this role."; return; }

            IsBusy = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();

                bool userExists = await db.SystemUsers
                    .AnyAsync(u => u.Username == Username.Trim().ToLower());
                if (userExists)
                { ErrorMessage = "Username already taken. Choose another."; return; }

                bool alreadyHasAccount = _benId.HasValue
                    ? await db.SystemUsers.AnyAsync(u => u.BenId == _benId.Value)
                    : await db.SystemUsers.AnyAsync(u => u.EmpId == _empId);
                if (alreadyHasAccount)
                { ErrorMessage = _benId.HasValue ? "This beneficiary already has a user account." : "This employee already has a user account."; return; }

                var user = new SystemUser
                {
                    EmpId = _benId.HasValue ? null : _empId,
                    BenId = _benId,
                    Username = Username.Trim().ToLower(),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(pwd),
                    Role = Role,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                db.SystemUsers.Add(user);
                await db.SaveChangesAsync();

                // After save:
                await AuditService.LogInsert("system_users", user.UserId,
                    $"User account created: {Username} ({Role})");

                OnSaveSuccess?.Invoke();
                CloseAction?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed: {ex.Message}";
            }
            finally { IsBusy = false; }
        }

        private static string[] BuildRoleOptions()
        {
            var currentRole = AuthService.Instance.CurrentUser?.Role;
            if (PermissionService.IsSuperAdmin(currentRole))
                return new[] { "Super Admin", "Admin", "User", "Employee", "Beneficiary" };

            if (PermissionService.IsAdminReviewer(currentRole))
                return new[] { "User", "Employee", "Beneficiary" };

            return new[] { "User", "Employee", "Beneficiary" };
        }
    }
}
