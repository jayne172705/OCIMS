using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Models;

namespace eSureHi.Services
{
    public static class PermissionService
    {
        private static UserPermission? _current;

        public static void LoadForUser(int userId)
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                _current = db.UserPermissions
                    .AsNoTracking()
                    .FirstOrDefault(p => p.UserId == userId);
            }
            catch
            {
                _current = null;
            }
        }

        public static void Clear() => _current = null;

        public static bool IsSuperAdmin(string? role) =>
            string.Equals(role, "Super Admin", StringComparison.OrdinalIgnoreCase);

        public static bool IsAdminReviewer(string? role) =>
            IsSuperAdmin(role) ||
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);

        public static bool IsUserRegistrar(string? role) =>
            string.Equals(role, "User", StringComparison.OrdinalIgnoreCase);

        public static bool IsEndUserRole(string? role) =>
            string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "Beneficiary", StringComparison.OrdinalIgnoreCase);

        public static bool IsControllableRole(string? role) =>
            !string.IsNullOrWhiteSpace(role) &&
            !IsSuperAdmin(role) &&
            !IsEndUserRole(role);

        private static bool NeedsCheck =>
            IsControllableRole(AuthService.Instance.CurrentUser?.Role);

        private static bool IsCurrentUserEmployee =>
            IsEndUserRole(AuthService.Instance.CurrentUser?.Role);

        private static bool IsCurrentUserBeneficiary =>
            string.Equals(AuthService.Instance.CurrentUser?.Role, "Beneficiary", StringComparison.OrdinalIgnoreCase);

        private static bool Check(bool value) =>
            IsAdminReviewer(AuthService.Instance.CurrentUser?.Role) ||
            !IsCurrentUserEmployee &&
            (!NeedsCheck || value);

        private static bool PermissionValue(string permissionName, bool fallback)
        {
            if (_current is not null)
            {
                return permissionName switch
                {
                    nameof(UserPermission.CanAccessDashboard) => _current.CanAccessDashboard,
                    nameof(UserPermission.CanAccessEmployees) => _current.CanAccessEmployees,
                    nameof(UserPermission.CanAccessBeneficiaries) => _current.CanAccessBeneficiaries,
                    nameof(UserPermission.CanAccessPolicies) => _current.CanAccessPolicies,
                    nameof(UserPermission.CanAccessClaims) => _current.CanAccessClaims,
                    nameof(UserPermission.CanAccessPremiums) => _current.CanAccessPremiums,
                    nameof(UserPermission.CanAccessBenefits) => _current.CanAccessBenefits,
                    nameof(UserPermission.CanAccessDocuments) => _current.CanAccessDocuments,
                    nameof(UserPermission.CanAccessTransactions) => _current.CanAccessTransactions,
                    nameof(UserPermission.CanAccessCedulas) => _current.CanAccessCedulas,
                    nameof(UserPermission.CanAccessReports) => _current.CanAccessReports,
                    nameof(UserPermission.CanAccessCompanyProfile) => _current.CanAccessCompanyProfile,
                    nameof(UserPermission.CanAccessSettings) => _current.CanAccessSettings,
                    _ => fallback
                };
            }

            var role = AuthService.Instance.CurrentUser?.Role;
            if (IsAdminReviewer(role))
            {
                return permissionName is nameof(UserPermission.CanAccessDashboard)
                    or nameof(UserPermission.CanAccessBeneficiaries)
                    or nameof(UserPermission.CanAccessClaims);
            }

            if (IsUserRegistrar(role))
            {
                return permissionName is nameof(UserPermission.CanAccessDashboard)
                    or nameof(UserPermission.CanAccessBeneficiaries)
                    or nameof(UserPermission.CanAccessClaims)
                    or nameof(UserPermission.CanAccessPremiums)
                    or nameof(UserPermission.CanAccessDocuments)
                    or nameof(UserPermission.CanAccessTransactions)
                    or nameof(UserPermission.CanAccessReports);
            }

            return fallback;
        }

        public static bool CanAccessHome => true;
        public static bool CanAccessDashboard => IsCurrentUserEmployee || Check(PermissionValue(nameof(UserPermission.CanAccessDashboard), true));
        public static bool CanAccessMyProfile => IsCurrentUserEmployee;
        public static bool CanAccessMyPolicies => IsCurrentUserEmployee;
        public static bool CanAccessMyClaims => IsCurrentUserEmployee;
        public static bool CanAccessMyPremiums => IsCurrentUserEmployee;
        public static bool CanAccessMyBenefits => IsCurrentUserEmployee;
        public static bool CanAccessEmployees => IsAdminOrSuperAdmin && Check(PermissionValue(nameof(UserPermission.CanAccessEmployees), true));
        public static bool CanAccessBeneficiaries => IsCurrentUserEmployee || Check(PermissionValue(nameof(UserPermission.CanAccessBeneficiaries), true));
        public static bool CanAccessPolicies => IsAdminOrSuperAdmin && Check(PermissionValue(nameof(UserPermission.CanAccessPolicies), true));
        public static bool CanAccessClaims => IsCurrentUserBeneficiary || Check(PermissionValue(nameof(UserPermission.CanAccessClaims), true));
        public static bool CanAccessPremiums => (IsAdminOrSuperAdmin || IsUserRegistrar(AuthService.Instance.CurrentUser?.Role)) && Check(PermissionValue(nameof(UserPermission.CanAccessPremiums), true));
        public static bool CanAccessBenefits => IsAdminOrSuperAdmin && Check(PermissionValue(nameof(UserPermission.CanAccessBenefits), true));
        public static bool CanAccessDocuments => (IsAdminOrSuperAdmin || IsUserRegistrar(AuthService.Instance.CurrentUser?.Role)) && Check(PermissionValue(nameof(UserPermission.CanAccessDocuments), true));
        public static bool CanAccessTransactions => IsCurrentUserEmployee || (IsAdminOrSuperAdmin && Check(PermissionValue(nameof(UserPermission.CanAccessTransactions), true)));
        public static bool CanAccessReviewQueue =>
            CanApproveWorkflow &&
            (CanAccessBeneficiaries || CanAccessClaims || CanAccessPolicies || CanAccessCedulas || CanAccessTransactions);
        public static bool CanAccessPendingMembers => CanApproveWorkflow;
        public static bool CanAccessPendingClaims => CanApproveWorkflow;
        public static bool CanAccessUserMemberFlow => IsUserRegistrar(AuthService.Instance.CurrentUser?.Role) || CanAccessBeneficiaries;
        public static bool CanAccessRegisterMember => IsUserRegistrar(AuthService.Instance.CurrentUser?.Role) || CanAccessBeneficiaries;
        public static bool CanAccessManageMembers =>
            IsUserRegistrar(AuthService.Instance.CurrentUser?.Role) ||
            CanAccessBeneficiaries;
        public static bool CanApproveWorkflow => IsAdminReviewer(AuthService.Instance.CurrentUser?.Role);
        public static bool CreatesPendingWorkflow => !CanApproveWorkflow;
        private static bool IsAdminOrSuperAdmin =>
            IsAdminReviewer(AuthService.Instance.CurrentUser?.Role);

        public static bool CanAccessSourceFunds => (IsAdminOrSuperAdmin || IsUserRegistrar(AuthService.Instance.CurrentUser?.Role)) && Check(PermissionValue(nameof(UserPermission.CanAccessTransactions), true));
        public static bool CanAccessCedulas => IsCurrentUserEmployee || (IsAdminOrSuperAdmin && Check(PermissionValue(nameof(UserPermission.CanAccessCedulas), true)));
        public static bool CanAccessReports => (IsAdminOrSuperAdmin || IsUserRegistrar(AuthService.Instance.CurrentUser?.Role)) && Check(PermissionValue(nameof(UserPermission.CanAccessReports), true));
        public static bool CanAccessCompanyProfile => IsAdminOrSuperAdmin && Check(PermissionValue(nameof(UserPermission.CanAccessCompanyProfile), true));
        public static bool CanAccessSettings => IsAdminOrSuperAdmin && Check(PermissionValue(nameof(UserPermission.CanAccessSettings), true));
        public static bool CanEditSettings => IsSuperAdmin(AuthService.Instance.CurrentUser?.Role);

        public static bool CanAccessPage(string page) => page switch
        {
            "Home" => CanAccessHome,
            "Dashboard" => CanAccessDashboard,
            "My Profile" => CanAccessMyProfile,
            "My Policies" => CanAccessMyPolicies,
            "My Claims" => CanAccessMyClaims,
            "My Premiums" => CanAccessMyPremiums,
            "My Benefits" => CanAccessMyBenefits,
            "Review Queue" => CanAccessReviewQueue,
            "Pending Members" => CanAccessPendingMembers,
            "Pending Claims" => CanAccessPendingClaims,
            "Employees" => CanAccessEmployees,
            "Beneficiaries" => CanAccessBeneficiaries,
            "Register Member" => CanAccessRegisterMember,
            "Manage Members" => CanAccessManageMembers,
            "Policies" => CanAccessPolicies,
            "Claims" => CanAccessClaims,
            "All Claims" => CanAccessClaims,
            "Premiums" => CanAccessPremiums,
            "Benefits" => CanAccessBenefits,
            "Documents" => CanAccessDocuments,
            "Transactions" => CanAccessTransactions,
            "Source of Funds" => CanAccessSourceFunds,
            "Barangay Funds" => CanAccessSourceFunds,
            "Group Funds" => CanAccessSourceFunds,
            "Allocated Funds" => CanAccessSourceFunds,
            "Cedulas" => CanAccessCedulas,
            "Reports" => CanAccessReports,
            "Company Profile" => CanAccessCompanyProfile,
            "Settings" => CanAccessSettings,
            "Manage Accounts" => CanAccessEmployees,
            "Announcements" => IsAdminOrSuperAdmin,
            "Beneficiary Portal" => IsCurrentUserBeneficiary,
            _ => true
        };

        public static string FirstAllowedAdminPage()
        {
            var pages = new[]
            {
                "Home",
                "Beneficiaries", "Transactions", "Benefits", "Review Queue",
                "Source of Funds", "Claims", "Employees", "Policies", "Premiums",
                "Documents", "Cedulas", "Dashboard", "Reports", "Company Profile", "Settings",
                "My Profile", "My Policies", "My Claims", "My Premiums", "My Benefits"
            };

            return pages.FirstOrDefault(CanAccessPage) ?? "Home";
        }

        public static IEnumerable<(string Label, string PropertyName)> PermissionOptions()
        {
            yield return ("Dashboard", nameof(UserPermission.CanAccessDashboard));
            yield return ("Employees", nameof(UserPermission.CanAccessEmployees));
            yield return ("Beneficiaries", nameof(UserPermission.CanAccessBeneficiaries));
            yield return ("Policies", nameof(UserPermission.CanAccessPolicies));
            yield return ("Claims", nameof(UserPermission.CanAccessClaims));
            yield return ("Premiums", nameof(UserPermission.CanAccessPremiums));
            yield return ("Benefits", nameof(UserPermission.CanAccessBenefits));
            yield return ("Documents", nameof(UserPermission.CanAccessDocuments));
            yield return ("Transactions", nameof(UserPermission.CanAccessTransactions));
            yield return ("Source of Funds", nameof(UserPermission.CanAccessTransactions));
            yield return ("Cedulas", nameof(UserPermission.CanAccessCedulas));
            yield return ("Reports", nameof(UserPermission.CanAccessReports));
            yield return ("Company Profile", nameof(UserPermission.CanAccessCompanyProfile));
            yield return ("Settings", nameof(UserPermission.CanAccessSettings));
        }
    }
}
