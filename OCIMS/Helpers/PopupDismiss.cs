using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace eSureHi.Helpers
{
    /// <summary>
    /// Closes a <see cref="Popup"/> on an outside click, an Escape press, or window
    /// deactivation — without swallowing the click.
    ///
    /// <para><c>Popup.StaysOpen="False"</c> is the built-in way to get click-away
    /// dismissal, but it works by capturing the mouse: the click that dismisses the
    /// popup is consumed by the capture and never reaches the control underneath, so
    /// the user has to click twice to hit a grid row, a filter combo, or a button.
    /// Set <c>StaysOpen="True"</c> and attach this instead — the popup closes on the
    /// first click and that same click still lands on its target.</para>
    /// </summary>
    public static class PopupDismiss
    {
        public static readonly DependencyProperty OnOutsideClickProperty =
            DependencyProperty.RegisterAttached(
                "OnOutsideClick",
                typeof(bool),
                typeof(PopupDismiss),
                new PropertyMetadata(false, OnOutsideClickChanged));

        public static void SetOnOutsideClick(DependencyObject element, bool value) =>
            element.SetValue(OnOutsideClickProperty, value);

        public static bool GetOnOutsideClick(DependencyObject element) =>
            (bool)element.GetValue(OnOutsideClickProperty);

        // Keeps the watcher alive for as long as the popup is, and lets us tear the
        // subscription down again if the property is switched back off.
        private static readonly DependencyProperty WatcherProperty =
            DependencyProperty.RegisterAttached(
                "Watcher", typeof(Watcher), typeof(PopupDismiss), new PropertyMetadata(null));

        private static void OnOutsideClickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Popup popup)
                return;

            if (d.GetValue(WatcherProperty) is Watcher existing)
            {
                existing.Detach();
                d.SetValue(WatcherProperty, null);
            }

            if (e.NewValue is true)
                d.SetValue(WatcherProperty, new Watcher(popup));
        }

        private sealed class Watcher
        {
            private readonly Popup _popup;
            private Window? _window;

            public Watcher(Popup popup)
            {
                _popup = popup;
                _popup.Opened += OnOpened;
                _popup.Closed += OnClosed;

                if (_popup.IsOpen)
                    OnOpened(null, EventArgs.Empty);
            }

            public void Detach()
            {
                _popup.Opened -= OnOpened;
                _popup.Closed -= OnClosed;
                Unhook();
            }

            private void OnOpened(object? sender, EventArgs e)
            {
                // PlacementTarget is a real visual, so it always resolves the window;
                // the popup itself lives outside the main visual tree.
                var anchor = _popup.PlacementTarget ?? _popup;
                _window = Window.GetWindow(anchor);
                if (_window is null)
                    return;

                _window.AddHandler(UIElement.PreviewMouseDownEvent,
                    new MouseButtonEventHandler(OnPreviewMouseDown), handledEventsToo: true);
                _window.AddHandler(UIElement.PreviewKeyDownEvent,
                    new KeyEventHandler(OnPreviewKeyDown), handledEventsToo: true);
                _window.Deactivated += OnWindowDeactivated;
            }

            private void OnClosed(object? sender, EventArgs e) => Unhook();

            private void Unhook()
            {
                if (_window is null)
                    return;

                _window.RemoveHandler(UIElement.PreviewMouseDownEvent,
                    new MouseButtonEventHandler(OnPreviewMouseDown));
                _window.RemoveHandler(UIElement.PreviewKeyDownEvent,
                    new KeyEventHandler(OnPreviewKeyDown));
                _window.Deactivated -= OnWindowDeactivated;
                _window = null;
            }

            private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
            {
                if (!_popup.IsOpen)
                    return;

                var source = e.OriginalSource as DependencyObject;

                // Popups bridge their routed events back into the parent tree, so a
                // click on a suggestion row surfaces here too. Ignore those (matching
                // on Child as well as the Popup, since the popup's own visual root
                // does not always chain back to the Popup element), and ignore the
                // search box so focusing it does not wipe the list.
                if (IsWithin(source, _popup) ||
                    IsWithin(source, _popup.Child) ||
                    IsWithin(source, _popup.PlacementTarget))
                    return;

                // Deliberately no e.Handled — the click continues to its real target.
                _popup.IsOpen = false;
            }

            private void OnPreviewKeyDown(object sender, KeyEventArgs e)
            {
                if (_popup.IsOpen && e.Key == Key.Escape)
                    _popup.IsOpen = false;
            }

            private void OnWindowDeactivated(object? sender, EventArgs e) => _popup.IsOpen = false;
        }

        /// <summary>
        /// Walks visual parents, falling back to logical parents so the walk can cross
        /// out of a popup's own visual tree and reach the <see cref="Popup"/> itself.
        /// </summary>
        private static bool IsWithin(DependencyObject? node, DependencyObject? ancestor)
        {
            if (ancestor is null)
                return false;

            while (node is not null)
            {
                if (ReferenceEquals(node, ancestor))
                    return true;

                DependencyObject? parent = null;

                if (node is Visual or System.Windows.Media.Media3D.Visual3D)
                    parent = VisualTreeHelper.GetParent(node);

                parent ??= LogicalTreeHelper.GetParent(node);
                parent ??= (node as FrameworkElement)?.TemplatedParent;

                node = parent;
            }

            return false;
        }
    }
}
