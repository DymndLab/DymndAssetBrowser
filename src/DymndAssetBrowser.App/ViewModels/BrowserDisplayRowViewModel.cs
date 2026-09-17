using System.Windows;
using DymndAssetBrowser.App.Infrastructure;

namespace DymndAssetBrowser.App.ViewModels;

public sealed class BrowserDisplayRowViewModel : ViewModelBase
{
    private BrowserDisplayRowViewModel(string name, IReadOnlyList<AssetTileViewModel> assets, bool isHeader, bool isCollapsed = false, int count = 0)
    {
        Name = name;
        Assets = assets;
        IsHeader = isHeader;
        IsCollapsed = isCollapsed;
        Count = count;
    }

    public string Name { get; }
    public IReadOnlyList<AssetTileViewModel> Assets { get; }
    public bool IsHeader { get; }
    private bool _isCollapsed;
    public bool IsCollapsed
    {
        get => _isCollapsed;
        private set { if (SetProperty(ref _isCollapsed, value)) OnPropertyChanged(nameof(HeaderText)); }
    }
    public void SetCollapsed(bool value) => IsCollapsed = value;
    public int Count { get; }
    public string HeaderText => $"{(IsCollapsed ? "▶" : "▼")} {Name} ({Count:N0})";
    public Visibility HeaderVisibility => IsHeader ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AssetsVisibility => IsHeader ? Visibility.Collapsed : Visibility.Visible;

    public static BrowserDisplayRowViewModel Header(string name, bool isCollapsed, int count) => new(name, [], true, isCollapsed, count);
    public static BrowserDisplayRowViewModel AssetRow(IReadOnlyList<AssetTileViewModel> assets) => new(string.Empty, assets, false);
}
