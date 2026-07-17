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
    public class NotificationItem : ObservableObject
    {
        public int NotifId { get; set; }
        public string NotifType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string TimeAgo { get; set; } = string.Empty;
        public string DotColor { get; set; } = "#1565C0";

        private bool _isRead;
        public bool IsRead
        {
            get => _isRead;
            set => SetProperty(ref _isRead, value);
        }
    }

    public class NotificationsViewModel : ObservableObject
    {
        // ── Items ──────────────────────────────────────────────────────
        public ObservableCollection<NotificationItem> Items { get; } = new();

        // ── State ──────────────────────────────────────────────────────
        private int _unreadCount;
        private bool _isLoading;
        private bool _hasItems;

        public int UnreadCount { get => _unreadCount; set { SetProperty(ref _unreadCount, value); OnPropertyChanged(nameof(HasUnread)); } }
        public bool HasUnread => UnreadCount > 0;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
        public bool HasItems { get => _hasItems; set => SetProperty(ref _hasItems, value); }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand RefreshCommand { get; }
        public RelayCommand MarkAllReadCommand { get; }
        public RelayCommand<NotificationItem> MarkReadCommand { get; }

        // ── Callback to update badge in shell ──────────────────────────
        public Action<int>? OnUnreadCountChanged { get; set; }

        // ── Constructor ────────────────────────────────────────────────
        public NotificationsViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            MarkAllReadCommand = new RelayCommand(async () => await MarkAllReadAsync());
            MarkReadCommand = new RelayCommand<NotificationItem>(
                async item => { if (item is not null) await MarkReadAsync(item); });

            _ = LoadAsync();
        }

        // ── Load ───────────────────────────────────────────────────────
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var userId = AuthService.Instance.CurrentUser?.UserId ?? 0;

                var list = await db.Notifications
                    .Where(n => n.RecipientId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(50)
                    .ToListAsync();

                Items.Clear();
                foreach (var n in list)
                {
                    Items.Add(new NotificationItem
                    {
                        NotifId = n.NotifId,
                        NotifType = n.NotifType,
                        Title = n.Title,
                        Message = n.Message ?? string.Empty,
                        TimeAgo = GetTimeAgo(n.CreatedAt),
                        IsRead = n.IsRead,
                        DotColor = GetDotColor(n.NotifType)
                    });
                }

                HasItems = Items.Any();
                UnreadCount = Items.Count(i => !i.IsRead);
                OnUnreadCountChanged?.Invoke(UnreadCount);
            }
            catch (Exception ex)
            {
                App.ReportError("Load Notifications Failed", ex);
            }
            finally { IsLoading = false; }
        }

        // ── Mark Single Read ───────────────────────────────────────────
        private async Task MarkReadAsync(NotificationItem item)
        {
            if (item.IsRead) return;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var n = await db.Notifications.FindAsync(item.NotifId);
                if (n is null) return;
                n.IsRead = true;
                await db.SaveChangesAsync();

                item.IsRead = true;
                UnreadCount = Items.Count(i => !i.IsRead);
                OnUnreadCountChanged?.Invoke(UnreadCount);
            }
            catch (Exception ex)
            {
                App.ReportError("Mark Notification Read Failed", ex);
            }
        }

        // ── Mark All Read ──────────────────────────────────────────────
        private async Task MarkAllReadAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var userId = AuthService.Instance.CurrentUser?.UserId ?? 0;
                var unread = await db.Notifications
                    .Where(n => n.RecipientId == userId && !n.IsRead)
                    .ToListAsync();

                foreach (var n in unread) n.IsRead = true;
                await db.SaveChangesAsync();

                foreach (var item in Items) item.IsRead = true;
                UnreadCount = 0;
                OnUnreadCountChanged?.Invoke(0);
            }
            catch (Exception ex)
            {
                App.ReportError("Mark Notifications Read Failed", ex);
            }
        }

        // ── Helpers ────────────────────────────────────────────────────
        private static string GetTimeAgo(DateTime dt)
        {
            var diff = DateTime.Now - dt;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} min ago";
            if (diff.TotalDays < 1) return $"{(int)diff.TotalHours} hr ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} days ago";
            return dt.ToString("MMM d");
        }

        private static string GetDotColor(string type) => type switch
        {
            "Claim Update" => "#1565C0",
            "Premium Due" => "#C62828",
            "Policy Renewal" => "#E65100",
            "New Policy" => "#2E7D32",
            "System" => "#757575",
            _ => "#9E9E9E"
        };

        // ── Seed test notifications (dev helper) ───────────────────────
        public static async Task SeedTestNotificationsAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var userId = AuthService.Instance.CurrentUser?.UserId
                    ?? await db.SystemUsers
                        .OrderBy(u => u.UserId)
                        .Select(u => u.UserId)
                        .FirstOrDefaultAsync();

                if (userId == 0)
                    return;

                bool any = await db.Notifications.AnyAsync(
                    n => n.RecipientId == userId);
                if (any) return; // already seeded

                var samples = new[]
                {
                    new Notification
                    {
                        RecipientId  = userId,
                        NotifType    = "Claim Update",
                        Title        = "Claim CLM-260321100907 Approved",
                        Message      = "Your claim has been approved for ₱5,000.00.",
                        IsRead       = false,
                        CreatedAt    = DateTime.Now.AddMinutes(-5)
                    },
                    new Notification
                    {
                        RecipientId  = userId,
                        NotifType    = "Premium Due",
                        Title        = "Premium Due — April 2026",
                        Message      = "Your Regular employee budget track premium of PHP 900.00 is due on April 30.",
                        IsRead       = false,
                        CreatedAt    = DateTime.Now.AddHours(-2)
                    },
                    new Notification
                    {
                        RecipientId  = userId,
                        NotifType    = "Policy Renewal",
                        Title        = "Policy Renewal Reminder",
                        Message      = "Regular employee budget track renews on November 30, 2026.",
                        IsRead       = false,
                        CreatedAt    = DateTime.Now.AddHours(-5)
                    },
                    new Notification
                    {
                        RecipientId  = userId,
                        NotifType    = "New Policy",
                        Title        = "New Policy Assigned",
                        Message      = "You have been assigned to the Regular employee budget track.",
                        IsRead       = true,
                        CreatedAt    = DateTime.Now.AddDays(-1)
                    },
                    new Notification
                    {
                        RecipientId  = userId,
                        NotifType    = "System",
                        Title        = "System Backup Completed",
                        Message      = "Full backup completed successfully at 08:00 AM.",
                        IsRead       = true,
                        CreatedAt    = DateTime.Now.AddDays(-2)
                    }
                };

                db.Notifications.AddRange(samples);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                App.ReportError("Seed Notifications Failed", ex, showMessage: false);
            }
        }
    }
}
