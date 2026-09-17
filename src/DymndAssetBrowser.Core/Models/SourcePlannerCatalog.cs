namespace DymndAssetBrowser.Core.Models;

// Source facets are authoritative for discovery; construction-set membership and
// floor/opening eligibility remain the existing, tested building-domain rules.
public sealed class SourcePlannerCatalog
{
    public static SourcePlannerCatalog Empty { get; } = new(new SourceBrowserIndex([]), AssetCatalogIndex.Empty, WallConstructionCatalog.Empty);
    private readonly WallConstructionCatalog _walls;
    private readonly AssetCatalogIndex _semantic;
    private readonly Dictionary<BuildComponent, SourceBrowserIndex> _scopes;
    public SourcePlannerCatalog(SourceBrowserIndex source, AssetCatalogIndex semantic, WallConstructionCatalog walls)
    {
        _walls = walls; _semantic = semantic;
        var wallIds = walls.Sets.SelectMany(s => s.WallPieces).Select(a => a.StableIdentity).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var floorIds = BuildModeService.MatchSelection(semantic, BuildComponent.Floor, new()).Select(a => a.StableIdentity).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var trimIds = BuildModeService.MatchSelection(semantic, BuildComponent.Trim, new()).Select(a => a.StableIdentity).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _scopes = new()
        {
            [BuildComponent.Walls] = new(source.Entries.Where(e => wallIds.Contains(e.Asset.StableIdentity))),
            [BuildComponent.Floor] = new(source.Entries.Where(e => floorIds.Contains(e.Asset.StableIdentity))),
            [BuildComponent.Trim] = new(source.Entries.Where(e => trimIds.Contains(e.Asset.StableIdentity)))
        };
    }
    public SourceBrowserIndex Scope(BuildComponent component) => _scopes[component];
    public IReadOnlyList<WallConstructionSet> WallSets(PlannerSourceSelection selection, string sourceId)
    {
        var ids = Scope(BuildComponent.Walls).Filter(selection.Filter with { SourceId = sourceId })
            .Select(e => e.Asset.StableIdentity).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _walls.Sets.Where(s => (selection.SelectedIdentity.Length == 0 || s.Id.Equals(selection.SelectedIdentity, StringComparison.OrdinalIgnoreCase))
            && s.WallPieces.Any(a => ids.Contains(a.StableIdentity))).ToArray();
    }
    public List<AssetRecord> Assets(BuildComponent component, PlannerSourceSelection selection, string sourceId) =>
        Scope(component).Filter(selection.Filter with { SourceId = sourceId })
            .Where(e => selection.SelectedIdentity.Length == 0 || e.Asset.StableIdentity.Equals(selection.SelectedIdentity, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Asset).ToList();
    public BuildPalette Populate(SourcePlannerRecipe recipe, IReadOnlyDictionary<string, RibbonMapping> mappings, string sourceId)
    {
        var sets = WallSets(recipe.Walls, sourceId);
        // An explicit nonmatching ID prevents an ambiguous/empty source query from
        // accidentally becoming the semantic catalog's unfiltered wall selection.
        var selection = sets.Count == 1 ? new WallSetSelection { SetId = sets[0].Id, Theme = sets[0].Theme }
            : new WallSetSelection { SetId = "source-planner:no-single-set" };
        var wallPalette = BuildModeService.PreviewWallSet(_semantic, _walls, selection, mappings, new(), sourceId);
        var trim = Assets(BuildComponent.Trim, recipe.Trim, sourceId);
        return wallPalette with
        {
            Floors = Assets(BuildComponent.Floor, recipe.Floor, sourceId),
            DoorFrames = trim.Where(BuildModeService.IsDoorFrame).ToArray(),
            WindowSills = trim.Where(BuildModeService.IsWindowSill).ToArray(),
            OtherOpenings = trim.Where(a => !BuildModeService.IsDoorFrame(a) && !BuildModeService.IsWindowSill(a))
                .ExceptBy(wallPalette.WallComponents.Select(a => a.StableIdentity), a => a.StableIdentity).ToArray()
        };
    }
    public SourcePlannerRecipe Migrate(BuildRecipe old, string sourceId)
    {
        if (old.SourcePlanner is not null) return old.SourcePlanner;
        var sets = _walls.Filter(old.WallSet, sourceId);
        var wallEntries = sets.SelectMany(s => s.WallPieces).Select(a => a.StableIdentity).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var wallFilter = CommonFilter(Scope(BuildComponent.Walls).Entries.Where(e => wallEntries.Contains(e.Asset.StableIdentity)).ToArray());
        return new()
        {
            Walls = new() { Filter = wallFilter, SelectedIdentity = sets.Count == 1 ? sets[0].Id : "" },
            Floor = ConvertSelection(old.Floor, BuildComponent.Floor),
            Trim = ConvertSelection(old.Trim, BuildComponent.Trim)
        };
        PlannerSourceSelection ConvertSelection(BuildSelection selection, BuildComponent component)
        {
            var ids = BuildModeService.MatchSelection(_semantic, component, selection, sourceId).Select(a => a.StableIdentity).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var entries = Scope(component).Entries.Where(e => ids.Contains(e.Asset.StableIdentity)).ToArray();
            var filter = CommonFilter(entries);
            var styleTags = selection.Style.Split(['_', ' '], StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t != "All" && t != "Unspecified" && entries.Length > 0 && entries.All(e => e.Tags.Contains(t, StringComparer.OrdinalIgnoreCase))).ToArray();
            return new() { Filter = filter with { Tags = styleTags, AllTags = true, Variant = selection.Variant }, SelectedIdentity = selection.AssetIdentity };
        }
    }
    private static SourceBrowserFilter CommonFilter(IReadOnlyList<SourceBrowserIndex.Entry> entries)
    {
        if (entries.Count == 0) return new();
        var biomes = entries.Select(e => e.Taxonomy.Biome).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var collections = entries.Select(e => e.Taxonomy.Collection).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var materials = entries[0].Taxonomy.Materials.Select(m => m.Key)
            .Where(k => entries.All(e => e.Taxonomy.Materials.Any(m => m.Key.Equals(k, StringComparison.OrdinalIgnoreCase)))).ToArray();
        return new() { Biomes = biomes.Length == 1 && biomes[0].Length > 0 ? biomes : [],
            Collection = collections.Length == 1 && collections[0].Length > 0 ? collections[0] : "All", Materials = materials, AllMaterials = true };
    }
}
