using System.Windows;

namespace FAFamilyBrowser.App.ViewModels;

public sealed record BrowserDisplayRowViewModel
{
    private BrowserDisplayRowViewModel(string name, IReadOnlyList<AssetTileViewModel> assets, bool isHeader, bool isCollapsed = false)
    {
        Name = name;
        Assets = assets;
        IsHeader = isHeader;
        IsCollapsed = isCollapsed;
    }

    public string Name { get; }
    public IReadOnlyList<AssetTileViewModel> Assets { get; }
    public bool IsHeader { get; }
    public bool IsCollapsed { get; }
    public string HeaderText => $"{(IsCollapsed ? "▶" : "▼")} {Name}";
    public Visibility HeaderVisibility => IsHeader ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AssetsVisibility => IsHeader ? Visibility.Collapsed : Visibility.Visible;

    public static BrowserDisplayRowViewModel Header(string name, bool isCollapsed) => new(name, [], true, isCollapsed);
    public static BrowserDisplayRowViewModel AssetRow(IReadOnlyList<AssetTileViewModel> assets) => new(string.Empty, assets, false);
}
