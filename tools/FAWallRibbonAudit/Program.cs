using FAFamilyBrowser.Core.Models;
using FAFamilyBrowser.Core.Parsing;
using FAFamilyBrowser.Core.Persistence;

if (args.Length < 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: FAWallRibbonAudit <asset-index.db>");
    return 2;
}

var indexedAssets = await new AssetIndexStore(args[0]).LoadAllAsync();
var reparse = args.Skip(1).Any(arg => arg.Equals("--reparse", StringComparison.OrdinalIgnoreCase));
var assets = indexedAssets;
if (reparse)
{
    var faFiles = indexedAssets.Where(asset => asset.SourceId.Equals(
        LibrarySourceIds.ForgottenAdventures, StringComparison.OrdinalIgnoreCase)).ToList();
    var styles = FaAssetFilenameParser.DiscoverStyleVocabulary(faFiles.Select(asset => asset.FilePath));
    var parser = new FaAssetFilenameParser();
    var reparsedByIdentity = faFiles
        .Select(asset => parser.Parse(asset.SourceRoot, asset.FilePath, styles) with { SourceId = asset.SourceId })
        .ToDictionary(asset => asset.StableIdentity, StringComparer.OrdinalIgnoreCase);
    assets = indexedAssets.Select(asset => reparsedByIdentity.TryGetValue(asset.StableIdentity, out var parsed)
            ? parsed
            : asset)
        .ToList();

    Console.WriteLine("CURRENT-PARSER COMPARISON");
    Console.WriteLine($"Indexed assets: {indexedAssets.Count:N0}");
    foreach (var field in new (string Name, Func<AssetRecord, string> Read)[]
    {
        ("Group", asset => asset.Group), ("SubGroup", asset => asset.SubGroup),
        ("Material", asset => asset.Material), ("Style", asset => asset.Style),
        ("Theme", asset => asset.Theme), ("Family", asset => asset.Family),
        ("Variant", asset => asset.Variant)
    })
    {
        var changed = indexedAssets.Zip(assets).Count(pair => !field.Read(pair.First).Equals(field.Read(pair.Second), StringComparison.OrdinalIgnoreCase));
        Console.WriteLine($"{field.Name,-9} changed: {changed,7:N0}");
    }
    Console.WriteLine($"Wall Family=Unspecified: indexed {indexedAssets.Count(asset => asset.Group == "Building" && asset.SubGroup == "Walls" && asset.Family == "Unspecified"):N0}; current parser {assets.Count(asset => asset.Group == "Building" && asset.SubGroup == "Walls" && asset.Family == "Unspecified"):N0}");
    Console.WriteLine();
}

if (args.Skip(1).Any(arg => arg.Equals("--taxonomy", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine("TAXONOMY COVERAGE");
    Console.WriteLine("Count | Group > SubGroup | Material? | Style? | Theme? | Family? | Variant?");
    foreach (var group in assets.GroupBy(asset => (asset.Group, asset.SubGroup))
                 .OrderBy(group => group.Key.Group).ThenBy(group => group.Key.SubGroup))
    {
        var members = group.ToList();
        Console.WriteLine($"{members.Count,7:N0} | {group.Key.Group} > {group.Key.SubGroup} | "
                          + $"{Specified(members, asset => asset.Material),6} | {Specified(members, asset => asset.Style),6} | "
                          + $"{Specified(members, asset => asset.Theme),6} | {Specified(members, asset => asset.Family),6} | "
                          + $"{Specified(members, asset => asset.Variant),6}");
    }
    Console.WriteLine();
}
var walls = assets.Where(asset =>
        asset.SourceId.Equals(LibrarySourceIds.ForgottenAdventures, StringComparison.OrdinalIgnoreCase)
        && asset.Group.Equals("Building", StringComparison.OrdinalIgnoreCase)
        && asset.SubGroup.Equals("Walls", StringComparison.OrdinalIgnoreCase))
    .ToList();

var groups = walls.GroupBy(asset => asset.FamilyKey, StringComparer.OrdinalIgnoreCase).ToList();
var resolved = new List<(string Key, int Count, RibbonMapping Mapping)>();
var unresolved = new List<(string Key, int Count, string Reason, string Samples)>();

foreach (var group in groups)
{
    var members = group.ToList();
    var mapping = Resolve(members, group.Key);
    if (mapping is not null)
    {
        resolved.Add((group.Key, members.Count, mapping));
        continue;
    }

    var perAsset = members.Select(asset => Resolve([asset], group.Key)?.CspRibbonName ?? "<none>")
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    var reason = perAsset.Count > 1
        ? $"mixed: {string.Join(", ", perAsset.Take(5))}"
        : "unrecognized";
    unresolved.Add((group.Key, members.Count, reason,
        string.Join("; ", members.Select(asset => asset.FileName).Take(3))));
}

Console.WriteLine($"FA Building > Walls assets: {walls.Count:N0}");
Console.WriteLine($"Planner family keys: {groups.Count:N0}");
Console.WriteLine($"Resolved family keys: {resolved.Count:N0} ({Percent(resolved.Count, groups.Count)})");
Console.WriteLine($"Resolved assets: {resolved.Sum(item => item.Count):N0} ({Percent(resolved.Sum(item => item.Count), walls.Count)})");
Console.WriteLine($"Unresolved family keys: {unresolved.Count:N0}");
Console.WriteLine();
var perAssetResults = walls.Select(asset => (Asset: asset, Mapping: Resolve([asset], asset.FamilyKey))).ToList();
Console.WriteLine($"Individually recognized assets: {perAssetResults.Count(item => item.Mapping is not null):N0} "
                  + $"({Percent(perAssetResults.Count(item => item.Mapping is not null), walls.Count)})");
var pathResults = perAssetResults.Where(item => item.Asset.PartType.Equals("Path", StringComparison.OrdinalIgnoreCase)
        || item.Asset.FileName.EndsWith("_Path.png", StringComparison.OrdinalIgnoreCase)).ToList();
Console.WriteLine($"Wall path assets: {pathResults.Count:N0}; associated: {pathResults.Count(item => item.Mapping is not null):N0} "
                  + $"({Percent(pathResults.Count(item => item.Mapping is not null), pathResults.Count)})");
Console.WriteLine($"Distinct associated ribbons: {pathResults.Where(item => item.Mapping is not null).Select(item => item.Mapping!.CspRibbonName).Distinct(StringComparer.OrdinalIgnoreCase).Count():N0}");

if (args.Skip(1).Any(arg => arg.Equals("--coverage", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine();
    Console.WriteLine("WALL ASSOCIATION COVERAGE BY MATERIAL");
    foreach (var group in walls.GroupBy(asset => asset.Material).OrderByDescending(group => group.Count()))
    {
        var materialPaths = pathResults.Where(item => item.Asset.Material.Equals(group.Key, StringComparison.OrdinalIgnoreCase)).ToList();
        Console.WriteLine($"{group.Key,-20} assets {group.Count(),6:N0} | paths {materialPaths.Count,4:N0} | associated {materialPaths.Count(item => item.Mapping is not null),4:N0} ({Percent(materialPaths.Count(item => item.Mapping is not null), materialPaths.Count)})");
    }
    Console.WriteLine();
    Console.WriteLine("WALL ASSOCIATION COVERAGE BY THEME");
    foreach (var group in walls.GroupBy(asset => asset.Theme).OrderByDescending(group => group.Count()))
    {
        var themePaths = pathResults.Where(item => item.Asset.Theme.Equals(group.Key, StringComparison.OrdinalIgnoreCase)).ToList();
        Console.WriteLine($"{group.Key,-20} assets {group.Count(),6:N0} | paths {themePaths.Count,4:N0} | associated {themePaths.Count(item => item.Mapping is not null),4:N0} ({Percent(themePaths.Count(item => item.Mapping is not null), themePaths.Count)})");
    }
}

var brushRootIndex = Array.FindIndex(args, arg => arg.Equals("--brush-root", StringComparison.OrdinalIgnoreCase));
if (brushRootIndex >= 0 && brushRootIndex + 1 < args.Length && Directory.Exists(args[brushRootIndex + 1]))
{
    var brushNames = Directory.EnumerateFiles(args[brushRootIndex + 1], "*.sut", SearchOption.AllDirectories)
        .Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var associated = pathResults.Where(item => item.Mapping is not null).Select(item => item.Mapping!.CspRibbonName)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    var missing = associated.Where(name => !brushNames.Contains(name)).Order(StringComparer.OrdinalIgnoreCase).ToList();
    Console.WriteLine($"Associated ribbon names present in brush package: {associated.Count - missing.Count:N0}/{associated.Count:N0}");
    if (missing.Count > 0) Console.WriteLine("Associated names missing from package: " + string.Join(", ", missing));
}

if (args.Skip(1).Any(arg => arg.Equals("--semantic", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine();
    Console.WriteLine("SEMANTIC RETRIEVAL TAXONOMY");
    var semanticAssets = assets.Select(asset => (Asset: asset, Taxonomy: AssetSemanticClassifier.Classify(asset))).ToList();
    foreach (var category in semanticAssets.GroupBy(item => item.Taxonomy.Category)
                 .OrderByDescending(group => group.Count()).ThenBy(group => group.Key))
        Console.WriteLine($"{category.Count(),7:N0} | {category.Key}");

    var wallCatalog = WallConstructionCatalog.Build(assets);
    var resolvedSets = wallCatalog.Sets.Count(set => set.AutomaticRibbon.Status == WallRibbonResolutionStatus.Resolved);
    var ambiguousSets = wallCatalog.Sets.Count(set => set.AutomaticRibbon.Status == WallRibbonResolutionStatus.Ambiguous);
    var detailMemberships = wallCatalog.Sets.Sum(set => set.Details.Count);
    var uniqueDetails = wallCatalog.Sets.SelectMany(set => set.Details).Select(asset => asset.StableIdentity)
        .Distinct(StringComparer.OrdinalIgnoreCase).Count();
    Console.WriteLine();
    Console.WriteLine($"Wall construction sets: {wallCatalog.Sets.Count:N0}");
    Console.WriteLine($"Resolved set ribbons: {resolvedSets:N0} ({Percent(resolvedSets, wallCatalog.Sets.Count)})");
    Console.WriteLine($"Ambiguous set ribbons: {ambiguousSets:N0}");
    Console.WriteLine($"Detail memberships: {detailMemberships:N0} across {uniqueDetails:N0} unique detail assets");
    foreach (var system in wallCatalog.Sets.GroupBy(set => set.WallSystem)
                 .OrderByDescending(group => group.Count()).ThenBy(group => group.Key))
        Console.WriteLine($"{system.Count(),5:N0} sets | {system.Sum(set => set.WallPieces.Count),6:N0} pieces | {system.Sum(set => set.Details.Count),6:N0} detail memberships | {system.Key}");

    Console.WriteLine();
    Console.WriteLine("ADOBE DETAILING CHECK");
    foreach (var set in wallCatalog.Sets.Where(set => set.WallSystem.Equals("Adobe", StringComparison.OrdinalIgnoreCase))
                 .OrderBy(set => set.DisplayName).ThenBy(set => set.Variant).Take(100))
        Console.WriteLine($"{set.WallPieces.Count,4:N0} pieces | {set.Details.Count,3:N0} details | {set.AutomaticRibbon.Status,-9} | {set.DisplayName} / {set.Variant}");

    if (args.Skip(1).Any(arg => arg.Equals("--set-details", StringComparison.OrdinalIgnoreCase)))
    {
        Console.WriteLine();
        Console.WriteLine("UNRESOLVED CONSTRUCTION SETS");
        foreach (var set in wallCatalog.Sets.Where(set => set.AutomaticRibbon.Status != WallRibbonResolutionStatus.Resolved)
                     .OrderBy(set => set.WallSystem).ThenBy(set => set.DisplayName).ThenBy(set => set.Variant))
        {
            var anchors = set.WallPieces.Where(asset => asset.PartType.Equals("Path", StringComparison.OrdinalIgnoreCase)
                    || asset.FileName.EndsWith("_Path.png", StringComparison.OrdinalIgnoreCase))
                .Select(asset => asset.FileName).Distinct(StringComparer.OrdinalIgnoreCase);
            Console.WriteLine($"{set.AutomaticRibbon.Status,-9} | {set.DisplayName} / {set.Variant} | {string.Join(", ", anchors)}");
        }
    }
}
if (args.Skip(1).Any(arg => arg.Equals("--unrecognized", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine();
    Console.WriteLine("UNRECOGNIZED ASSET DIRECTORIES");
    foreach (var directory in perAssetResults.Where(item => item.Mapping is null)
                 .GroupBy(item => Path.GetDirectoryName(item.Asset.RelativePath) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                 .Select(group => new { Directory = group.Key, Count = group.Count(), Sample = group.First().Asset.FileName })
                 .OrderByDescending(item => item.Count).ThenBy(item => item.Directory).Take(200))
        Console.WriteLine($"{directory.Count,5} | {directory.Directory} | {directory.Sample}");
}

if (args.Skip(1).Any(arg => arg.Equals("--path-details", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine();
    Console.WriteLine("UNRESOLVED WALL PATHS");
    foreach (var item in pathResults.Where(item => item.Mapping is null)
                 .OrderBy(item => item.Asset.RelativePath, StringComparer.OrdinalIgnoreCase))
        Console.WriteLine($"{item.Asset.Material,-18} | {item.Asset.Theme,-12} | {item.Asset.RelativePath}");
}

if (args.Skip(1).Any(arg => arg.Equals("--details", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine();
    Console.WriteLine("UNRESOLVED PLANNER KEYS");
    foreach (var item in unresolved.OrderByDescending(item => item.Count).ThenBy(item => item.Key))
        Console.WriteLine($"{item.Count,5} | {item.Reason} | {item.Key} | {item.Samples}");
}

if (args.Skip(1).Any(arg => arg.Equals("--resolved-details", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine();
    Console.WriteLine("RESOLVED PLANNER KEYS");
    foreach (var item in resolved.OrderBy(item => item.Key))
        Console.WriteLine($"{item.Count,5} | {item.Key} | {item.Mapping.CspToolGroup} > {item.Mapping.CspRibbonName}");
}

return 0;

static RibbonMapping? Resolve(IReadOnlyList<AssetRecord> walls, string key) =>
    FaWallRibbonCatalog.TryResolve(walls, key);

static string Percent(int numerator, int denominator) => denominator == 0
    ? "0.0%"
    : $"{100d * numerator / denominator:0.0}%";

static string Specified(IReadOnlyCollection<AssetRecord> assets, Func<AssetRecord, string> read) =>
    Percent(assets.Count(asset => !read(asset).Equals("Unspecified", StringComparison.OrdinalIgnoreCase)), assets.Count);
