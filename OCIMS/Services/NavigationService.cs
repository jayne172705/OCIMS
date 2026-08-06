using System;
using System.Windows.Controls;

namespace eSureHi.Services
{
    public class NavigationService
    {
        // ── Singleton ──────────────────────────────────────────────────
        private static NavigationService? _instance;
        public static NavigationService Instance =>
            _instance ??= new NavigationService();

        // ── The ContentControl that hosts page UserControls ────────────
        private ContentControl? _frame;

        public event Action<UserControl>? Navigated;

        private NavigationService() { }

        /// <summary>
        /// Called once from MainShell to register the host ContentControl.
        /// </summary>
        public void SetFrame(ContentControl frame)
        {
            _frame = frame;
        }

        /// <summary>
        /// Navigates to a UserControl page.
        /// Usage: NavigationService.Instance.NavigateTo(new DashboardView());
        /// </summary>
        public void NavigateTo(UserControl page)
        {
            if (_frame is null)
                throw new InvalidOperationException(
                    "NavigationService: frame not registered. " +
                    "Call SetFrame() from MainShell first.");

            _frame.Content = page;
            Navigated?.Invoke(page);
        }

        /// <summary>
        /// Returns the currently displayed page.
        /// </summary>
        public UserControl? CurrentPage => _frame?.Content as UserControl;

        /// <summary>
        /// Returns the currently displayed page type (for nav highlighting).
        /// </summary>
        public Type? CurrentPageType => (_frame?.Content as UserControl)?.GetType();
    }
}
