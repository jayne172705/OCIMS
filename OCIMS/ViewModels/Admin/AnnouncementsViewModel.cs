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
    public class AnnouncementRow
    {
        public int NotifId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string RecipientName { get; init; } = string.Empty;
        public string NotifType { get; init; } = string.Empty;
        public bool IsRead { get; init; }
        public DateTime CreatedAt { get; init; }
        public string StatusText => IsRead ? "Read" : "Unread";
        public string CreatedAtText => CreatedAt.ToString("MMM dd, yyyy hh:mm tt");
    }

    public class AnnouncementsViewModel : ObservableObject
    {
        private readonly ObservableCollection<AnnouncementRow> _allAnnouncements = new();

        public ObservableCollection<AnnouncementRow> DisplayedAnnouncements { get; } = new();
        public ObservableCollection<string> TypeOptions { get; } = new();
        public string[] ReadOptions { get; } = { "All", "Unread", "Read" };

        private AnnouncementRow? _selectedAnnouncement;
        public AnnouncementRow? SelectedAnnouncement
        {
            get => _selectedAnnouncement;
            set
            {
                if (SetProperty(ref _selectedAnnouncement, value))
                    MarkSelectedReadCommand.RaiseCanExecuteChanged();
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

        private string _selectedType = "All Types";
        public string SelectedType
        {
            get => _selectedType;
            set
            {
                if (SetProperty(ref _selectedType, value))
                    ApplyFilter();
            }
        }

        private string _selectedReadState = "All";
        public string SelectedReadState
        {
            get => _selectedReadState;
            set
            {
                if (SetProperty(ref _selectedReadState, value))
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

        private int _unreadCount;
        public int UnreadCount
        {
            get => _unreadCount;
            set => SetProperty(ref _unreadCount, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand MarkSelectedReadCommand { get; }
        public RelayCommand MarkVisibleReadCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

        public AnnouncementsViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            MarkSelectedReadCommand = new RelayCommand(async () => await MarkSelectedReadAsync(),
                () => SelectedAnnouncement is not null && !SelectedAnnouncement.IsRead);
            MarkVisibleReadCommand = new RelayCommand(async () => await MarkVisibleReadAsync());
            BackToDashboardCommand = new RelayCommand(() =>
                NavigationService.Instance.NavigateTo(new HomeView()));

            TypeOptions.Add("All Types");
            _ = LoadAsync();
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            StatusMessage = string.Empty;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var notifications = await db.Notifications
                    .OrderByDescending(item => item.CreatedAt)
                    .Take(500)
                    .ToListAsync();

                var userLookup = await db.SystemUsers
                    .Select(user => new { user.UserId, user.Username })
                    .ToDictionaryAsync(user => user.UserId, user => user.Username);

                _allAnnouncements.Clear();
                foreach (var item in notifications)
                {
                    _allAnnouncements.Add(new AnnouncementRow
                    {
                        NotifId = item.NotifId,
                        Title = item.Title,
                        Message = item.Message ?? string.Empty,
                        RecipientName = userLookup.TryGetValue(item.RecipientId, out var username)
                            ? username
                            : $"User #{item.RecipientId}",
                        NotifType = string.IsNullOrWhiteSpace(item.NotifType) ? "System" : item.NotifType,
                        IsRead = item.IsRead,
                        CreatedAt = item.CreatedAt
                    });
                }

                TotalCount = _allAnnouncements.Count;
                UnreadCount = _allAnnouncements.Count(item => !item.IsRead);

                var currentType = SelectedType;
                TypeOptions.Clear();
                TypeOptions.Add("All Types");
                foreach (var type in _allAnnouncements
                             .Select(item => item.NotifType)
                             .Where(type => !string.IsNullOrWhiteSpace(type))
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .OrderBy(type => type))
                {
                    TypeOptions.Add(type);
                }

                SelectedType = TypeOptions.Contains(currentType) ? currentType : "All Types";
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
            var query = _allAnnouncements.AsEnumerable();
            var search = SearchText.Trim();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(item =>
                    item.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.RecipientName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.NotifType.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedType != "All Types")
            {
                query = query.Where(item =>
                    string.Equals(item.NotifType, SelectedType, StringComparison.OrdinalIgnoreCase));
            }

            query = SelectedReadState switch
            {
                "Unread" => query.Where(item => !item.IsRead),
                "Read" => query.Where(item => item.IsRead),
                _ => query
            };

            DisplayedAnnouncements.Clear();
            foreach (var item in query)
                DisplayedAnnouncements.Add(item);

            FilteredCount = DisplayedAnnouncements.Count;
        }

        private async Task MarkSelectedReadAsync()
        {
            if (SelectedAnnouncement is null)
                return;

            await MarkReadAsync(new[] { SelectedAnnouncement.NotifId });
        }

        private async Task MarkVisibleReadAsync()
        {
            var unreadIds = DisplayedAnnouncements
                .Where(item => !item.IsRead)
                .Select(item => item.NotifId)
                .ToArray();

            if (unreadIds.Length == 0)
                return;

            await MarkReadAsync(unreadIds);
        }

        private async Task MarkReadAsync(int[] notifIds)
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var items = await db.Notifications
                    .Where(item => notifIds.Contains(item.NotifId) && !item.IsRead)
                    .ToListAsync();

                foreach (var item in items)
                    item.IsRead = true;

                await db.SaveChangesAsync();
                StatusMessage = $"{items.Count} announcement(s) marked as read.";
                await LoadAsync();
                SelectedAnnouncement = null;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Update failed: {ex.GetBaseException().Message}";
            }
        }
    }
}
