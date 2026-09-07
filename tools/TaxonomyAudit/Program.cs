using System.Text;
using System.Text.Json;
using System.Diagnostics;
using FAFamilyBrowser.Core.Indexing;
using FAFamilyBrowser.Core.Models;
using FAFamilyBrowser.Core.Parsing;
using FAFamilyBrowser.Core.Persistence;

if (args.Length == 1 && args[0].Equals("--verify-default-state", StringComparison.OrdinalIgnoreCase))
{
    return await VerifyDefaultStateAsync();
}

if (args.Length == 2 && args[0].Equals("--reindex-state-root", StringComparison.OrdinalIgnoreCase))
{
    return await ReindexPersistedStateAsync(Path.GetFullPath(args[1]));
}

if (args.Length == 2 && args[0].Equals("--stone-wall-state-audit", StringComparison.OrdinalIgnoreCase))
{
    return await AuditStoneWallsAsync(Path.GetFullPath(args[1]));
}

if (args.Length == 2 && args[0].Equals("--semantic-summary", StringComparison.OrdinalIgnoreCase))
{
    return await AuditSemanticSummaryAsync(Path.GetFullPath(args[1]));
}

if (args.Length == 4 && args[0].Equals("--semantic-groups", StringComparison.OrdinalIgnoreCase))
{
    return await AuditSemanticGroupsAsync(Path.GetFullPath(args[1]), args[2], args[3]);
}

if (args.Length == 3 && args[0].Equals("--semantic-groups", StringComparison.OrdinalIgnoreCase))
{
    return await AuditSemanticGroupsAsync(Path.GetFullPath(args[1]), args[2], string.Empty);
}

if (args.Length == 2 && args[0].Equals("--catch-all-audit", StringComparison.OrdinalIgnoreCase))
{
    return await AuditCatchAllsAsync(Path.GetFullPath(args[1]));
}

if (args.Length == 2 && args[0].Equals("--advanced-facet-audit", StringComparison.OrdinalIgnoreCase))
{
    return await AuditAdvancedFacetsAsync(Path.GetFullPath(args[1]));
}

if (args.Length == 3 && args[0].Equals("--path-token-audit", StringComparison.OrdinalIgnoreCase))
{
    return await AuditPathTokenAsync(Path.GetFullPath(args[1]), args[2]);
}

if (args.Length == 3 && args[0].Equals("--performance-baseline", StringComparison.OrdinalIgnoreCase))
{
    return await MeasurePerformanceAsync(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
}

if (args.Length < 2 || !int.TryParse(args[0], out var seed))
{
    Console.Error.WriteLine("Usage: TaxonomyAudit <seed> <output-directory> [library-root]");
    Console.Error.WriteLine("   or: TaxonomyAudit --verify-default-state");
    Console.Error.WriteLine("   or: TaxonomyAudit --reindex-state-root <app-data-root>");
    Console.Error.WriteLine("   or: TaxonomyAudit --semantic-summary <library-root>");
    return 2;
}

var outputDirectory = Path.GetFullPath(args[1]);
var configuredRoot = args.Length > 2 ? args[2] : Environment.GetEnvironmentVariable("DYMND_FA_LIBRARY_ROOT");
if (string.IsNullOrWhiteSpace(configuredRoot))
{
    Console.Error.WriteLine("Supply a library root as the third argument or set DYMND_FA_LIBRARY_ROOT.");
    return 2;
}
var root = Path.GetFullPath(configuredRoot);
var source = new AssetLibrarySource
{
    Id = LibrarySourceIds.ForgottenAdventures,
    Name = "Forgotten Adventures",
    RootPath = root,
    ParserProfile = LibraryParserProfiles.Fa
};
var assets = await new AssetLibraryIndexer().ScanLibraryAsync(source);
if (assets.Count < 200) throw new InvalidOperationException($"Only {assets.Count} supported assets were found.");

assets = assets.OrderBy(asset => asset.StableIdentity, StringComparer.OrdinalIgnoreCase).ToList();
var random = new Random(seed);
var indexes = Enumerable.Range(0, assets.Count).ToArray();
for (var i = indexes.Length - 1; i > 0; i--)
{
    var swap = random.Next(i + 1);
    (indexes[i], indexes[swap]) = (indexes[swap], indexes[i]);
}
var sample = indexes.Take(200).Select(index => assets[index]).ToList();
var candidates = sample.Where(asset => asset.Group == "Unspecified" || asset.SubGroup == "Unspecified"
    || LooksLikeVariant(asset.Family) || LooksLikeObjectMaterial(asset.Material)).ToList();

Directory.CreateDirectory(outputDirectory);
var basename = $"cycle-{seed}";
var jsonPath = Path.Combine(outputDirectory, basename + ".json");
var markdownPath = Path.Combine(outputDirectory, basename + ".md");
var payload = new
{
    seed,
    sampleSize = sample.Count,
    libraryCount = assets.Count,
    generatedUtc = DateTime.UtcNow,
    counts = sample.GroupBy(asset => $"{asset.Group} > {asset.SubGroup}").OrderBy(group => group.Key)
        .ToDictionary(group => group.Key, group => group.Count()),
    reviewCandidates = candidates.Select(ToAuditRow),
    sample = sample.Select(ToAuditRow)
};
await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

var markdown = new StringBuilder();
markdown.AppendLine($"# Taxonomy audit cycle {seed}").AppendLine().AppendLine($"- Seed: `{seed}`")
    .AppendLine("- Sample size: `200`").AppendLine($"- Indexed library size: `{assets.Count:N0}`")
    .AppendLine($"- Automated review candidates: `{candidates.Count}`").AppendLine()
    .AppendLine("## Classification counts").AppendLine();
foreach (var group in sample.GroupBy(asset => $"{asset.Group} > {asset.SubGroup}").OrderBy(group => group.Key)) markdown.AppendLine($"- {group.Key}: {group.Count()}");
markdown.AppendLine().AppendLine("## Review candidates").AppendLine();
if (candidates.Count == 0) markdown.AppendLine("No automatic candidates. Inspect the complete JSON sample for semantic mismatches.");
foreach (var asset in candidates) markdown.AppendLine($"- `{asset.RelativePath}` → {asset.Group} / {asset.SubGroup} / {asset.Family}; {asset.Material} / {asset.Style} / {asset.Theme} / {asset.Variant}");
markdown.AppendLine().AppendLine("## Review outcome").AppendLine().AppendLine("Pending semantic review.")
    .AppendLine().AppendLine("## Validation").AppendLine().AppendLine("Pending parser changes, smoke tests, and release build.");
await File.WriteAllTextAsync(markdownPath, markdown.ToString(), new UTF8Encoding(false));
Console.WriteLine($"Wrote exact 200-item audit to {jsonPath} and {markdownPath}.");
return 0;

static object ToAuditRow(AssetRecord asset) => new
{
    asset.RelativePath, asset.FileName, asset.RawTokens, asset.Group, asset.SubGroup, asset.Family,
    asset.Material, asset.Style, asset.Theme, asset.Variant, asset.PartType
};
static bool LooksLikeVariant(string value) => System.Text.RegularExpressions.Regex.IsMatch(value, "^[A-Z](?:\\d+)?$");
static bool LooksLikeObjectMaterial(string value) => new[] { "Boat", "Canoe", "Cart", "Chair", "Ship", "Wheelbarrow" }.Contains(value, StringComparer.OrdinalIgnoreCase);

static async Task<int> VerifyDefaultStateAsync()
{
    var store = new ApplicationStateStore();
    var state = await store.LoadAsync();
    Console.WriteLine($"State root: {store.AppDataRoot}");
    Console.WriteLine($"Libraries: {state.Libraries.Count:N0}");
    Console.WriteLine($"Assets: {state.Assets.Count:N0}");
    Console.WriteLine($"Ribbon mappings: {state.RibbonMappings.Count:N0}");
    Console.WriteLine($"Build recipe: {(state.BuildRecipe is null ? "missing" : "present")}");
    return state.Libraries.Count > 0 && state.Assets.Count > 0 ? 0 : 4;
}

static async Task<int> ReindexPersistedStateAsync(string appDataRoot)
{
    var store = new ApplicationStateStore(appDataRoot);
    var state = await store.LoadAsync();
    if (state.Libraries.Count == 0)
    {
        Console.Error.WriteLine($"No registered libraries were found in {store.StatePath}.");
        return 3;
    }

    var indexed = new List<AssetRecord>();
    var indexer = new AssetLibraryIndexer();
    foreach (var library in state.Libraries)
    {
        Console.WriteLine($"Indexing {library.Name}: {library.RootPath}");
        var parsed = await indexer.ScanLibraryAsync(library);
        indexed.AddRange(parsed);
        Console.WriteLine($"  {parsed.Count:N0} assets indexed.");
    }

    var updated = state with
    {
        Assets = indexed
            .OrderBy(asset => asset.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(asset => asset.SubGroup, StringComparer.OrdinalIgnoreCase)
            .ThenBy(asset => asset.Material, StringComparer.OrdinalIgnoreCase)
            .ThenBy(asset => asset.Style, StringComparer.OrdinalIgnoreCase)
            .ThenBy(asset => asset.Theme, StringComparer.OrdinalIgnoreCase)
            .ThenBy(asset => asset.Family, StringComparer.OrdinalIgnoreCase)
            .ThenBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList()
    };
    await store.SaveAsync(updated);
    Console.WriteLine($"Persisted {updated.Assets.Count:N0} parser-derived assets and {updated.RibbonMappings.Count:N0} ribbon mappings.");
    return 0;
}

static async Task<int> AuditStoneWallsAsync(string appDataRoot)
{
    var state = await new ApplicationStateStore(appDataRoot).LoadAsync();
    var walls = state.Assets
        .Where(asset => asset.Group.Equals("Building", StringComparison.OrdinalIgnoreCase)
            && asset.SubGroup.Equals("Walls", StringComparison.OrdinalIgnoreCase)
            && (asset.Material.Contains("Stone", StringComparison.OrdinalIgnoreCase)
                || asset.FileName.Contains("Wall_Stone_", StringComparison.OrdinalIgnoreCase)))
        .ToList();
    Console.WriteLine($"Stone wall candidates: {walls.Count:N0}");
    foreach (var group in walls.GroupBy(asset => new { asset.Material, asset.Style, asset.Theme, asset.Family, asset.Variant })
                 .OrderBy(group => group.Key.Material).ThenBy(group => group.Key.Style).ThenBy(group => group.Key.Theme)
                 .ThenBy(group => group.Key.Family).ThenBy(group => group.Key.Variant))
    {
        Console.WriteLine($"{group.Count(),5} | {group.Key.Material} | {group.Key.Style} | {group.Key.Theme} | {group.Key.Family} | {group.Key.Variant}");
    }
    return 0;
}

static async Task<int> AuditSemanticSummaryAsync(string root)
{
    var source = new AssetLibrarySource
    {
        Id = LibrarySourceIds.ForgottenAdventures,
        Name = "Forgotten Adventures",
        RootPath = root,
        ParserProfile = LibraryParserProfiles.Fa
    };
    var assets = await new AssetLibraryIndexer().ScanLibraryAsync(source);
    var classified = assets.Select(asset => (Asset: asset, Semantic: AssetSemanticClassifier.Classify(asset))).ToList();
    Console.WriteLine($"Semantic catalog: {assets.Count:N0} assets");
    foreach (var group in classified.GroupBy(item => new { item.Semantic.Category, item.Semantic.Type, item.Semantic.Subtype })
                 .OrderBy(group => group.Key.Category, StringComparer.OrdinalIgnoreCase)
                 .ThenBy(group => group.Key.Type, StringComparer.OrdinalIgnoreCase)
                 .ThenBy(group => group.Key.Subtype, StringComparer.OrdinalIgnoreCase))
        Console.WriteLine($"{group.Count(),7:N0} | {group.Key.Category} | {group.Key.Type} | {group.Key.Subtype}");

    static bool IsTexture(AssetRecord asset) => asset.RelativePath.Replace('\\', '/')
        .Contains("/Textures/", StringComparison.OrdinalIgnoreCase);
    var invalidFloorSurfaces = classified.Where(item => item.Semantic is { Category: "Construction", Type: "Floors", Subtype: "Surfaces" }
        && !IsTexture(item.Asset)).ToList();
    var invalidCaveSurfaces = classified.Where(item => item.Semantic is { Category: "Nature", Type: "Cave & Underdark", Subtype: "Cave Surfaces" }
        && !IsTexture(item.Asset)).ToList();
    Console.WriteLine($"Invalid floor surfaces outside texture folders: {invalidFloorSurfaces.Count:N0}");
    Console.WriteLine($"Invalid cave surfaces outside texture folders: {invalidCaveSurfaces.Count:N0}");
    return invalidFloorSurfaces.Count == 0 && invalidCaveSurfaces.Count == 0 ? 0 : 5;
}

static async Task<int> AuditSemanticGroupsAsync(string root, string type, string subtype)
{
    var source = new AssetLibrarySource
    {
        Id = LibrarySourceIds.ForgottenAdventures,
        Name = "Forgotten Adventures",
        RootPath = root,
        ParserProfile = LibraryParserProfiles.Fa
    };
    var assets = await new AssetLibraryIndexer().ScanLibraryAsync(source);
    var matches = assets.Where(asset => AssetSemanticClassifier.Classify(asset) is var semantic
        && semantic.Type.Equals(type, StringComparison.OrdinalIgnoreCase)
        && semantic.Subtype.Equals(subtype, StringComparison.OrdinalIgnoreCase)).ToList();
    Console.WriteLine($"{type} > {subtype}: {matches.Count:N0} assets");
    foreach (var group in matches.GroupBy(asset => new
             {
                 asset.Group,
                 asset.SubGroup,
                 Directory = Path.GetDirectoryName(asset.RelativePath) ?? string.Empty
             })
             .OrderByDescending(group => group.Count())
             .ThenBy(group => group.Key.Directory, StringComparer.OrdinalIgnoreCase))
        Console.WriteLine($"{group.Count(),6:N0} | {group.Key.Group} / {group.Key.SubGroup} | {group.Key.Directory}");
    return 0;
}

static async Task<int> AuditCatchAllsAsync(string root)
{
    var source = new AssetLibrarySource
    {
        Id = LibrarySourceIds.ForgottenAdventures,
        Name = "Forgotten Adventures",
        RootPath = root,
        ParserProfile = LibraryParserProfiles.Fa
    };
    var assets = await new AssetLibraryIndexer().ScanLibraryAsync(source);
    var classified = assets.Select(asset => (Asset: asset, Semantic: AssetSemanticClassifier.Classify(asset))).ToList();
    var targets = new (string Type, string Subtype)[]
    {
        ("Water Features", "Other Water Structures"),
        ("Decor & Display", "Other Decor"),
        ("Stairs & Access", "Access Components"),
        ("Ruins & Construction Debris", "Other Debris"),
        ("Furniture", "Miscellaneous Furniture"),
        ("Flora", "General Plants"),
        ("Mechanical Parts", "Miscellaneous Parts"),
        ("Water & Liquids", "Other Water Features"),
        ("Shelters & Prefab Structures", "Other Shelters"),
        ("Tools & Workstations", "General Tools")
    };
    foreach (var target in targets)
    {
        var matches = classified.Where(item => item.Semantic.Type.Equals(target.Type, StringComparison.OrdinalIgnoreCase)
            && item.Semantic.Subtype.Equals(target.Subtype, StringComparison.OrdinalIgnoreCase)).ToList();
        Console.WriteLine();
        Console.WriteLine($"=== {target.Type}{(target.Subtype.Length == 0 ? string.Empty : " > " + target.Subtype)}: {matches.Count:N0} ===");
        foreach (var group in matches.GroupBy(item => Path.GetDirectoryName(item.Asset.RelativePath) ?? string.Empty)
                     .OrderByDescending(group => group.Count())
                     .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                     .Take(35))
            Console.WriteLine($"{group.Count(),6:N0} | {group.Key}");
    }
    return 0;
}

static async Task<int> AuditAdvancedFacetsAsync(string root)
{
    var source = new AssetLibrarySource
    {
        Id = LibrarySourceIds.ForgottenAdventures,
        Name = "Forgotten Adventures",
        RootPath = root,
        ParserProfile = LibraryParserProfiles.Fa
    };
    var assets = await new AssetLibraryIndexer().ScanLibraryAsync(source);
    var classified = assets.Select(asset => (Asset: asset, Semantic: AssetSemanticClassifier.Classify(asset))).ToList();

    Console.WriteLine($"Advanced facet audit: {assets.Count:N0} assets");
    Console.WriteLine("\n=== Family values crossing semantic subtypes ===");
    foreach (var family in classified
                 .Where(item => !item.Asset.Family.Equals("Unspecified", StringComparison.OrdinalIgnoreCase))
                 .GroupBy(item => item.Asset.Family, StringComparer.OrdinalIgnoreCase)
                 .Select(group => new
                 {
                     Family = group.Key,
                     Count = group.Count(),
                     Paths = group.Select(item => $"{item.Semantic.Category} > {item.Semantic.Type} > {item.Semantic.Subtype}")
                         .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray()
                 })
                 .Where(group => group.Paths.Length > 1)
                 .OrderByDescending(group => group.Paths.Length)
                 .ThenByDescending(group => group.Count)
                 .ThenBy(group => group.Family, StringComparer.OrdinalIgnoreCase))
        Console.WriteLine($"{family.Count,6:N0} | {family.Family} | {string.Join(" || ", family.Paths)}");

    Console.WriteLine("\n=== Variant values spanning many semantic paths ===");
    foreach (var variant in classified
                 .Where(item => !item.Asset.Variant.Equals("Unspecified", StringComparison.OrdinalIgnoreCase))
                 .GroupBy(item => item.Asset.Variant, StringComparer.OrdinalIgnoreCase)
                 .Select(group => new
                 {
                     Variant = group.Key,
                     Count = group.Count(),
                     PathCount = group.Select(item => $"{item.Semantic.Category} > {item.Semantic.Type} > {item.Semantic.Subtype}")
                         .Distinct(StringComparer.OrdinalIgnoreCase).Count()
                 })
                 .Where(group => group.PathCount >= 8)
                 .OrderByDescending(group => group.PathCount)
                 .ThenByDescending(group => group.Count))
        Console.WriteLine($"{variant.Count,6:N0} | {variant.Variant} | {variant.PathCount:N0} semantic paths");

    return 0;
}

static async Task<int> AuditPathTokenAsync(string root, string token)
{
    var source = new AssetLibrarySource
    {
        Id = LibrarySourceIds.ForgottenAdventures,
        Name = "Forgotten Adventures",
        RootPath = root,
        ParserProfile = LibraryParserProfiles.Fa
    };
    var assets = await new AssetLibraryIndexer().ScanLibraryAsync(source);
    var matches = assets.Where(asset => asset.RelativePath.Contains(token, StringComparison.OrdinalIgnoreCase)
        || asset.FileName.Contains(token, StringComparison.OrdinalIgnoreCase))
        .Select(asset => (Asset: asset, Semantic: AssetSemanticClassifier.Classify(asset))).ToList();
    Console.WriteLine($"Path/name token '{token}': {matches.Count:N0} assets");
    foreach (var group in matches.GroupBy(item => new { item.Semantic.Category, item.Semantic.Type, item.Semantic.Subtype })
                 .OrderByDescending(group => group.Count())
                 .ThenBy(group => group.Key.Category, StringComparer.OrdinalIgnoreCase)
                 .ThenBy(group => group.Key.Type, StringComparer.OrdinalIgnoreCase)
                 .ThenBy(group => group.Key.Subtype, StringComparer.OrdinalIgnoreCase))
        Console.WriteLine($"{group.Count(),6:N0} | {group.Key.Category} | {group.Key.Type} | {group.Key.Subtype}");
    return 0;
}

static async Task<int> MeasurePerformanceAsync(string appDataRoot, string outputDirectory)
{
    Directory.CreateDirectory(outputDirectory);
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    var loadTimer = Stopwatch.StartNew();
    var store = new ApplicationStateStore(appDataRoot);
    var state = await store.LoadAsync();
    loadTimer.Stop();

    var catalogTimer = Stopwatch.StartNew();
    var catalog = new AssetCatalogIndex(state.Assets);
    catalogTimer.Stop();
    var retainedBytes = GC.GetTotalMemory(forceFullCollection: true);

    var representativeFilter = new AssetFilter(Group: "Building", SubGroup: "Walls", Material: "Stone_Wood");
    const int iterations = 10;
    var filterTimer = Stopwatch.StartNew();
    var matchingCount = 0;
    for (var iteration = 0; iteration < iterations; iteration++)
        matchingCount = catalog.Filter(representativeFilter).Count;
    filterTimer.Stop();

    var facetTimer = Stopwatch.StartNew();
    var facetValueCount = 0;
    for (var iteration = 0; iteration < iterations; iteration++)
        foreach (var facet in Enum.GetValues<AssetFacet>())
            facetValueCount += catalog.ValuesForFacet(representativeFilter, facet).Count;
    facetTimer.Stop();

    var saveRoot = Path.Combine(outputDirectory, "json-save-probe");
    var saveTimer = Stopwatch.StartNew();
    await new ApplicationStateStore(saveRoot).SaveAsync(state);
    saveTimer.Stop();

    var incrementalTimer = Stopwatch.StartNew();
    if (state.Assets.Count > 0) await store.UpsertAssetsAsync([state.Assets[0]]);
    incrementalTimer.Stop();

    var settingsBytes = File.Exists(store.StatePath) ? new FileInfo(store.StatePath).Length : 0;
    var indexBytes = File.Exists(store.IndexPath) ? new FileInfo(store.IndexPath).Length : 0;
    var result = new
    {
        measuredUtc = DateTime.UtcNow,
        assetCount = state.Assets.Count,
        libraryCount = state.Libraries.Count,
        ribbonMappingCount = state.RibbonMappings.Count,
        plannerPopulated = state.BuildRecipe.IsPopulated,
        settingsBytes,
        indexBytes,
        retainedManagedBytes = retainedBytes,
        loadMilliseconds = loadTimer.Elapsed.TotalMilliseconds,
        catalogBuildMilliseconds = catalogTimer.Elapsed.TotalMilliseconds,
        startupExistenceSweepMilliseconds = 0,
        averageFilterMilliseconds = filterTimer.Elapsed.TotalMilliseconds / iterations,
        averageSevenFacetMilliseconds = facetTimer.Elapsed.TotalMilliseconds / iterations,
        saveMilliseconds = saveTimer.Elapsed.TotalMilliseconds,
        incrementalUpsertMilliseconds = incrementalTimer.Elapsed.TotalMilliseconds,
        matchingCount,
        facetValueCount
    };
    var resultPath = Path.Combine(outputDirectory, "baseline.json");
    await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
