using DymndAssetBrowser.Core.Models;

namespace DymndAssetBrowser.App.ViewModels;

public sealed partial class MainViewModel
{
    private SourcePlannerCatalog _sourcePlannerCatalog = SourcePlannerCatalog.Empty;
    private bool _batchPlannerChanges;
    public PlannerFilterViewModel WallsPlannerFilter { get; private set; } = null!;
    public PlannerFilterViewModel FloorPlannerFilter { get; private set; } = null!;
    public PlannerFilterViewModel TrimPlannerFilter { get; private set; } = null!;
    public IReadOnlyList<WallConstructionSet> PlannerWallSets => _sourcePlannerCatalog.WallSets(WallsPlannerFilter.Selection, SelectedSourceId);
    public WallConstructionSet? PlannerSelectedWallSet => PlannerWallSets is { Count: 1 } sets ? sets[0] : null;
    private void InitializePlannerFilters()
    {
        WallsPlannerFilter = new(BuildComponent.Walls, () => _sourcePlannerCatalog, () => SelectedSourceId);
        FloorPlannerFilter = new(BuildComponent.Floor, () => _sourcePlannerCatalog, () => SelectedSourceId);
        TrimPlannerFilter = new(BuildComponent.Trim, () => _sourcePlannerCatalog, () => SelectedSourceId);
        foreach (var filter in new[] { WallsPlannerFilter, FloorPlannerFilter, TrimPlannerFilter })
            filter.SelectionChanged += PlannerSourceFiltersChanged;
    }
    private SourcePlannerRecipe CurrentSourcePlannerRecipe() => new()
    { Walls = WallsPlannerFilter.Selection, Floor = FloorPlannerFilter.Selection, Trim = TrimPlannerFilter.Selection };
    private void PlannerSourceFiltersChanged(object? sender, EventArgs e)
    {
        if (!_plannerReady || _batchPlannerChanges) return;
        CancelBuildRefresh(); _buildIsPopulated = false;
        RefreshWallSetPreview(); NotifyBuildSummaries(); OnPropertyChanged(nameof(BuildSummary));
        StatusText = WallPreviewStatus();
    }
    private PlannerFilterViewModel PlannerFilter(BuildComponent component) => component switch
    { BuildComponent.Walls => WallsPlannerFilter, BuildComponent.Floor => FloorPlannerFilter, _ => TrimPlannerFilter };

    private SourceBrowserIndex.Entry? PlannerEntry(AssetTileViewModel tile)
    {
        var component = PlannerComponentFor(tile);
        if (!IsBuildMode || component is null) return null;
        return _sourceBrowser.Entries.FirstOrDefault(e => e.Asset.StableIdentity == tile.Asset.StableIdentity);
    }
    public IReadOnlyList<AssetFilterOption> PlannerFilterOptions(AssetTileViewModel tile) => PlannerEntry(tile) is { } entry
        ? AssetFilterShortcut.Options(entry).Where(o => o.Key is "Biome" or "Collection" or "Variant" || o.IsTag || o.Key.StartsWith("Material:")).ToArray() : [];

    public bool ApplyPlannerFilter(AssetTileViewModel tile, string key) => ApplyPlannerFilters(tile, [key], [BuildComponent.Walls, BuildComponent.Floor, BuildComponent.Trim]);

    public bool ApplyPlannerFilters(AssetTileViewModel tile, IEnumerable<string> keys, IEnumerable<BuildComponent> targets)
    {
        var entry = PlannerEntry(tile);
        var destinations = targets.Where(Enum.IsDefined).Distinct().ToArray();
        if (entry is null || destinations.Length == 0) return false;
        var allowed = PlannerFilterOptions(tile).Select(o => o.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var updated = AssetFilterShortcut.Create(entry, keys.Where(allowed.Contains), SelectedSourceId);
        if (updated is null) return false;
        // Like Browser shortcuts, replace filters with the explicitly chosen
        // characteristics. Do not carry invisible pins or old constraints over.
        foreach (var target in destinations)
        {
            PlannerFilter(target).Restore(new() { Filter = updated });
        }
        PlannerSourceFiltersChanged(this, EventArgs.Empty);
        StatusText = $"Applied selected asset filters to {string.Join(", ", destinations)}. Other sections unchanged.";
        return true;
    }

    public bool IsPlannerWall(AssetTileViewModel tile) => IsBuildMode && PlannerComponentFor(tile) == BuildComponent.Walls;

    public IReadOnlyList<string> WallMaterialsForTrim(AssetTileViewModel tile)
    {
        if (!IsBuildMode || PlannerComponentFor(tile) != BuildComponent.Walls) return [];
        var entries = _sourcePlannerCatalog.Scope(BuildComponent.Walls).Entries;
        var entry = entries.FirstOrDefault(e => e.Asset.StableIdentity == tile.Asset.StableIdentity);
        // Detailing/connector tiles can be outside the primary wall scope.
        if (entry is null && WallSetFor(tile) is { } set)
            entry = entries.FirstOrDefault(e => set.WallPieces.Any(a => a.StableIdentity == e.Asset.StableIdentity));
        return entry?.Taxonomy.Materials.Select(m => m.Key).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
    }

    public bool UseWallMaterialsForTrim(AssetTileViewModel tile)
    {
        var materials = WallMaterialsForTrim(tile);
        if (materials.Count == 0) return false;
        TrimPlannerFilter.SelectMaterials(materials, matchAll: false, exactFinishes: true);
        StatusText = $"Trim materials: {string.Join(", ", materials)} (matching finishes only; single or mixed pieces). Other Trim filters unchanged. {TrimPlannerFilter.MatchingCount:N0} matching assets.";
        return true;
    }
}
