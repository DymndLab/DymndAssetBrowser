using System.Windows;

namespace FAFamilyBrowser.App.ViewModels;

public enum BuildPaletteSection
{
    WallSetSamples,
    FloorSamples,
    TrimSamples,
    Walls,
    WallComponents,
    Detailing,
    Floors,
    DoorFrames,
    WindowSills,
    OtherOpenings
}

public sealed record BuildPaletteDisplayRowViewModel
{
    public bool IsHeader { get; init; }
    public string LeftHeader { get; init; } = string.Empty;
    public string RightHeader { get; init; } = string.Empty;
    public BuildPaletteSection LeftSection { get; init; }
    public BuildPaletteSection RightSection { get; init; }
    public IReadOnlyList<AssetTileViewModel> LeftAssets { get; init; } = [];
    public IReadOnlyList<AssetTileViewModel> RightAssets { get; init; } = [];
    public bool IsFullWidth { get; init; }
    public bool LeftHeaderHasBody { get; init; }
    public bool RightHeaderHasBody { get; init; }
    public bool IsLeftLastBodyRow { get; init; }
    public bool IsRightLastBodyRow { get; init; }
    public Thickness HeaderMargin { get; init; }
    public Visibility HeaderVisibility => IsHeader ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AssetsVisibility => IsHeader ? Visibility.Collapsed : Visibility.Visible;
    public Visibility LeftHeaderVisibility => IsHeader && !string.IsNullOrEmpty(LeftHeader) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RightHeaderVisibility => IsHeader && !string.IsNullOrEmpty(RightHeader) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility LeftBodyVisibility => !IsHeader && LeftAssets.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RightBodyVisibility => !IsHeader && RightAssets.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public int LeftColumnSpan => IsFullWidth ? 2 : 1;
    public Thickness LeftHeaderBorderThickness => LeftHeaderHasBody ? new(1, 1, 1, 0) : new(1);
    public Thickness RightHeaderBorderThickness => RightHeaderHasBody ? new(1, 1, 1, 0) : new(1);
    public CornerRadius LeftHeaderCornerRadius => LeftHeaderHasBody ? new(3, 3, 0, 0) : new(3);
    public CornerRadius RightHeaderCornerRadius => RightHeaderHasBody ? new(3, 3, 0, 0) : new(3);
    public Thickness LeftBodyBorderThickness => IsLeftLastBodyRow ? new(1, 0, 1, 1) : new(1, 0, 1, 0);
    public Thickness RightBodyBorderThickness => IsRightLastBodyRow ? new(1, 0, 1, 1) : new(1, 0, 1, 0);
    public CornerRadius LeftBodyCornerRadius => IsLeftLastBodyRow ? new(0, 0, 3, 3) : new(0);
    public CornerRadius RightBodyCornerRadius => IsRightLastBodyRow ? new(0, 0, 3, 3) : new(0);
}
