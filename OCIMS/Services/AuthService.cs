using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Models;
using MySqlConnector;

namespace eSureHi.Services
{
    public class AuthService
    {
        // ── Singleton ──────────────────────────────────────────────────
        private static AuthService? _instance;
        public static AuthService Instance =>
            _instance ??= new AuthService();
        private static readonly SemaphoreSlim DefaultEmployeeSeedLock = new(1, 1);

        // ── Session State ──────────────────────────────────────────────
        public SystemUser? CurrentUser { get; private set; }
        public Employee? CurrentEmployee { get; private set; }
        public Beneficiary? CurrentBeneficiary { get; private set; }
        public bool IsLoggedIn => CurrentUser is not null;
        public bool IsAdmin =>
            IsRole("Super Admin") || IsRole("Admin") || IsRole("User");
        public bool IsEmployee => IsRole("Employee");
        public bool IsBeneficiary => IsRole("Beneficiary");

        private AuthService() { }

        // ── Login ──────────────────────────────────────────────────────
        /// <summary>
        /// Returns (success, errorMessage).
        /// On success CurrentUser and CurrentEmployee are populated.
        /// </summary>
        public async Task<(bool Success, string Message)> LoginAsync(
            string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
                return (false, "Username is required.");
            if (string.IsNullOrWhiteSpace(password))
                return (false, "Password is required.");

            try
            {
                var normalizedUsername = username.Trim().ToLowerInvariant();
                if (normalizedUsername == "emp1" && password == "emp123")
                {
                    var seedResult = await SeedDefaultEmployeeAsync();
                    if (!seedResult.Seeded && seedResult.Message.StartsWith("Seed failed:", StringComparison.OrdinalIgnoreCase))
                        return (false, seedResult.Message);
                }

                using var db = eSureHiDbContextFactory.Create();

                var user = await db.SystemUsers
                    .Include(u => u.Employee)
                    .Include(u => u.Beneficiary)
                    .FirstOrDefaultAsync(u =>
                        u.Username.ToLower() == normalizedUsername && u.IsActive);

                if (user is null)
                    return (false, "Invalid username or password.");

                var passwordMatches = false;
                try
                {
                    passwordMatches = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                }
                catch
                {
                    passwordMatches = false;
                }

                if (!passwordMatches)
                    return (false, "Invalid username or password.");

                // Update last_login

                // Update last_login
                user.LastLogin = DateTime.Now;
                await db.SaveChangesAsync();

                CurrentUser = user;
                CurrentEmployee = user.Employee;
                CurrentBeneficiary = user.Beneficiary;

                if (!IsRole("Super Admin") && !IsRole("Admin") && !IsRole("User") && !IsRole("Employee") && !IsRole("Beneficiary"))
                {
                    CurrentUser = null;
                    CurrentEmployee = null;
                    CurrentBeneficiary = null;
                    return (false, $"Your account has an invalid role '{user.Role}'. Please ask the admin to fix your role.");
                }

                PermissionService.LoadForUser(user.UserId);

                await CreateLoginNoticeAsync(user.UserId, user.Username, user.Role);

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, FormatConnectionError(ex));
            }
        }

        private static string FormatConnectionError(Exception ex)
        {
            var detail = ex.GetBaseException().Message;
            if (detail.Contains("max_connections_per_hour", StringComparison.OrdinalIgnoreCase))
            {
                return "Connection error: The remote database hourly connection limit has been reached. Please wait for the hosting limit to reset, use the local database for now, or ask the host/admin to increase or flush the MySQL user connection quota.";
            }

            return $"Connection error: {detail}";
        }

        private bool IsRole(string role) =>
            string.Equals(CurrentUser?.Role, role, StringComparison.OrdinalIgnoreCase);

        private static async Task CreateLoginNoticeAsync(int userId, string username, string role)
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                db.Notifications.Add(new Notification
                {
                    RecipientId = userId,
                    NotifType = "System",
                    Title = "Welcome to eSureHi+",
                    Message = $"Hello {username}, you are signed in as {role}. Please review your dashboard updates and pending insurance records.",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });

                await db.SaveChangesAsync();
            }
            catch
            {
                // Login should still succeed even if the welcome notice cannot be saved.
            }
        }

        // ── Logout ─────────────────────────────────────────────────────
        public void Logout()
        {
            PermissionService.Clear();
            CurrentUser = null;
            CurrentEmployee = null;
            CurrentBeneficiary = null;
        }

        // ── Seed Admin ─────────────────────────────────────────────────
        /// <summary>
        /// Creates the default admin account if no admin exists yet.
        /// Called once on startup from App.xaml.cs.
        /// Returns (seeded, message).
        /// </summary>
        public async Task<(bool Seeded, string Message)> SeedAdminAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                await EnsureRoleColumnSupportsWorkflowRolesAsync(db);

                var existing = await db.SystemUsers
                    .FirstOrDefaultAsync(u => u.Username == "superadmin" || u.Username == "admin");

                if (existing is not null)
                {
                    existing.Username = "superadmin";
                    existing.Role = "Super Admin";

                    // Check if hash is valid BCrypt — fix it if not
                    bool isValidHash = existing.PasswordHash.StartsWith("$2a$") ||
                                       existing.PasswordHash.StartsWith("$2b$") ||
                                       existing.PasswordHash.StartsWith("$2y$");

                    if (!isValidHash)
                    {
                        existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword("password");
                        await db.SaveChangesAsync();
                        return (true, "Super Admin password hash was invalid - reset to password.");
                    }

                    if (!BCrypt.Net.BCrypt.Verify("password", existing.PasswordHash))
                        existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword("password");

                    await db.SaveChangesAsync();
                    return (false, "Super Admin account already exists.");
                }

                var admin = new SystemUser
                {
                    Username = "superadmin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
                    Role = "Super Admin",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                db.SystemUsers.Add(admin);
                await db.SaveChangesAsync();

                return (true, "Default Super Admin account created (superadmin / password).");
            }
            catch (Exception ex)
            {
                return (false, $"Seed failed: {ex.Message}");
            }
        }

        public async Task<(bool Seeded, string Message)> SeedWebFlowAccountsAsync()
        {
            try
            {
                await EnsureUserPermissionsTableAsync();

                using var db = eSureHiDbContextFactory.Create();
                await EnsureRoleColumnSupportsWorkflowRolesAsync(db);
                await RenameLegacyWorkflowAccountAsync(db, "admin@gmail.com", "admin");
                await RenameLegacyWorkflowAccountAsync(db, "user@gmail.com", "user");

                var seeded = false;

                seeded |= await EnsureRoleAccountAsync(
                    db,
                    username: "admin",
                    password: "password",
                    role: "Admin");

                seeded |= await EnsureRoleAccountAsync(
                    db,
                    username: "user",
                    password: "password",
                    role: "User");

                await db.SaveChangesAsync();

                return (seeded,
                    seeded
                        ? "Admin and User workflow accounts are ready."
                        : "Admin and User workflow accounts already exist.");
            }
            catch (Exception ex)
            {
                return (false, $"Seed failed: {ex.Message}");
            }
        }

        private static async Task EnsureRoleColumnSupportsWorkflowRolesAsync(eSureHiDbContext db)
        {
            if (!db.Database.IsMySql())
                return;

            await db.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE system_users
                MODIFY role ENUM(
                    'Super Admin',
                    'Admin',
                    'User',
                    'HR Admin',
                    'Insurance Admin',
                    'Auditor',
                    'Employee',
                    'Beneficiary'
                ) NULL DEFAULT 'Employee';");
        }

        private static async Task<bool> EnsureRoleAccountAsync(
            eSureHiDbContext db,
            string username,
            string password,
            string role)
        {
            var normalized = username.Trim().ToLowerInvariant();
            var user = await db.SystemUsers
                .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized);

            var changed = false;
            if (user is null)
            {
                user = new SystemUser
                {
                    Username = normalized,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    Role = role,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                db.SystemUsers.Add(user);
                await db.SaveChangesAsync();
                changed = true;
            }

            if (!string.Equals(user.Role, role, StringComparison.OrdinalIgnoreCase))
            {
                user.Role = role;
                changed = true;
            }

            if (!user.IsActive)
            {
                user.IsActive = true;
                changed = true;
            }

            var passwordMatches = false;
            try
            {
                passwordMatches = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            }
            catch
            {
                passwordMatches = false;
            }

            if (!passwordMatches)
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                changed = true;
            }

            changed |= await EnsureRolePermissionsAsync(db, user.UserId, role);
            return changed;
        }

        private static async Task RenameLegacyWorkflowAccountAsync(
            eSureHiDbContext db,
            string oldUsername,
            string newUsername)
        {
            var oldNormalized = oldUsername.Trim().ToLowerInvariant();
            var newNormalized = newUsername.Trim().ToLowerInvariant();
            var legacy = await db.SystemUsers
                .FirstOrDefaultAsync(u => u.Username.ToLower() == oldNormalized);

            if (legacy is null)
                return;

            var targetExists = await db.SystemUsers
                .AnyAsync(u => u.UserId != legacy.UserId &&
                               u.Username.ToLower() == newNormalized);
            if (targetExists)
                return;

            legacy.Username = newNormalized;
        }

        private static async Task<bool> EnsureRolePermissionsAsync(eSureHiDbContext db, int userId, string role)
        {
            var permissions = await db.UserPermissions
                .FirstOrDefaultAsync(p => p.UserId == userId);
            var changed = false;

            if (permissions is null)
            {
                permissions = new UserPermission { UserId = userId };
                db.UserPermissions.Add(permissions);
                changed = true;
            }

            bool dashboard = true;
            bool superAdmin = string.Equals(role, "Super Admin", StringComparison.OrdinalIgnoreCase);
            bool userWorkflow = string.Equals(role, "User", StringComparison.OrdinalIgnoreCase);
            bool beneficiaries = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(role, "User", StringComparison.OrdinalIgnoreCase);
            bool claims = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                          userWorkflow;

            void SetIfNeeded(bool currentValue, Action<bool> setValue, bool value)
            {
                if (currentValue == value)
                    return;

                setValue(value);
                changed = true;
            }

            SetIfNeeded(permissions.CanAccessDashboard, value => permissions.CanAccessDashboard = value, dashboard);
            SetIfNeeded(permissions.CanAccessEmployees, value => permissions.CanAccessEmployees = value, superAdmin);
            SetIfNeeded(permissions.CanAccessBeneficiaries, value => permissions.CanAccessBeneficiaries = value, superAdmin || beneficiaries);
            SetIfNeeded(permissions.CanAccessPolicies, value => permissions.CanAccessPolicies = value, superAdmin);
            SetIfNeeded(permissions.CanAccessClaims, value => permissions.CanAccessClaims = value, superAdmin || claims);
            SetIfNeeded(permissions.CanAccessPremiums, value => permissions.CanAccessPremiums = value, superAdmin || userWorkflow);
            SetIfNeeded(permissions.CanAccessBenefits, value => permissions.CanAccessBenefits = value, superAdmin);
            SetIfNeeded(permissions.CanAccessDocuments, value => permissions.CanAccessDocuments = value, superAdmin || userWorkflow);
            SetIfNeeded(permissions.CanAccessTransactions, value => permissions.CanAccessTransactions = value, superAdmin || userWorkflow);
            SetIfNeeded(permissions.CanAccessCedulas, value => permissions.CanAccessCedulas = value, superAdmin);
            SetIfNeeded(permissions.CanAccessReports, value => permissions.CanAccessReports = value, superAdmin || userWorkflow);
            SetIfNeeded(permissions.CanAccessCompanyProfile, value => permissions.CanAccessCompanyProfile = value, superAdmin);
            SetIfNeeded(permissions.CanAccessSettings, value => permissions.CanAccessSettings = value, superAdmin);

            if (changed)
                permissions.UpdatedAt = DateTime.Now;

            return changed;
        }

        /// <summary>
        /// Ensures the default employee demo account exists for employee portal testing.
        /// </summary>
        public async Task<(bool Seeded, string Message)> SeedDefaultEmployeeAsync()
        {
            const string username = "emp1";
            const string password = "emp123";

            await DefaultEmployeeSeedLock.WaitAsync();
            try
            {
                await EnsureUserPermissionsTableAsync();

                using var db = eSureHiDbContextFactory.Create();

                var employee = await db.Employees
                    .OrderBy(e => e.EmpId)
                    .FirstOrDefaultAsync(e => e.EmploymentStatus == "Active")
                    ?? await db.Employees
                        .OrderBy(e => e.EmpId)
                        .FirstOrDefaultAsync();

                if (employee is null)
                {
                    employee = new Employee
                    {
                        EmployeeNo = "EMP-0001",
                        FirstName = "Employee",
                        LastName = "One",
                        CivilStatus = "Single",
                        Nationality = "Filipino",
                        EmploymentType = "Regular",
                        EmploymentStatus = "Active",
                        PositionTitle = "Employee",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    db.Employees.Add(employee);
                    await db.SaveChangesAsync();
                }

                var user = await db.SystemUsers
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == username);

                if (user is null)
                {
                    user = new SystemUser
                    {
                        EmpId = employee.EmpId,
                        Username = username,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                        Role = "Employee",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };

                    db.SystemUsers.Add(user);
                    await db.SaveChangesAsync();

                    await EnsureEmployeePortalOnlyPermissionsAsync(db, user.UserId);
                    await db.SaveChangesAsync();
                    return (true, "Default employee account created (emp1 / emp123).");
                }

                var changed = false;

                if (user.EmpId != employee.EmpId)
                {
                    user.EmpId = employee.EmpId;
                    changed = true;
                }

                if (!string.Equals(user.Role, "Employee", StringComparison.OrdinalIgnoreCase))
                {
                    user.Role = "Employee";
                    changed = true;
                }

                if (!user.IsActive)
                {
                    user.IsActive = true;
                    changed = true;
                }

                var passwordMatches = false;
                try
                {
                    passwordMatches = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                }
                catch
                {
                    passwordMatches = false;
                }

                if (!passwordMatches)
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                    changed = true;
                }

                var permissionsChanged = await EnsureEmployeePortalOnlyPermissionsAsync(db, user.UserId);

                if (changed || permissionsChanged)
                {
                    await db.SaveChangesAsync();
                    return (true, "Default employee account repaired (emp1 / emp123).");
                }

                return (false, "Default employee account already exists.");
            }
            catch (Exception ex)
            {
                return (false, $"Seed failed: {ex.Message}");
            }
            finally
            {
                DefaultEmployeeSeedLock.Release();
            }
        }

        public static async Task EnsureUserPermissionsTableAsync()
        {
            await using var db = eSureHiDbContextFactory.Create();
            await db.Database.EnsureCreatedAsync();
        }

        public static async Task EnsureCloudUserPermissionsTableAsync()
        {
            if (!App.DbConfig.IsConfigured)
                return;

            await using var conn = new MySqlConnection(App.DbConfig.ToConnectionString());
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS user_permissions (
                    permission_id INT NOT NULL AUTO_INCREMENT,
                    user_id INT NOT NULL,
                    can_access_dashboard TINYINT(1) NOT NULL DEFAULT 1,
                    can_access_employees TINYINT(1) NOT NULL DEFAULT 1,
                    can_access_beneficiaries TINYINT(1) NOT NULL DEFAULT 1,
                    can_access_policies TINYINT(1) NOT NULL DEFAULT 1,
                    can_access_claims TINYINT(1) NOT NULL DEFAULT 1,
                    can_access_premiums TINYINT(1) NOT NULL DEFAULT 1,
                    can_access_benefits TINYINT(1) NOT NULL DEFAULT 1,
                    can_access_documents TINYINT(1) NOT NULL DEFAULT 1,
                    can_access_transactions TINYINT(1) NOT NULL DEFAULT 1,
                    can_access_cedulas TINYINT(1) NOT NULL DEFAULT 1,
                    can_access_reports TINYINT(1) NOT NULL DEFAULT 1,
                    can_access_company_profile TINYINT(1) NOT NULL DEFAULT 1,
                    can_access_settings TINYINT(1) NOT NULL DEFAULT 1,
                    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (permission_id),
                    UNIQUE KEY ux_user_permissions_user_id (user_id),
                    CONSTRAINT fk_user_permissions_system_users
                        FOREIGN KEY (user_id) REFERENCES system_users(user_id)
                        ON DELETE CASCADE
                );";
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task<bool> EnsureEmployeePortalOnlyPermissionsAsync(eSureHiDbContext db, int userId)
        {
            var permissions = await db.UserPermissions
                .FirstOrDefaultAsync(p => p.UserId == userId);
            var changed = false;

            if (permissions is null)
            {
                permissions = new UserPermission { UserId = userId };
                db.UserPermissions.Add(permissions);
                changed = true;
            }

            void SetIfNeeded(bool currentValue, Action<bool> setValue, bool value)
            {
                if (currentValue == value)
                    return;

                setValue(value);
                changed = true;
            }

            SetIfNeeded(permissions.CanAccessDashboard, value => permissions.CanAccessDashboard = value, false);
            SetIfNeeded(permissions.CanAccessEmployees, value => permissions.CanAccessEmployees = value, false);
            SetIfNeeded(permissions.CanAccessBeneficiaries, value => permissions.CanAccessBeneficiaries = value, false);
            SetIfNeeded(permissions.CanAccessPolicies, value => permissions.CanAccessPolicies = value, false);
            SetIfNeeded(permissions.CanAccessClaims, value => permissions.CanAccessClaims = value, false);
            SetIfNeeded(permissions.CanAccessPremiums, value => permissions.CanAccessPremiums = value, false);
            SetIfNeeded(permissions.CanAccessBenefits, value => permissions.CanAccessBenefits = value, false);
            SetIfNeeded(permissions.CanAccessDocuments, value => permissions.CanAccessDocuments = value, false);
            SetIfNeeded(permissions.CanAccessTransactions, value => permissions.CanAccessTransactions = value, false);
            SetIfNeeded(permissions.CanAccessCedulas, value => permissions.CanAccessCedulas = value, false);
            SetIfNeeded(permissions.CanAccessReports, value => permissions.CanAccessReports = value, false);
            SetIfNeeded(permissions.CanAccessCompanyProfile, value => permissions.CanAccessCompanyProfile = value, false);
            SetIfNeeded(permissions.CanAccessSettings, value => permissions.CanAccessSettings = value, false);

            if (changed)
                permissions.UpdatedAt = DateTime.Now;

            return changed;
        }

        // ── Seed Document Types ───────────────────────────────────────
        /// <summary>
        /// Populates common document types if none exist.
        /// </summary>
        public async Task SeedDocumentTypesAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                if (await db.DocumentTypes.AnyAsync()) return; // already has data

                var types = new[]
                {
                    new DocumentType { TypeName = "Personal Records", Description = "Birth certificates, IDs, etc.", IsActive = true, CreatedAt = DateTime.Now },
                    new DocumentType { TypeName = "Employment Contract", Description = "Signed contracts and appointment papers.", IsActive = true, CreatedAt = DateTime.Now },
                    new DocumentType { TypeName = "Insurance Policy", Description = "Insurance coverage documents.", IsActive = true, CreatedAt = DateTime.Now },
                    new DocumentType { TypeName = "Medical Records", Description = "Health clearances and medical history.", IsActive = true, CreatedAt = DateTime.Now },
                    new DocumentType { TypeName = "Beneficiary Documents", Description = "Forms related to beneficiaries.", IsActive = true, CreatedAt = DateTime.Now },
                    new DocumentType { TypeName = "Educational Background", Description = "Diplomas, transcripts, etc.", IsActive = true, CreatedAt = DateTime.Now },
                    new DocumentType { TypeName = "Other Documents", Description = "Miscellaneous files.", IsActive = true, CreatedAt = DateTime.Now }
                };

                db.DocumentTypes.AddRange(types);
                await db.SaveChangesAsync();
            }
            catch
            {
                // Non-critical seeding — swallow
            }
        }


        // ── Change Password ────────────────────────────────────────────
        public async Task<(bool Success, string Message)> ChangePasswordAsync(
            int userId, string currentPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                return (false, "New password must be at least 6 characters.");

            try
            {
                using var db = eSureHiDbContextFactory.Create();

                var user = await db.SystemUsers.FindAsync(userId);
                if (user is null)
                    return (false, "User not found.");

                if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
                    return (false, "Current password is incorrect.");

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                await db.SaveChangesAsync();

                return (true, "Password changed successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        }
    }
}

