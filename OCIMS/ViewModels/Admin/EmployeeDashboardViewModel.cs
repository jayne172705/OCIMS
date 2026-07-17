using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Services;
using eSureHi.Views.Admin.UserControls;

namespace eSureHi.ViewModels.Admin
{
    public class EmployeeDashboardTimelineItem
    {
        public string Title { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public string DotHex { get; set; } = "#1565C0";
        public string TimeText { get; set; } = string.Empty;
    }

    public class EmployeeDashboardViewModel : ObservableObject
    {
        private int _assignedPolicies;
        private int _pendingClaims;
        private int _availableBenefits;
        private int _unreadNotifications;
        private decimal _outstandingBalance;
        private bool _isLoading = true;
        private string _statusMessage = "Loading your dashboard...";

        public int AssignedPolicies
        {
            get => _assignedPolicies;
            set => SetProperty(ref _assignedPolicies, value);
        }

        public int PendingClaims
        {
            get => _pendingClaims;
            set => SetProperty(ref _pendingClaims, value);
        }

        public int AvailableBenefits
        {
            get => _availableBenefits;
            set => SetProperty(ref _availableBenefits, value);
        }

        public int UnreadNotifications
        {
            get => _unreadNotifications;
            set => SetProperty(ref _unreadNotifications, value);
        }

        public decimal OutstandingBalance
        {
            get => _outstandingBalance;
            set
            {
                if (SetProperty(ref _outstandingBalance, value))
                    OnPropertyChanged(nameof(OutstandingBalanceFormatted));
            }
        }

        public string OutstandingBalanceFormatted => $"PHP {OutstandingBalance:N2}";

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string WelcomeName =>
            AuthService.Instance.CurrentEmployee?.FirstName ??
            AuthService.Instance.CurrentUser?.Username ??
            "Employee";

        public string EmployeeFullName =>
            AuthService.Instance.CurrentEmployee?.FullName ??
            AuthService.Instance.CurrentUser?.Username ??
            "Employee";

        public string EmployeeRole =>
            AuthService.Instance.CurrentUser?.Role ?? "Employee";

        public string EmployeeNumber =>
            AuthService.Instance.CurrentEmployee?.EmployeeNo ?? "No household/resident ID";

        public string DepartmentName =>
            AuthService.Instance.CurrentEmployee?.Department?.DeptName ?? "No department assigned";

        public string EmploymentSummary
        {
            get
            {
                var employee = AuthService.Instance.CurrentEmployee;
                if (employee is null)
                    return "Your account is active in the employee portal.";

                var position = string.IsNullOrWhiteSpace(employee.PositionTitle)
                    ? "Municipal staff"
                    : employee.PositionTitle;
                return $"{position} • {employee.EmploymentStatus}";
            }
        }

        public string TodayDisplay => DateTime.Now.ToString("dddd, MMMM d, yyyy");

        public ObservableCollection<EmployeeDashboardTimelineItem> ClaimUpdates { get; } = new();
        public ObservableCollection<EmployeeDashboardTimelineItem> NotificationUpdates { get; } = new();

        public bool HasClaimUpdates => ClaimUpdates.Count > 0;
        public bool HasNotificationUpdates => NotificationUpdates.Count > 0;

        public RelayCommand RefreshCommand { get; }
        public RelayCommand OpenMyProfileCommand { get; }
        public RelayCommand OpenMyPoliciesCommand { get; }
        public RelayCommand OpenMyClaimsCommand { get; }
        public RelayCommand OpenMyPremiumsCommand { get; }
        public RelayCommand OpenMyBenefitsCommand { get; }

        public EmployeeDashboardViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            OpenMyProfileCommand = new RelayCommand(() =>
                NavigationService.Instance.NavigateTo(new MyProfileView()));
            OpenMyPoliciesCommand = new RelayCommand(() =>
                NavigationService.Instance.NavigateTo(new MyPoliciesView()));
            OpenMyClaimsCommand = new RelayCommand(() =>
                NavigationService.Instance.NavigateTo(new MyClaimsView()));
            OpenMyPremiumsCommand = new RelayCommand(() =>
                NavigationService.Instance.NavigateTo(new MyPremiumsView()));
            OpenMyBenefitsCommand = new RelayCommand(() =>
                NavigationService.Instance.NavigateTo(new MyBenefitsView()));

            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading your dashboard...";

            try
            {
                var employee = AuthService.Instance.CurrentEmployee;
                var user = AuthService.Instance.CurrentUser;

                if (employee?.EmpId is null || user is null)
                {
                    ResetMetrics();
                    StatusMessage = "No employee session found. Please sign in again.";
                    return;
                }

                using var db = eSureHiDbContextFactory.Create();

                var employeePolicyIds = await db.EmployeePolicies
                    .Where(ep => ep.EmpId == employee.EmpId)
                    .Select(ep => ep.EpId)
                    .ToListAsync();

                AssignedPolicies = await db.EmployeePolicies
                    .CountAsync(ep => ep.EmpId == employee.EmpId &&
                                      ep.AssignmentStatus == "Active");

                PendingClaims = await db.Claims
                    .CountAsync(c => c.EmpId == employee.EmpId &&
                                     (c.ClaimStatus == "Draft" ||
                                      c.ClaimStatus == "Submitted" ||
                                      c.ClaimStatus == "Under Review"));

                AvailableBenefits = employeePolicyIds.Count == 0
                    ? 0
                    : await db.Benefits.CountAsync(b =>
                        employeePolicyIds.Contains(b.EpId) && b.Remaining > 0);

                OutstandingBalance = employeePolicyIds.Count == 0
                    ? 0
                    : await db.Premiums
                        .Where(p => employeePolicyIds.Contains(p.EpId) &&
                                    p.PaymentStatus != "Paid" &&
                                    p.PaymentStatus != "Waived")
                        .SumAsync(p => (decimal?)p.Balance) ?? 0;

                UnreadNotifications = await db.Notifications
                    .CountAsync(n => n.RecipientId == user.UserId && !n.IsRead);

                var recentClaims = await db.Claims
                    .Where(c => c.EmpId == employee.EmpId)
                    .OrderByDescending(c => c.UpdatedAt)
                    .Take(4)
                    .Select(c => new
                    {
                        c.ClaimNo,
                        c.ClaimStatus,
                        c.UpdatedAt,
                        c.AmountClaimed
                    })
                    .ToListAsync();

                ClaimUpdates.Clear();
                foreach (var claim in recentClaims)
                {
                    ClaimUpdates.Add(new EmployeeDashboardTimelineItem
                    {
                        Title = claim.ClaimNo,
                        Caption = $"{claim.ClaimStatus} • PHP {claim.AmountClaimed:N2}",
                        DotHex = GetClaimColor(claim.ClaimStatus),
                        TimeText = GetTimeAgo(claim.UpdatedAt)
                    });
                }
                OnPropertyChanged(nameof(HasClaimUpdates));

                var recentNotifications = await db.Notifications
                    .Where(n => n.RecipientId == user.UserId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(4)
                    .Select(n => new
                    {
                        n.Title,
                        n.Message,
                        n.IsRead,
                        n.CreatedAt
                    })
                    .ToListAsync();

                NotificationUpdates.Clear();
                foreach (var notification in recentNotifications)
                {
                    NotificationUpdates.Add(new EmployeeDashboardTimelineItem
                    {
                        Title = notification.Title,
                        Caption = string.IsNullOrWhiteSpace(notification.Message)
                            ? "No additional details."
                            : notification.Message,
                        DotHex = notification.IsRead ? "#94A3B8" : "#0EA5E9",
                        TimeText = GetTimeAgo(notification.CreatedAt)
                    });
                }
                OnPropertyChanged(nameof(HasNotificationUpdates));

                StatusMessage = "Your benefits, claims, and account activity are up to date.";
            }
            catch (Exception ex)
            {
                ResetMetrics();
                StatusMessage = $"Unable to load employee dashboard: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ResetMetrics()
        {
            AssignedPolicies = 0;
            PendingClaims = 0;
            AvailableBenefits = 0;
            UnreadNotifications = 0;
            OutstandingBalance = 0;
            ClaimUpdates.Clear();
            NotificationUpdates.Clear();
            OnPropertyChanged(nameof(HasClaimUpdates));
            OnPropertyChanged(nameof(HasNotificationUpdates));
        }

        private static string GetClaimColor(string? status) =>
            status switch
            {
                "Approved" => "#15803D",
                "Released" => "#0F766E",
                "Under Review" => "#C2410C",
                "Submitted" => "#1D4ED8",
                "Rejected" => "#B91C1C",
                _ => "#64748B"
            };

        private static string GetTimeAgo(DateTime value)
        {
            var diff = DateTime.Now - value;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} min ago";
            if (diff.TotalDays < 1) return $"{(int)diff.TotalHours} hr ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} days ago";
            return value.ToString("MMM d");
        }
    }
}
