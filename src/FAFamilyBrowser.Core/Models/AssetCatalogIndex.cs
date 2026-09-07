namespace FAFamilyBrowser.Core.Models;

/// <summary>
/// Immutable in-memory posting index for interactive filtering. Asset records remain the
/// source of truth; this only avoids repeatedly scanning the complete library for every facet.
/// </summary>
public sealed class AssetCatalogIndex
{
    private readonly IReadOnlyList<AssetRecord> _assets;
    private readonly AssetSemanticTaxonomy[] _semantic;
    private readonly Dictionary<string, int[]> _sources;
    private readonly Dictionary<AssetFacet, Dictionary<string, int[]>> _postings;
    private readonly Dictionary<AssetFacet, IReadOnlyList<string>> _allValues;

    public static AssetCatalogIndex Empty { get; } = new([]);

    public AssetCatalogIndex(IReadOnlyList<AssetRecord> assets)
    {
        _assets = assets;
        _semantic = assets.Select(AssetSemanticClassifier.Classify).ToArray();
        _sources = BuildPosting(assets.Count, index => [assets[index].SourceId]);
        _postings = Enum.GetValues<AssetFacet>().ToDictionary(
            facet => facet,
            facet => BuildPosting(assets.Count, index => Values(index, facet)));
        _allValues = _postings.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)new[] { AssetQueries.All }.Concat(pair.Value.Keys
                .Where(ValuePresent)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                .ToList());
    }

    public int Count => _assets.Count;
    public IReadOnlyList<AssetRecord> Assets => _assets;

    public List<AssetRecord> Filter(AssetFilter filter, CancellationToken cancellationToken = default) =>
        FilterCore(filter, ignoredFacet: null, fixedScope: null, cancellationToken);

    public bool Any(AssetFilter filter)
    {
        var filenameSearch = FilenameSearchQuery.Compile(filter.FilenameQuery, filter.FilenameCaseSensitive);
        foreach (var index in CandidateIndices(filter, ignoredFacet: null, fixedScope: null))
            if (Matches(index, filter, ignoredFacet: null, filenameSearch)) return true;
        return false;
    }

    public IReadOnlyList<string> ValuesForFacet(AssetFilter filter, AssetFacet facet,
        AssetFilter? fixedScope = null, CancellationToken cancellationToken = default)
    {
        if (fixedScope is null && string.IsNullOrWhiteSpace(filter.FilenameQuery)
            && HasNoConstraintsExcept(filter, facet))
            return _allValues[facet];

        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filenameSearch = FilenameSearchQuery.Compile(filter.FilenameQuery, filter.FilenameCaseSensitive);
        var fixedFilenameSearch = fixedScope is null ? null : FilenameSearchQuery.Compile(fixedScope.FilenameQuery, fixedScope.FilenameCaseSensitive);
        var checkedCount = 0;
        foreach (var index in CandidateIndices(filter, facet, fixedScope))
        {
            if ((checkedCount++ & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (!Matches(index, filter, facet, filenameSearch)
                || fixedScope is not null && !Matches(index, fixedScope, null, fixedFilenameSearch!)) continue;
            foreach (var value in Values(index, facet, filter))
                if (ValuePresent(value)) values.Add(value);
        }

        return new[] { AssetQueries.All }.Concat(values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    public IReadOnlyList<string> ValuesForFacetWhere(AssetFilter filter, AssetFacet facet,
        Func<AssetRecord, bool> predicate, AssetFilter? fixedScope = null,
        CancellationToken cancellationToken = default)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filenameSearch = FilenameSearchQuery.Compile(filter.FilenameQuery, filter.FilenameCaseSensitive);
        var fixedFilenameSearch = fixedScope is null ? null : FilenameSearchQuery.Compile(fixedScope.FilenameQuery, fixedScope.FilenameCaseSensitive);
        var checkedCount = 0;
        foreach (var index in CandidateIndices(filter, facet, fixedScope))
        {
            if ((checkedCount++ & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (!Matches(index, filter, facet, filenameSearch)
                || fixedScope is not null && !Matches(index, fixedScope, null, fixedFilenameSearch!)
                || !predicate(_assets[index])) continue;
            foreach (var value in Values(index, facet, filter))
                if (ValuePresent(value)) values.Add(value);
        }
        return new[] { AssetQueries.All }.Concat(values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    public IReadOnlyList<string> AllValues(AssetFacet facet) => _allValues[facet];

    private List<AssetRecord> FilterCore(AssetFilter filter, AssetFacet? ignoredFacet, AssetFilter? fixedScope,
        CancellationToken cancellationToken)
    {
        var result = new List<AssetRecord>();
        var filenameSearch = FilenameSearchQuery.Compile(filter.FilenameQuery, filter.FilenameCaseSensitive);
        var fixedFilenameSearch = fixedScope is null ? null : FilenameSearchQuery.Compile(fixedScope.FilenameQuery, fixedScope.FilenameCaseSensitive);
        var checkedCount = 0;
        foreach (var index in CandidateIndices(filter, ignoredFacet, fixedScope))
        {
            if ((checkedCount++ & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (Matches(index, filter, ignoredFacet, filenameSearch)
                && (fixedScope is null || Matches(index, fixedScope, null, fixedFilenameSearch!)))
                result.Add(_assets[index]);
        }
        return result;
    }

    private IEnumerable<int> CandidateIndices(AssetFilter filter, AssetFacet? ignoredFacet, AssetFilter? fixedScope)
    {
        int[]? smallest = null;
        Consider(_sources, filter.SourceId, ref smallest);
        if (fixedScope is not null) Consider(_sources, fixedScope.SourceId, ref smallest);

        foreach (var facet in Enum.GetValues<AssetFacet>())
        {
            if (facet != ignoredFacet) Consider(_postings[facet], FilterValue(filter, facet), ref smallest);
            if (fixedScope is not null) Consider(_postings[facet], FilterValue(fixedScope, facet), ref smallest);
        }

        return smallest ?? Enumerable.Range(0, _assets.Count);
    }

    private static void Consider(Dictionary<string, int[]> posting, string value, ref int[]? smallest)
    {
        if (IsAll(value)) return;
        if (!posting.TryGetValue(value, out var indices))
        {
            smallest = [];
            return;
        }
        if (smallest is null || indices.Length < smallest.Length) smallest = indices;
    }

    private bool Matches(int index, AssetFilter filter, AssetFacet? ignoredFacet, FilenameSearchQuery filenameSearch)
    {
        var asset = _assets[index];
        if (!Match(asset.SourceId, filter.SourceId)) return false;
        if (!MatchesSemantic(_semantic[index], filter, ignoredFacet)) return false;
        foreach (var facet in Enum.GetValues<AssetFacet>())
            if (facet != ignoredFacet && facet is not (AssetFacet.Category or AssetFacet.Type or AssetFacet.Subtype))
            {
                var filterValue = FilterValue(filter, facet);
                if (!IsAll(filterValue) && !Match(Values(index, facet), filterValue)) return false;
            }
        return filenameSearch.Matches(asset.FileName);
    }

    private static bool HasNoConstraintsExcept(AssetFilter filter, AssetFacet ignoredFacet)
    {
        if (!IsAll(filter.SourceId)) return false;
        return Enum.GetValues<AssetFacet>().All(facet => facet == ignoredFacet || IsAll(FilterValue(filter, facet)));
    }

    private static Dictionary<string, int[]> BuildPosting(int count, Func<int, IEnumerable<string>> selector)
    {
        var building = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < count; index++)
        {
            foreach (var value in selector(index).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!building.TryGetValue(value, out var values)) building[value] = values = [];
                values.Add(index);
            }
        }
        return building.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    private static string FilterValue(AssetFilter filter, AssetFacet facet) => facet switch
    {
        AssetFacet.Group => filter.Group,
        AssetFacet.SubGroup => filter.SubGroup,
        AssetFacet.Material => filter.Material,
        AssetFacet.Style => filter.Style,
        AssetFacet.Theme => filter.Theme,
        AssetFacet.Family => filter.Family,
        AssetFacet.Variant => filter.Variant,
        AssetFacet.Category => filter.Category,
        AssetFacet.Type => filter.Type,
        AssetFacet.Subtype => filter.Subtype,
        AssetFacet.Context => filter.Context,
        AssetFacet.MaterialFacet => filter.MaterialFacet,
        AssetFacet.AppearanceFacet => filter.AppearanceFacet,
        AssetFacet.SourceSet => filter.SourceSet,
        AssetFacet.FilenameVariant => filter.FilenameVariant,
        _ => AssetQueries.All
    };

    private IEnumerable<string> Values(int index, AssetFacet facet, AssetFilter? filter = null)
    {
        var asset = _assets[index];
        var semantic = _semantic[index];
        return facet switch
        {
            AssetFacet.Group => [asset.Group],
            AssetFacet.SubGroup => [asset.SubGroup],
            AssetFacet.Material => [asset.Material],
            AssetFacet.Style => [asset.Style],
            AssetFacet.Theme => [asset.Theme],
            AssetFacet.Family => [asset.Family],
            AssetFacet.Variant => [asset.Variant],
            AssetFacet.Category => SemanticValues(semantic, filter, AssetFacet.Category),
            AssetFacet.Type => SemanticValues(semantic, filter, AssetFacet.Type),
            AssetFacet.Subtype => SemanticValues(semantic, filter, AssetFacet.Subtype),
            AssetFacet.Context => semantic.Contexts,
            AssetFacet.MaterialFacet => semantic.Materials,
            AssetFacet.AppearanceFacet => semantic.Appearances,
            AssetFacet.SourceSet => [AssetSourceMetadata.SourceSet(asset)],
            AssetFacet.FilenameVariant => [AssetSourceMetadata.FilenameVariant(asset)],
            _ => []
        };
    }

    private static bool Match(IEnumerable<string> values, string filter) => IsAll(filter)
        || values.Any(value => value.Equals(filter, StringComparison.OrdinalIgnoreCase));
    private static bool MatchesSemantic(AssetSemanticTaxonomy semantic, AssetFilter filter, AssetFacet? ignoredFacet) =>
        semantic.Paths.Any(path =>
            (ignoredFacet == AssetFacet.Category || Match(path.Category, filter.Category))
            && (ignoredFacet == AssetFacet.Type || Match(path.Type, filter.Type))
            && (ignoredFacet == AssetFacet.Subtype || Match(path.Subtype, filter.Subtype)));
    private static IEnumerable<string> SemanticValues(AssetSemanticTaxonomy semantic, AssetFilter? filter, AssetFacet facet)
    {
        var paths = filter is null
            ? semantic.Paths
            : semantic.Paths.Where(path =>
                (facet == AssetFacet.Category || Match(path.Category, filter.Category))
                && (facet == AssetFacet.Type || Match(path.Type, filter.Type))
                && (facet == AssetFacet.Subtype || Match(path.Subtype, filter.Subtype)));
        return paths.Select(path => facet switch
        {
            AssetFacet.Category => path.Category,
            AssetFacet.Type => path.Type,
            AssetFacet.Subtype => path.Subtype,
            _ => string.Empty
        });
    }
    private static bool Match(string value, string filter) => IsAll(filter)
        || value.Equals(filter, StringComparison.OrdinalIgnoreCase);
    private static bool IsAll(string? value) => string.IsNullOrWhiteSpace(value)
        || value.Equals(AssetQueries.All, StringComparison.OrdinalIgnoreCase);
    private static bool ValuePresent(string value) => !string.IsNullOrWhiteSpace(value);
}
