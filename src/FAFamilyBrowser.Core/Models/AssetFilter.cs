namespace FAFamilyBrowser.Core.Models;

public sealed record AssetFilter(string Group = "All", string SubGroup = "All", string Material = "All",
    string Style = "All", string Theme = "All", string Family = "All", string FilenameQuery = "",
    bool FilenameCaseSensitive = false, string SourceId = "All", string Variant = "All",
    string Category = "All", string Type = "All", string Subtype = "All", string Context = "All",
    string MaterialFacet = "All", string AppearanceFacet = "All", string SourceSet = "All",
    string FilenameVariant = "All");

public sealed class FilenameSearchQuery
{
    private readonly string[] _terms;
    private readonly StringComparison _comparison;

    private FilenameSearchQuery(string[] terms, bool caseSensitive)
    {
        _terms = terms;
        _comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
    }

    public static FilenameSearchQuery Compile(string? query, bool caseSensitive)
    {
        if (string.IsNullOrWhiteSpace(query)) return new([], caseSensitive);

        var terms = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;

        void FinishTerm()
        {
            if (current.Length == 0) return;
            terms.Add(current.ToString());
            current.Clear();
        }

        for (var index = 0; index < query.Length; index++)
        {
            var character = query[index];
            if (character == '"')
            {
                if (quoted && index + 1 < query.Length && query[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                FinishTerm();
                quoted = !quoted;
                continue;
            }

            if (!quoted && char.IsWhiteSpace(character)) FinishTerm();
            else current.Append(character);
        }

        FinishTerm();
        return new(terms.ToArray(), caseSensitive);
    }

    public bool Matches(string filename) => _terms.All(term => filename.Contains(term, _comparison));
}

public enum AssetFacet
{
    Group, SubGroup, Material, Style, Theme, Family, Variant, Category, Type, Subtype, Context,
    MaterialFacet, AppearanceFacet, SourceSet, FilenameVariant
}

public static class AssetQueries
{
    public const string All = "All";

    public static IEnumerable<AssetRecord> Filter(IEnumerable<AssetRecord> assets, AssetFilter filter,
        CancellationToken cancellationToken = default)
    {
        var filenameSearch = FilenameSearchQuery.Compile(filter.FilenameQuery, filter.FilenameCaseSensitive);
        var index = 0;
        foreach (var asset in assets)
        {
            if ((index++ & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
            var semantic = AssetSemanticClassifier.Classify(asset);
            if (Matches(asset.SourceId, filter.SourceId) && Matches(asset.Group, filter.Group) && Matches(asset.SubGroup, filter.SubGroup)
                && Matches(asset.Material, filter.Material) && Matches(asset.Style, filter.Style)
                && Matches(asset.Theme, filter.Theme) && Matches(asset.Family, filter.Family)
                && Matches(asset.Variant, filter.Variant)
                && MatchesSemantic(semantic, filter, null)
                && MatchesAny(semantic.Contexts, filter.Context)
                && MatchesAny(semantic.Materials, filter.MaterialFacet)
                && MatchesAny(semantic.Appearances, filter.AppearanceFacet)
                && Matches(AssetSourceMetadata.SourceSet(asset), filter.SourceSet)
                && Matches(AssetSourceMetadata.FilenameVariant(asset), filter.FilenameVariant)
                && filenameSearch.Matches(asset.FileName))
                yield return asset;
        }
    }

    public static IReadOnlyList<string> SubGroupsFor(IEnumerable<AssetRecord> assets, string group) =>
        new[] { All }.Concat(assets.Where(asset => Matches(asset.Group, group)).Select(asset => asset.SubGroup)
            .Where(ValuePresent).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase)).ToList();

    public static IEnumerable<AssetRecord> ForGroupAndSubGroup(IEnumerable<AssetRecord> assets, string group, string subGroup) =>
        assets.Where(asset => Matches(asset.Group, group) && Matches(asset.SubGroup, subGroup));

    public static IReadOnlyList<string> ValuesForFacet(IEnumerable<AssetRecord> assets, AssetFilter filter, AssetFacet facet)
    {
        var filenameSearch = FilenameSearchQuery.Compile(filter.FilenameQuery, filter.FilenameCaseSensitive);
        var matching = assets.Select(asset => (Asset: asset, Semantic: AssetSemanticClassifier.Classify(asset))).Where(item =>
            Matches(item.Asset.SourceId, filter.SourceId)
            && (facet == AssetFacet.Group || Matches(item.Asset.Group, filter.Group))
            && (facet == AssetFacet.SubGroup || Matches(item.Asset.SubGroup, filter.SubGroup))
            && (facet == AssetFacet.Material || Matches(item.Asset.Material, filter.Material))
            && (facet == AssetFacet.Style || Matches(item.Asset.Style, filter.Style))
            && (facet == AssetFacet.Theme || Matches(item.Asset.Theme, filter.Theme))
            && (facet == AssetFacet.Family || Matches(item.Asset.Family, filter.Family))
            && (facet == AssetFacet.Variant || Matches(item.Asset.Variant, filter.Variant))
            && MatchesSemantic(item.Semantic, filter, facet)
            && (facet == AssetFacet.Context || MatchesAny(item.Semantic.Contexts, filter.Context))
            && (facet == AssetFacet.MaterialFacet || MatchesAny(item.Semantic.Materials, filter.MaterialFacet))
            && (facet == AssetFacet.AppearanceFacet || MatchesAny(item.Semantic.Appearances, filter.AppearanceFacet))
            && (facet == AssetFacet.SourceSet || Matches(AssetSourceMetadata.SourceSet(item.Asset), filter.SourceSet))
            && (facet == AssetFacet.FilenameVariant || Matches(AssetSourceMetadata.FilenameVariant(item.Asset), filter.FilenameVariant))
            && filenameSearch.Matches(item.Asset.FileName));
        var values = facet switch
        {
            AssetFacet.Group => matching.Select(item => item.Asset.Group),
            AssetFacet.SubGroup => matching.Select(item => item.Asset.SubGroup),
            AssetFacet.Material => matching.Select(item => item.Asset.Material),
            AssetFacet.Style => matching.Select(item => item.Asset.Style),
            AssetFacet.Theme => matching.Select(item => item.Asset.Theme),
            AssetFacet.Family => matching.Select(item => item.Asset.Family),
            AssetFacet.Variant => matching.Select(item => item.Asset.Variant),
            AssetFacet.Category => matching.SelectMany(item => SemanticValues(item.Semantic, filter, AssetFacet.Category)),
            AssetFacet.Type => matching.SelectMany(item => SemanticValues(item.Semantic, filter, AssetFacet.Type)),
            AssetFacet.Subtype => matching.SelectMany(item => SemanticValues(item.Semantic, filter, AssetFacet.Subtype)),
            AssetFacet.Context => matching.SelectMany(item => item.Semantic.Contexts),
            AssetFacet.MaterialFacet => matching.SelectMany(item => item.Semantic.Materials),
            AssetFacet.AppearanceFacet => matching.SelectMany(item => item.Semantic.Appearances),
            AssetFacet.SourceSet => matching.Select(item => AssetSourceMetadata.SourceSet(item.Asset)),
            AssetFacet.FilenameVariant => matching.Select(item => AssetSourceMetadata.FilenameVariant(item.Asset)),
            _ => []
        };
        return new[] { All }.Concat(values.Where(ValuePresent).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    public static IEnumerable<AssetRecord> RelatedBuildingPieces(IEnumerable<AssetRecord> assets, AssetFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(filter.Group, "Building", StringComparison.OrdinalIgnoreCase) || !string.Equals(filter.SubGroup, "Walls", StringComparison.OrdinalIgnoreCase)) return [];
        var families = new HashSet<string>(new[] { "Door Frames", "Window Sills", "Arches" }, StringComparer.OrdinalIgnoreCase);
        return FilterRelated(assets, filter, families, cancellationToken);
    }

    private static IEnumerable<AssetRecord> FilterRelated(IEnumerable<AssetRecord> assets, AssetFilter filter,
        HashSet<string> families, CancellationToken cancellationToken)
    {
        var filenameSearch = FilenameSearchQuery.Compile(filter.FilenameQuery, filter.FilenameCaseSensitive);
        var index = 0;
        foreach (var asset in assets)
        {
            if ((index++ & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (Matches(asset.SourceId, filter.SourceId)
                && asset.Group.Equals("Building", StringComparison.OrdinalIgnoreCase)
                && (families.Contains(asset.Family) || asset.FileName.Contains("Curb", StringComparison.OrdinalIgnoreCase)
                    || asset.FileName.Contains("Trim", StringComparison.OrdinalIgnoreCase))
                && Matches(asset.Material, filter.Material) && Matches(asset.Style, filter.Style)
                && Matches(asset.Theme, filter.Theme) && Matches(asset.Variant, filter.Variant)
                && filenameSearch.Matches(asset.FileName))
                yield return asset;
        }
    }

    private static bool Matches(string value, string filter) => filter == All || string.Equals(value, filter, StringComparison.OrdinalIgnoreCase);
    private static bool MatchesSemantic(AssetSemanticTaxonomy semantic, AssetFilter filter, AssetFacet? ignoredFacet) =>
        semantic.Paths.Any(path =>
            (ignoredFacet == AssetFacet.Category || Matches(path.Category, filter.Category))
            && (ignoredFacet == AssetFacet.Type || Matches(path.Type, filter.Type))
            && (ignoredFacet == AssetFacet.Subtype || Matches(path.Subtype, filter.Subtype)));
    private static IEnumerable<string> SemanticValues(AssetSemanticTaxonomy semantic, AssetFilter filter, AssetFacet facet) =>
        semantic.Paths.Where(path =>
                (facet == AssetFacet.Category || Matches(path.Category, filter.Category))
                && (facet == AssetFacet.Type || Matches(path.Type, filter.Type))
                && (facet == AssetFacet.Subtype || Matches(path.Subtype, filter.Subtype)))
            .Select(path => facet switch
            {
                AssetFacet.Category => path.Category,
                AssetFacet.Type => path.Type,
                AssetFacet.Subtype => path.Subtype,
                _ => string.Empty
            });
    private static bool MatchesAny(IEnumerable<string> values, string filter) => filter == All
        || values.Any(value => string.Equals(value, filter, StringComparison.OrdinalIgnoreCase));
    private static bool ValuePresent(string value) => !string.IsNullOrWhiteSpace(value);
}
