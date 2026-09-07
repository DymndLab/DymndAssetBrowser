namespace FAFamilyBrowser.Core.Models;

public static class BuildModeService
{
    private const string Building = "Building";

    public static IReadOnlyList<string> ValuesForFacet(IEnumerable<AssetRecord> assets, BuildComponent component,
        BuildSelection selection, AssetFacet facet, string sourceId = AssetQueries.All)
    {
        var scoped = Scope(assets, component);
        return AssetQueries.ValuesForFacet(scoped, selection.ToFilter(sourceId), facet);
    }

    public static IReadOnlyList<string> ValuesForFacet(AssetCatalogIndex index, BuildComponent component,
        BuildSelection selection, AssetFacet facet, string sourceId = AssetQueries.All)
    {
        var (group, subGroup) = ScopeFor(component);
        var fixedScope = new AssetFilter(Group: group, SubGroup: subGroup, SourceId: sourceId);
        if (component is BuildComponent.Floor or BuildComponent.Trim)
            return index.ValuesForFacetWhere(selection.ToFilter(sourceId), facet,
                asset => IsInScope(asset, component), fixedScope);
        return index.ValuesForFacet(selection.ToFilter(sourceId), facet, fixedScope);
    }

    public static BuildPalette Populate(IEnumerable<AssetRecord> assets, BuildRecipe recipe,
        IReadOnlyDictionary<string, RibbonMapping> mappings, string sourceId = AssetQueries.All)
    {
        var materialized = assets.ToList();
        return Populate(new AssetCatalogIndex(materialized), WallConstructionCatalog.Build(materialized), recipe, mappings, sourceId);
    }

    public static BuildPalette Populate(AssetCatalogIndex index, BuildRecipe recipe,
        IReadOnlyDictionary<string, RibbonMapping> mappings, string sourceId = AssetQueries.All)
        => Populate(index, WallConstructionCatalog.Build(index.Assets), recipe, mappings, sourceId);

    public static BuildPalette Populate(AssetCatalogIndex index, WallConstructionCatalog wallCatalog,
        BuildRecipe recipe, IReadOnlyDictionary<string, RibbonMapping> mappings,
        string sourceId = AssetQueries.All)
    {
        var wallSets = wallCatalog.Filter(recipe.WallSet, sourceId);
        var selectedWallSet = wallSets.Count == 1 ? wallSets[0] : null;
        var walls = selectedWallSet?.WallPieces ?? [];
        var wallComponents = selectedWallSet is null
            ? []
            : MatchWallComponents(index.Assets, selectedWallSet, recipe.Trim);
        var wallDetails = MergeDetails(selectedWallSet?.Details ?? [],
            MatchThemeDetails(index.Assets, recipe.WallSet, sourceId));
        var floors = MatchSelection(index, BuildComponent.Floor, recipe.Floor, sourceId);
        var openings = MatchSelection(index, BuildComponent.Trim, recipe.Trim, sourceId);
        var doorFrames = openings.Where(IsDoorFrame).ToList();
        var windowSills = openings.Where(IsWindowSill).ToList();
        var otherOpenings = openings.Where(asset => !IsDoorFrame(asset) && !IsWindowSill(asset) && !IsWallBuildComponent(asset)).ToList();

        return new BuildPalette(walls, wallComponents, wallDetails, floors, doorFrames, windowSills, otherOpenings,
            ResolveWallRibbon(selectedWallSet, recipe.WallSet, mappings, wallSets.Count));
    }

    public static BuildPalette PreviewWallSet(AssetCatalogIndex index, WallConstructionCatalog wallCatalog,
        WallSetSelection selection, IReadOnlyDictionary<string, RibbonMapping> mappings,
        BuildSelection trim, string sourceId = AssetQueries.All)
    {
        var wallSets = wallCatalog.Filter(selection, sourceId);
        var selectedWallSet = wallSets.Count == 1 ? wallSets[0] : null;
        var wallDetails = MergeDetails(selectedWallSet?.Details ?? [], MatchThemeDetails(index.Assets, selection, sourceId));
        return new BuildPalette(
            selectedWallSet?.WallPieces ?? [],
            selectedWallSet is null ? [] : MatchWallComponents(index.Assets, selectedWallSet, trim),
            wallDetails,
            [], [], [], [],
            ResolveWallRibbon(selectedWallSet, selection, mappings, wallSets.Count));
    }

    public static IReadOnlyList<AssetRecord> MatchThemeDetails(IEnumerable<AssetRecord> assets,
        WallSetSelection selection, string sourceId = AssetQueries.All)
    {
        if (IsAll(selection.Theme)) return [];
        return assets
            .Where(asset => IsAll(sourceId) || asset.SourceId.Equals(sourceId, StringComparison.OrdinalIgnoreCase))
            .Where(asset => asset.Theme.Equals(selection.Theme, StringComparison.OrdinalIgnoreCase))
            .Where(asset => AssetSemanticClassifier.Classify(asset) is { Type: "Terrain" } semantic
                && semantic.Subtype is "Snow Cover & Edges" or "Frost & Snow Overlays")
            .OrderBy(asset => AssetSemanticClassifier.Classify(asset).Subtype, StringComparer.OrdinalIgnoreCase)
            .ThenBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<AssetRecord> MergeDetails(IEnumerable<AssetRecord> wallDetails,
        IEnumerable<AssetRecord> themeDetails) => wallDetails.Concat(themeDetails)
        .DistinctBy(asset => asset.StableIdentity, StringComparer.OrdinalIgnoreCase)
        .OrderBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public static string WallMappingKey(WallConstructionSet set) => set.Id;

    public static BuildSelection Canonicalize(BuildSelection selection, BuildComponent component)
    {
        var (_, subGroup) = ScopeFor(component);
        return selection with { Group = Building, SubGroup = subGroup };
    }

    public static List<AssetRecord> MatchSelection(AssetCatalogIndex index, BuildComponent component,
        BuildSelection selection, string sourceId = AssetQueries.All)
    {
        var canonical = Canonicalize(selection, component);
        if (!string.IsNullOrWhiteSpace(canonical.AssetIdentity))
        {
            var exact = index.Assets.FirstOrDefault(asset =>
                asset.StableIdentity.Equals(canonical.AssetIdentity, StringComparison.OrdinalIgnoreCase)
                && (sourceId.Equals(AssetQueries.All, StringComparison.OrdinalIgnoreCase)
                    || asset.SourceId.Equals(sourceId, StringComparison.OrdinalIgnoreCase))
                && IsInScope(asset, component));
            return exact is null ? [] : [exact];
        }
        return index.Filter(canonical.ToFilter(sourceId)).Where(asset => IsInScope(asset, component)).ToList();
    }

    private static IEnumerable<AssetRecord> Scope(IEnumerable<AssetRecord> assets, BuildComponent component)
    {
        return assets.Where(asset => IsInScope(asset, component));
    }

    private static bool IsInScope(AssetRecord asset, BuildComponent component)
    {
        var (group, subGroup) = ScopeFor(component);
        if (!asset.Group.Equals(group, StringComparison.OrdinalIgnoreCase)
            || !asset.SubGroup.Equals(subGroup, StringComparison.OrdinalIgnoreCase)) return false;
        if (component == BuildComponent.Floor)
        {
            var semantic = AssetSemanticClassifier.Classify(asset);
            return semantic.Type.Equals("Floors", StringComparison.OrdinalIgnoreCase)
                && semantic.Subtype.Equals("Surfaces", StringComparison.OrdinalIgnoreCase);
        }
        // Surface textures such as the four core Glass sheets are painting materials, not physical trim pieces.
        return component != BuildComponent.Trim
            || !asset.RelativePath.Replace('\\', '/').Contains("/Textures/", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<AssetRecord> MatchSelection(IEnumerable<AssetRecord> assets, BuildSelection selection)
    {
        var filter = selection with { Group = AssetQueries.All, SubGroup = AssetQueries.All };
        return AssetQueries.Filter(assets, filter.ToFilter());
    }

    private static List<AssetRecord> MatchWallComponents(IEnumerable<AssetRecord> assets,
        WallConstructionSet wallSet, BuildSelection trim)
    {
        return assets
            .Where(asset => asset.SourceId.Equals(wallSet.SourceId, StringComparison.OrdinalIgnoreCase))
            .Where(IsWallBuildComponent)
            .Where(asset => WallComponentApplies(asset, wallSet, trim))
            .OrderBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsWallBuildComponent(AssetRecord asset)
    {
        var path = asset.RelativePath.Replace('\\', '/');
        return (asset.FileName.StartsWith("Arrowslit_", StringComparison.OrdinalIgnoreCase)
                && path.Contains("/Windows/Arrowslits/", StringComparison.OrdinalIgnoreCase))
            || (asset.FileName.StartsWith("Arched_Wall_", StringComparison.OrdinalIgnoreCase)
                && path.Contains("/Arches/", StringComparison.OrdinalIgnoreCase));
    }

    private static bool WallComponentApplies(AssetRecord asset, WallConstructionSet wallSet, BuildSelection trim)
    {
        var tokens = Path.GetFileNameWithoutExtension(asset.FileName)
            .Split('_', StringSplitOptions.RemoveEmptyEntries);
        var wallMasonryAppearance = ComponentAppearance(wallSet, "Stone", "Brick", "Adobe", "Tile", "Mosaic Tile", "Plaster");
        var componentMasonryAppearance = AppearanceFollowing(tokens, "Stone", "Brick", "Adobe", "Tile", "Marble", "Plaster");
        if (string.IsNullOrWhiteSpace(wallMasonryAppearance)
            || string.IsNullOrWhiteSpace(componentMasonryAppearance)
            || !wallMasonryAppearance.Equals(componentMasonryAppearance, StringComparison.OrdinalIgnoreCase)) return false;

        if (!asset.FileName.StartsWith("Arrowslit_", StringComparison.OrdinalIgnoreCase)) return true;
        var appearances = AssetSemanticClassifier.Classify(asset).Appearances;
        var woodFinish = appearances.FirstOrDefault(value => !value.Equals(componentMasonryAppearance, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(woodFinish)) return true;

        var wallWood = ComponentAppearance(wallSet, "Wood");
        if (!string.IsNullOrWhiteSpace(wallWood))
            return woodFinish.Equals(wallWood, StringComparison.OrdinalIgnoreCase);
        if (trim.Material.Equals("Wood", StringComparison.OrdinalIgnoreCase)
            && !IsAll(trim.Style)) return woodFinish.Equals(trim.Style, StringComparison.OrdinalIgnoreCase);
        return IsAll(trim.Material) || IsAll(trim.Style);
    }

    private static string? ComponentAppearance(WallConstructionSet set, params string[] components)
    {
        if (components.Contains(set.PrimaryComponent, StringComparer.OrdinalIgnoreCase)) return set.PrimaryAppearance;
        if (components.Contains(set.SecondaryComponent, StringComparer.OrdinalIgnoreCase)) return set.SecondaryAppearance;
        return null;
    }

    private static string? AppearanceFollowing(IReadOnlyList<string> tokens, params string[] materials)
    {
        for (var index = 0; index + 1 < tokens.Count; index++)
            if (materials.Contains(tokens[index], StringComparer.OrdinalIgnoreCase)) return tokens[index + 1];
        return null;
    }

    private static bool IsAll(string? value) => string.IsNullOrWhiteSpace(value)
        || value.Equals(AssetQueries.All, StringComparison.OrdinalIgnoreCase);

    private static WallRibbonResolution ResolveWallRibbon(WallConstructionSet? set, WallSetSelection selection,
        IReadOnlyDictionary<string, RibbonMapping> mappings, int candidateCount)
    {
        var key = set?.Id ?? selection.SetId;
        if (set is null)
            return new WallRibbonResolution(candidateCount > 1 ? WallRibbonResolutionStatus.Ambiguous : WallRibbonResolutionStatus.Missing,
                key, null, candidateCount);
        if (mappings.TryGetValue(set.Id, out var exact) && HasRibbon(exact))
            return new WallRibbonResolution(WallRibbonResolutionStatus.Resolved, set.Id, exact, 1);
        return set.AutomaticRibbon with { MappingKey = set.Id };
    }
    private static bool HasRibbon(RibbonMapping mapping) => !string.IsNullOrWhiteSpace(mapping.CspTool)
        || !string.IsNullOrWhiteSpace(mapping.CspToolGroup) || !string.IsNullOrWhiteSpace(mapping.CspRibbonName);
    private static string MappingIdentity(RibbonMapping mapping) => string.Join('|', mapping.CspTool.Trim(),
        mapping.CspToolGroup.Trim(), mapping.CspRibbonName.Trim());
    private static bool IsDoorFrame(AssetRecord asset) => asset.Family.Equals("Door Frames", StringComparison.OrdinalIgnoreCase)
        || asset.FileName.Contains("Door_Frame", StringComparison.OrdinalIgnoreCase);
    private static bool IsWindowSill(AssetRecord asset) => asset.Family.Equals("Window Sills", StringComparison.OrdinalIgnoreCase)
        || asset.FileName.Contains("Window_Sill", StringComparison.OrdinalIgnoreCase);

    private static (string Group, string SubGroup) ScopeFor(BuildComponent component) => component switch
    {
        BuildComponent.Walls => (Building, "Walls"),
        BuildComponent.Floor => (Building, "Floors"),
        BuildComponent.Trim => (Building, "Openings"),
        _ => throw new ArgumentOutOfRangeException(nameof(component), component, null)
    };
}
