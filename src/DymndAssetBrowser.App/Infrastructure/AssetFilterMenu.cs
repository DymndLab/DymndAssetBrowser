using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DymndAssetBrowser.Core.Models;

namespace DymndAssetBrowser.App.Infrastructure;

public sealed class AssetFilterTarget(string label)
{
    public string Label { get; } = label;
    public bool IsSelected { get; set; } = true;
}

public static class AssetFilterMenu
{
    public static MenuItem Create(IReadOnlyList<AssetFilterOption> options, Action<IReadOnlyList<string>> apply,
        Action close, Func<ModifierKeys>? modifiers = null, IReadOnlyList<AssetFilterTarget>? targets = null)
    {
        modifiers ??= () => Keyboard.Modifiers;
        var root = new MenuItem { Header = "Filter to", ToolTip = "Replaces current filters, including filename search. Keeps the selected library." };
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new Dictionary<string, MenuItem>(StringComparer.OrdinalIgnoreCase);
        var applyItem = new MenuItem { Header = "Apply selected (0)", IsEnabled = false };
        var tags = new MenuItem { Header = "Tags" };
        root.Items.Add(new MenuItem { Header = "Ctrl/Shift-click to queue; click to filter now", IsEnabled = false });
        if (targets is not null)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = "Apply to:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            foreach (var target in targets)
            {
                var box = new CheckBox { Content = target.Label, IsChecked = target.IsSelected, Margin = new Thickness(0, 2, 12, 2) };
                box.Click += (_, e) => { e.Handled = true; target.IsSelected = box.IsChecked == true; applyItem.IsEnabled = selected.Count > 0 && targets.Any(t => t.IsSelected); };
                row.Children.Add(box);
            }
            root.Items.Add(new MenuItem { Header = row, StaysOpenOnClick = true });
        }
        root.Items.Add(new Separator());
        foreach (var option in options)
        {
            var item = new MenuItem { Header = new TextBlock { Text = option.IsTag ? option.Value : $"{option.Label}: {option.Value}" },
                Tag = option.Key, IsCheckable = true, StaysOpenOnClick = true };
            items.Add(option.Key, item);
            item.Click += (_, e) =>
            {
                e.Handled = true;
                if ((modifiers() & (ModifierKeys.Control | ModifierKeys.Shift)) != 0)
                {
                    if (!selected.Add(option.Key)) selected.Remove(option.Key);
                    foreach (var pair in items) pair.Value.IsChecked = selected.Contains(pair.Key);
                    applyItem.IsEnabled = selected.Count > 0 && (targets is null || targets.Any(t => t.IsSelected));
                    applyItem.Header = $"Apply selected ({selected.Count})";
                }
                else if (targets is null || targets.Any(t => t.IsSelected)) { close(); apply([option.Key]); }
            };
            if (option.IsTag) tags.Items.Add(item); else root.Items.Add(item);
        }
        if (tags.HasItems) root.Items.Add(tags);
        root.Items.Add(new Separator());
        root.Items.Add(applyItem);
        applyItem.Click += (_, e) => { e.Handled = true; var keys = selected.ToArray(); close(); if (keys.Length > 0) apply(keys); };
        return root;
    }
}
