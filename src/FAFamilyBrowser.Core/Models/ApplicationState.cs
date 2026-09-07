namespace FAFamilyBrowser.Core.Models;

public sealed record ApplicationState
{
    public int SchemaVersion { get; init; } = 6;
    public List<AssetLibrarySource> Libraries { get; init; } = [];
    public List<AssetRecord> Assets { get; init; } = [];
    public Dictionary<string, RibbonMapping> RibbonMappings { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? LastGroup { get; init; }
    public string? LastSubGroup { get; init; }
    public string? LastMaterial { get; init; }
    public string? LastStyle { get; init; }
    public string? LastTheme { get; init; }
    public string? LastFamily { get; init; }
    public string? LastVariant { get; init; }
    public string? LastSourceSet { get; init; }
    public string? LastFilenameVariant { get; init; }
    public string? LastCategory { get; init; }
    public string? LastType { get; init; }
    public string? LastSubtype { get; init; }
    public string? LastContext { get; init; }
    public string? LastSourceId { get; init; }
    public string? CacheRoot { get; init; }
    public BuildRecipe BuildRecipe { get; init; } = new();
}
