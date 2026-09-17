using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DymndAssetBrowser.App.Infrastructure;

// Popups have their own native windows. Never let one outlive foreground
// ownership, including a programmatic open after the owner already lost focus.
public static class PopupOwnerGuard
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(PopupOwnerGuard), new PropertyMetadata(false, Changed));
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(Guard), typeof(PopupOwnerGuard));
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not Popup popup) return;
        (popup.GetValue(StateProperty) as Guard)?.Dispose();
        popup.SetValue(StateProperty, (bool)args.NewValue ? new Guard(popup) : null);
    }
    public static bool CanOpen(Window? owner) => owner is { IsActive: true, IsVisible: true, IsEnabled: true }
        && owner.WindowState != WindowState.Minimized;

    private sealed class Guard : IDisposable
    {
        private readonly Popup _popup;
        private Window? _owner;
        private FrameworkElement? _anchor;
        public Guard(Popup popup)
        {
            _popup = popup;
            popup.Opened += Opened;
            popup.Closed += Closed;
            popup.Unloaded += Unloaded;
        }
        private void Opened(object? sender, EventArgs e)
        {
            DetachOwner();
            _anchor = _popup.PlacementTarget as FrameworkElement ?? _popup.TemplatedParent as FrameworkElement;
            _owner = Window.GetWindow(_anchor ?? _popup);
            if (!CanOpen(_owner) || _anchor is { IsVisible: false }) { Close(); return; }
            _owner!.Deactivated += OwnerChanged;
            _owner.StateChanged += OwnerChanged;
            _owner.LocationChanged += OwnerChanged;
            _owner.Closed += OwnerChanged;
            _owner.IsVisibleChanged += VisibilityChanged;
            if (_anchor is not null) _anchor.IsVisibleChanged += VisibilityChanged;
        }
        private void OwnerChanged(object? sender, EventArgs e) => Close();
        private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) { if (!(bool)e.NewValue) Close(); }
        private void Close()
        {
            // Preserve bindings and reset the ComboBox as well as its native popup.
            if (_popup.TemplatedParent is ComboBox combo) combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
            _popup.SetCurrentValue(Popup.IsOpenProperty, false);
            DetachOwner();
        }
        private void Closed(object? sender, EventArgs e) => DetachOwner();
        private void Unloaded(object sender, RoutedEventArgs e) => Close();
        private void DetachOwner()
        {
            if (_owner is not null)
            {
                _owner.Deactivated -= OwnerChanged; _owner.StateChanged -= OwnerChanged;
                _owner.LocationChanged -= OwnerChanged; _owner.Closed -= OwnerChanged;
                _owner.IsVisibleChanged -= VisibilityChanged;
            }
            if (_anchor is not null) _anchor.IsVisibleChanged -= VisibilityChanged;
            _owner = null; _anchor = null;
        }
        public void Dispose()
        {
            Close();
            _popup.Opened -= Opened; _popup.Closed -= Closed; _popup.Unloaded -= Unloaded;
        }
    }
}
