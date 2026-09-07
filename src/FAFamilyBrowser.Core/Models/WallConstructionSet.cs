namespace FAFamilyBrowser.Core.Models;

public enum WallSetMembershipKind
{
    WallPiece,
    Detailing
}

public sealed record WallComponentAppearance(string Component, string Appearance);

public sealed record WallSetSelection
{
    public string SetId { get; init; } = AssetQueries.All;
    public string WallSystem { get; init; } = AssetQueries.All;
    public string PrimaryAppearance { get; init; } = AssetQueries.All;
    public string SecondaryAppearance { get; init; } = AssetQueries.All;
    public string Theme { get; init; } = AssetQueries.All;
    public string Profile { get; init; } = AssetQueries.All;
    public string Variant { get; init; } = AssetQueries.All;

    public bool IsEmpty => new[] { SetId, WallSystem, PrimaryAppearance, SecondaryAppearance, Theme, Profile, Variant }
        .All(IsAll);

    private static bool IsAll(string? value) => string.IsNullOrWhiteSpace(value)
        || value.Equals(AssetQueries.All, StringComparison.OrdinalIgnoreCase);
}

public sealed record WallConstructionSet
{
    public required string Id { get; init; }
    public required string SourceId { get; init; }
    public required string DisplayName { get; init; }
    public required string WallSystem { get; init; }
    public string PrimaryComponent { get; init; } = "Appearance";
    public string PrimaryAppearance { get; init; } = "Unspecified";
    public string SecondaryComponent { get; init; } = string.Empty;
    public string SecondaryAppearance { get; init; } = "Unspecified";
    public string Theme { get; init; } = "Unspecified";
    public string Profile { get; init; } = "Unspecified";
    public string Variant { get; init; } = "Unspecified";
    public IReadOnlyList<AssetRecord> WallPieces { get; init; } = [];
    public IReadOnlyList<AssetRecord> Details { get; init; } = [];
    public WallRibbonResolution AutomaticRibbon { get; init; } = new(WallRibbonResolutionStatus.Missing, string.Empty);
}

public sealed class WallConstructionCatalog
{
    private static readonly HashSet<string> AppearanceValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ashen", "Beige", "Black", "Blue", "Brass", "Bronze", "Brown", "Copper", "Dark", "Earthy",
        "Frosty", "Gold", "Gray", "Green", "Light", "Pale", "Polished", "Purple", "Red", "Redrock",
        "Rusty", "Sandstone", "Silver", "Slate", "Soot", "Terracotta", "Volcanic", "Walnut", "White",
        "Mudbrick Light", "Mudbrick Red"
    };

    public static WallConstructionCatalog Empty { get; } = new([]);

    public WallConstructionCatalog(IReadOnlyList<WallConstructionSet> sets) => Sets = sets;

    public IReadOnlyList<WallConstructionSet> Sets { get; }

    public static WallConstructionCatalog Build(IEnumerable<AssetRecord> assets)
    {
        var all = assets.ToList();
        var wallAssets = all.Where(IsWallPiece).ToList();
        var details = all.Where(IsWallDetail).ToList();
        var sets = new List<WallConstructionSet>();
        var groups = wallAssets.GroupBy(SetGroupingKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase).ToList())
            .ToList();
        var definingGroups = groups.Where(group => group.Any(IsPathAnchor)
            || !group[0].SourceId.Equals(LibrarySourceIds.ForgottenAdventures, StringComparison.OrdinalIgnoreCase)).ToList();
        var sharedPieces = groups.Except(definingGroups).SelectMany(group => group).ToList();

        foreach (var definingMembers in definingGroups)
        {
            var representative = definingMembers.FirstOrDefault(IsPathAnchor) ?? definingMembers[0];
            var descriptor = Describe(representative);
            var members = definingMembers.Concat(sharedPieces.Where(asset => SharedPieceApplies(asset, representative.SourceId, descriptor, representative.Theme)))
                .DistinctBy(asset => asset.StableIdentity, StringComparer.OrdinalIgnoreCase)
                .OrderBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase).ToList();
            var compatibleDetails = details.Where(detail => DetailApplies(detail, representative.SourceId, descriptor, representative.Theme))
                .OrderBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase).ToList();
            var id = $"wall-set|{SetGroupingKey(representative)}";
            var ribbon = ResolveAutomatic(definingMembers, id);
            sets.Add(new WallConstructionSet
            {
                Id = id,
                SourceId = representative.SourceId,
                DisplayName = DisplayName(descriptor),
                WallSystem = descriptor.WallSystem,
                PrimaryComponent = descriptor.PrimaryComponent,
                PrimaryAppearance = descriptor.PrimaryAppearance,
                SecondaryComponent = descriptor.SecondaryComponent,
                SecondaryAppearance = descriptor.SecondaryAppearance,
                Theme = representative.Theme,
                Profile = descriptor.Profile,
                Variant = descriptor.Variant,
                WallPieces = members,
                Details = compatibleDetails,
                AutomaticRibbon = ribbon
            });
        }

        return new WallConstructionCatalog(sets.OrderBy(set => set.DisplayName, StringComparer.OrdinalIgnoreCase).ToList());
    }

    public IReadOnlyList<WallConstructionSet> Filter(WallSetSelection selection, string sourceId = AssetQueries.All) => Sets
        .Where(set => IsAll(sourceId) || set.SourceId.Equals(sourceId, StringComparison.OrdinalIgnoreCase))
        .Where(set => Match(set.Id, selection.SetId))
        .Where(set => Match(set.WallSystem, selection.WallSystem))
        .Where(set => Match(set.PrimaryAppearance, selection.PrimaryAppearance))
        .Where(set => Match(set.SecondaryAppearance, selection.SecondaryAppearance))
        .Where(set => Match(set.Theme, selection.Theme))
        .Where(set => Match(set.Profile, selection.Profile))
        .Where(set => Match(set.Variant, selection.Variant))
        .ToList();

    public IReadOnlyList<string> Values(WallSetSelection selection, WallSetFacet facet,
        string sourceId = AssetQueries.All)
    {
        var values = Sets
            .Where(set => IsAll(sourceId) || set.SourceId.Equals(sourceId, StringComparison.OrdinalIgnoreCase))
            .Where(set => facet == WallSetFacet.Set || Match(set.Id, selection.SetId))
            .Where(set => facet == WallSetFacet.WallSystem || Match(set.WallSystem, selection.WallSystem))
            .Where(set => facet == WallSetFacet.PrimaryAppearance || Match(set.PrimaryAppearance, selection.PrimaryAppearance))
            .Where(set => facet == WallSetFacet.SecondaryAppearance || Match(set.SecondaryAppearance, selection.SecondaryAppearance))
            .Where(set => facet == WallSetFacet.Theme || Match(set.Theme, selection.Theme))
            .Where(set => facet == WallSetFacet.Profile || Match(set.Profile, selection.Profile))
            .Where(set => facet == WallSetFacet.Variant || Match(set.Variant, selection.Variant))
            .Select(set => Value(set, facet))
            .Where(IsSpecified)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);
        return new[] { AssetQueries.All }.Concat(values).ToList();
    }

    public IReadOnlyList<string> ComponentsForSystem(string wallSystem)
    {
        var set = Sets.FirstOrDefault(item => Match(item.WallSystem, wallSystem));
        return set is null || IsAll(wallSystem)
            ? []
            : new[] { set.PrimaryComponent, set.SecondaryComponent }.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
    }

    private static WallSetDescriptor Describe(AssetRecord asset)
    {
        var nameTokens = Path.GetFileNameWithoutExtension(asset.FileName)
            .Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (nameTokens.Length >= 4
            && nameTokens[0].Equals("Ceremorph", StringComparison.OrdinalIgnoreCase)
            && nameTokens[1].Equals("Wall", StringComparison.OrdinalIgnoreCase))
            return new WallSetDescriptor("Ceremorph", "Ceremorph", nameTokens[2], string.Empty,
                "Unspecified", "Unspecified", KnownVariant(asset, nameTokens));
        if (nameTokens.Length >= 4 && nameTokens[0].Equals("Flesh", StringComparison.OrdinalIgnoreCase))
        {
            var brain = nameTokens.Contains("Brain", StringComparer.OrdinalIgnoreCase);
            return new WallSetDescriptor("Flesh", "Flesh", nameTokens[1], string.Empty,
                "Unspecified", brain ? "Brain Wall" : "Flesh Wall", KnownVariant(asset, nameTokens));
        }

        var material = asset.Material.Replace('_', ' ');
        var components = asset.Material.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(Humanize).ToList();
        if (asset.Material.Equals("Mosaic_Tile", StringComparison.OrdinalIgnoreCase)) components = ["Mosaic Tile"];
        if (components.Count == 0) components.Add("Material");

        var primaryAppearance = asset.Style;
        var secondaryAppearance = "Unspecified";
        var profile = asset.Family;
        if (components.Count > 1)
        {
            var parsedAppearances = ComponentAppearancesFromName(asset, components);
            if (parsedAppearances.Primary is not null || parsedAppearances.Secondary is not null)
            {
                primaryAppearance = parsedAppearances.Primary ?? "Unspecified";
                secondaryAppearance = parsedAppearances.Secondary ?? "Unspecified";
                if (AppearanceValues.Contains(profile)) profile = "Unspecified";
            }
            else if (AppearanceValues.Contains(asset.Family))
            {
                secondaryAppearance = asset.Family;
                profile = "Unspecified";
            }
            else
            {
                secondaryAppearance = SecondaryAppearanceFromName(asset, primaryAppearance);
            }
        }

        return new WallSetDescriptor(
            material,
            components[0],
            primaryAppearance,
            components.ElementAtOrDefault(1) ?? string.Empty,
            secondaryAppearance,
            profile,
            KnownVariant(asset, nameTokens));
    }

    private static string SetGroupingKey(AssetRecord asset)
    {
        var descriptor = Describe(asset);
        return string.Join('|', asset.SourceId, descriptor.WallSystem, descriptor.PrimaryAppearance,
            descriptor.SecondaryAppearance, asset.Theme, descriptor.Profile, descriptor.Variant);
    }

    private static string KnownVariant(AssetRecord asset, IReadOnlyList<string> tokens)
    {
        if (tokens.Count >= 5 && tokens[0].Equals("Wall", StringComparison.OrdinalIgnoreCase)
            && tokens[1].Equals("Crenellations", StringComparison.OrdinalIgnoreCase)
            && tokens[2].Equals("Adobe", StringComparison.OrdinalIgnoreCase))
            return tokens[4].Length > 0 ? tokens[4][0].ToString().ToUpperInvariant() : asset.Variant;
        if (tokens.Count >= 4 && tokens[0].Equals("Ceremorph", StringComparison.OrdinalIgnoreCase))
            return tokens[3].Length > 0 ? tokens[3][0].ToString().ToUpperInvariant() : asset.Variant;
        if (tokens.Count >= 4 && tokens[0].Equals("Flesh", StringComparison.OrdinalIgnoreCase))
        {
            var candidate = tokens.FirstOrDefault(token => token.Length == 2 && token[0] is >= 'A' and <= 'M'
                && token[1] is '1' or '2');
            if (candidate is not null) return candidate.ToUpperInvariant();
        }
        return asset.Variant;
    }

    private static (string? Primary, string? Secondary) ComponentAppearancesFromName(
        AssetRecord asset, IReadOnlyList<string> components)
    {
        var tokens = Path.GetFileNameWithoutExtension(asset.FileName)
            .Split('_', StringSplitOptions.RemoveEmptyEntries);
        var appearances = tokens.Where(AppearanceValues.Contains).ToList();
        if (asset.Material.Equals("Plaster_Wood", StringComparison.OrdinalIgnoreCase))
            return (null, appearances.FirstOrDefault());
        if (asset.Material.Equals("Brick_Plaster", StringComparison.OrdinalIgnoreCase))
            return (appearances.FirstOrDefault(), appearances.Skip(1).FirstOrDefault());
        if (components.Count > 1)
            return (appearances.FirstOrDefault(), appearances.Skip(1).FirstOrDefault());
        return (appearances.FirstOrDefault(), null);
    }

    private static string SecondaryAppearanceFromName(AssetRecord asset, string primary)
    {
        var candidates = Path.GetFileNameWithoutExtension(asset.FileName)
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Where(AppearanceValues.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return candidates.FirstOrDefault(value => !value.Equals(primary, StringComparison.OrdinalIgnoreCase)) ?? "Unspecified";
    }

    private static string DisplayName(WallSetDescriptor descriptor)
    {
        var pieces = new[]
        {
            descriptor.WallSystem,
            Specified(descriptor.PrimaryAppearance) ? descriptor.PrimaryAppearance : null,
            Specified(descriptor.SecondaryAppearance) ? descriptor.SecondaryAppearance : null,
            Specified(descriptor.Profile) ? descriptor.Profile : null
        };
        return string.Join(" / ", pieces.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static WallRibbonResolution ResolveAutomatic(IReadOnlyList<AssetRecord> members, string setId)
    {
        var anchors = members.Where(IsPathAnchor).ToList();
        var candidates = (anchors.Count > 0 ? anchors : members)
            .Select(asset => FaWallRibbonCatalog.TryResolve([asset], asset.FamilyKey))
            .Where(mapping => mapping is not null)
            .Select(mapping => mapping!)
            .GroupBy(MappingIdentity, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (candidates.Count == 0 && FaWallRibbonCatalog.TryResolve(members, setId) is { } wholeSet)
            candidates.Add(wholeSet);
        return candidates.Count switch
        {
            1 => new WallRibbonResolution(WallRibbonResolutionStatus.Resolved, setId, candidates[0], 1),
            > 1 => new WallRibbonResolution(WallRibbonResolutionStatus.Ambiguous, setId, null, candidates.Count),
            _ => new WallRibbonResolution(WallRibbonResolutionStatus.Missing, setId)
        };
    }

    private static bool IsWallPiece(AssetRecord asset) => asset.Group.Equals("Building", StringComparison.OrdinalIgnoreCase)
        && asset.SubGroup.Equals("Walls", StringComparison.OrdinalIgnoreCase)
        && !IsWallDetail(asset);

    private static bool IsWallDetail(AssetRecord asset) => AssetSemanticClassifier.IsWallDetail(asset);

    private static bool DetailApplies(AssetRecord detail, string sourceId, WallSetDescriptor set, string setTheme)
    {
        if (!detail.SourceId.Equals(sourceId, StringComparison.OrdinalIgnoreCase)) return false;
        if (detail.FileName.Contains("Adobe", StringComparison.OrdinalIgnoreCase)
            && detail.FileName.Contains("Crack", StringComparison.OrdinalIgnoreCase))
            return set.WallSystem.Equals("Adobe", StringComparison.OrdinalIgnoreCase)
                && (detail.Style.Equals(set.PrimaryAppearance, StringComparison.OrdinalIgnoreCase)
                    || detail.FileName.Contains($"_{set.PrimaryAppearance}_", StringComparison.OrdinalIgnoreCase));
        if (detail.FileName.Contains("Mosaic_Wall", StringComparison.OrdinalIgnoreCase)
            && detail.FileName.Contains("Broken_Piece", StringComparison.OrdinalIgnoreCase))
            return set.WallSystem.Contains("Mosaic", StringComparison.OrdinalIgnoreCase)
                && (detail.Style.Equals(set.PrimaryAppearance, StringComparison.OrdinalIgnoreCase)
                    || detail.FileName.Contains($"_{set.PrimaryAppearance}_", StringComparison.OrdinalIgnoreCase));
        if (detail.FileName.Contains("Wall_Tile_Broken_Piece", StringComparison.OrdinalIgnoreCase))
            return set.WallSystem.Equals("Tile", StringComparison.OrdinalIgnoreCase)
                && (detail.Style.Equals(set.PrimaryAppearance, StringComparison.OrdinalIgnoreCase)
                    || detail.FileName.Contains($"_{set.PrimaryAppearance}_", StringComparison.OrdinalIgnoreCase));
        if (detail.RelativePath.Replace('\\', '/').Contains("/Wall_Brick_C/Bricks/", StringComparison.OrdinalIgnoreCase))
            return set.WallSystem.Equals("Brick", StringComparison.OrdinalIgnoreCase)
                && set.Variant.StartsWith("C", StringComparison.OrdinalIgnoreCase)
                && (detail.Style.Equals(set.PrimaryAppearance, StringComparison.OrdinalIgnoreCase)
                    || detail.FileName.Contains($"_{set.PrimaryAppearance}_", StringComparison.OrdinalIgnoreCase));
        if (detail.FileName.StartsWith("Drow_Wall_Overlay_", StringComparison.OrdinalIgnoreCase)
            && detail.FileName.Contains("_Universal_Endpiece_", StringComparison.OrdinalIgnoreCase))
            return set.Profile.Contains("Drow Wall", StringComparison.OrdinalIgnoreCase);
        if (detail.RelativePath.Replace('\\', '/').Contains("/Metal_Overlay/", StringComparison.OrdinalIgnoreCase))
        {
            var detailVariant = detail.RelativePath.Replace('\\', '/').Split('/')
                .FirstOrDefault(part => part.StartsWith("Drow_Wall_", StringComparison.OrdinalIgnoreCase))?
                .Split('_').LastOrDefault();
            return set.Profile.Contains("Drow Wall", StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(detailVariant)
                    || set.Variant.StartsWith(detailVariant, StringComparison.OrdinalIgnoreCase));
        }
        if (detail.FileName.StartsWith("Wall_Damage_Overlay_", StringComparison.OrdinalIgnoreCase))
            return DetailComponentsApply(detail, set);
        var normalizedPath = detail.RelativePath.Replace('\\', '/');
        if (normalizedPath.Contains("/Structures/Aesthetics/Wall_Decorations/", StringComparison.OrdinalIgnoreCase))
            return DetailComponentsApply(detail, set);
        if (normalizedPath.Contains("/Structures/Aesthetics/Wall_Misc/", StringComparison.OrdinalIgnoreCase))
            return (setTheme.Equals("Mountain", StringComparison.OrdinalIgnoreCase)
                    || set.Profile.Contains("Dwarven", StringComparison.OrdinalIgnoreCase))
                && DetailComponentsApply(detail, set);
        return false;
    }

    private static bool DetailComponentsApply(AssetRecord detail, WallSetDescriptor set)
    {
        var components = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [set.PrimaryComponent] = set.PrimaryAppearance
        };
        if (!string.IsNullOrWhiteSpace(set.SecondaryComponent))
            components[set.SecondaryComponent] = set.SecondaryAppearance;

        var tokens = Path.GetFileNameWithoutExtension(detail.FileName)
            .Split('_', StringSplitOptions.RemoveEmptyEntries);
        var matchedComponent = false;
        for (var index = 0; index < tokens.Length; index++)
        {
            if (!components.TryGetValue(tokens[index], out var setAppearance)) continue;
            matchedComponent = true;
            if (index + 1 < tokens.Length && AppearanceValues.Contains(tokens[index + 1])
                && !tokens[index + 1].Equals(setAppearance, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return matchedComponent;
    }

    private static bool SharedPieceApplies(AssetRecord asset, string sourceId, WallSetDescriptor set, string theme)
    {
        if (!asset.SourceId.Equals(sourceId, StringComparison.OrdinalIgnoreCase)) return false;
        var candidate = Describe(asset);
        if (!candidate.WallSystem.Equals(set.WallSystem, StringComparison.OrdinalIgnoreCase)) return false;
        if (Specified(candidate.PrimaryAppearance)
            && !candidate.PrimaryAppearance.Equals(set.PrimaryAppearance, StringComparison.OrdinalIgnoreCase)) return false;
        if (Specified(candidate.SecondaryAppearance)
            && !candidate.SecondaryAppearance.Equals(set.SecondaryAppearance, StringComparison.OrdinalIgnoreCase)) return false;
        if (Specified(candidate.Profile) && Specified(set.Profile)
            && !candidate.Profile.Equals(set.Profile, StringComparison.OrdinalIgnoreCase)) return false;
        if (Specified(candidate.Variant) && Specified(set.Variant)
            && !VariantsAreCompatible(candidate.Variant, set.Variant)) return false;
        return !Specified(asset.Theme) || !Specified(theme)
            || asset.Theme.Equals(theme, StringComparison.OrdinalIgnoreCase);
    }

    private static bool VariantsAreCompatible(string candidate, string set)
    {
        if (candidate.Equals(set, StringComparison.OrdinalIgnoreCase)) return true;
        var candidateBase = new string(candidate.TakeWhile(char.IsLetter).ToArray());
        var setBase = new string(set.TakeWhile(char.IsLetter).ToArray());
        if (candidateBase.Length == 0 || !candidateBase.Equals(setBase, StringComparison.OrdinalIgnoreCase)) return false;
        var candidateHasNumber = candidate.Skip(candidateBase.Length).Any(char.IsDigit);
        var setHasNumber = set.Skip(setBase.Length).Any(char.IsDigit);
        return !candidateHasNumber || !setHasNumber;
    }

    private static bool IsPathAnchor(AssetRecord asset) => asset.PartType.Equals("Path", StringComparison.OrdinalIgnoreCase)
        || asset.FileName.EndsWith("_Path.png", StringComparison.OrdinalIgnoreCase);
    private static bool Match(string value, string filter) => IsAll(filter)
        || value.Equals(filter, StringComparison.OrdinalIgnoreCase);
    private static bool IsAll(string? value) => string.IsNullOrWhiteSpace(value)
        || value.Equals(AssetQueries.All, StringComparison.OrdinalIgnoreCase);
    private static bool IsSpecified(string value) => !string.IsNullOrWhiteSpace(value)
        && !value.Equals("Unspecified", StringComparison.OrdinalIgnoreCase);
    private static bool Specified(string value) => IsSpecified(value);
    private static string Humanize(string value) => value.Replace('_', ' ');
    private static string MappingIdentity(RibbonMapping mapping) => string.Join('|', mapping.CspTool.Trim(),
        mapping.CspToolGroup.Trim(), mapping.CspRibbonName.Trim());
    private static string Value(WallConstructionSet set, WallSetFacet facet) => facet switch
    {
        WallSetFacet.Set => set.Id,
        WallSetFacet.WallSystem => set.WallSystem,
        WallSetFacet.PrimaryAppearance => set.PrimaryAppearance,
        WallSetFacet.SecondaryAppearance => set.SecondaryAppearance,
        WallSetFacet.Theme => set.Theme,
        WallSetFacet.Profile => set.Profile,
        WallSetFacet.Variant => set.Variant,
        _ => string.Empty
    };

    private sealed record WallSetDescriptor(string WallSystem, string PrimaryComponent, string PrimaryAppearance,
        string SecondaryComponent, string SecondaryAppearance, string Profile, string Variant);
}

public enum WallSetFacet
{
    Set,
    WallSystem,
    PrimaryAppearance,
    SecondaryAppearance,
    Theme,
    Profile,
    Variant
}
