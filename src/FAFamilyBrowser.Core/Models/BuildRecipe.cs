namespace FAFamilyBrowser.Core.Models;

public enum BuildComponent
{
    Walls,
    Floor,
    Trim
}

public sealed record BuildSelection
{
    public string AssetIdentity { get; init; } = string.Empty;
    public string Group { get; init; } = AssetQueries.All;
    public string SubGroup { get; init; } = AssetQueries.All;
    public string Material { get; init; } = AssetQueries.All;
    public string Style { get; init; } = AssetQueries.All;
    public string Theme { get; init; } = AssetQueries.All;
    public string Family { get; init; } = AssetQueries.All;
    public string Variant { get; init; } = AssetQueries.All;

    public AssetFilter ToFilter(string sourceId = AssetQueries.All) => new(
        Group: Group,
        SubGroup: SubGroup,
        Material: Material,
        Style: Style,
        Theme: Theme,
        Family: Family,
        SourceId: sourceId,
        Variant: Variant);
}

public sealed record BuildRecipe
{
    public WallSetSelection WallSet { get; init; } = new();
    public BuildSelection Floor { get; init; } = new() { Group = "Building", SubGroup = "Floors" };
    public BuildSelection Trim { get; init; } = new() { Group = "Building", SubGroup = "Openings" };
    public bool IsPopulated { get; init; }
}

public enum WallRibbonResolutionStatus
{
    Resolved,
    Missing,
    Ambiguous
}

public sealed record WallRibbonResolution(
    WallRibbonResolutionStatus Status,
    string MappingKey,
    RibbonMapping? Mapping = null,
    int CandidateCount = 0);

public sealed record BuildPalette(
    IReadOnlyList<AssetRecord> Walls,
    IReadOnlyList<AssetRecord> WallComponents,
    IReadOnlyList<AssetRecord> WallDetails,
    IReadOnlyList<AssetRecord> Floors,
    IReadOnlyList<AssetRecord> DoorFrames,
    IReadOnlyList<AssetRecord> WindowSills,
    IReadOnlyList<AssetRecord> OtherOpenings,
    WallRibbonResolution WallRibbon);
