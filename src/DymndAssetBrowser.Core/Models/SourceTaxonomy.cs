using System.Text.RegularExpressions;

namespace DymndAssetBrowser.Core.Models;

public sealed record MaterialTag(string Material, string Finish = "")
{
    public string Key => Finish.Length == 0 ? Material : $"{Material}: {Finish}";
}

public sealed record SourceTaxonomy(string Biome, string Context, string Collection,
    IReadOnlyList<string> Levels, IReadOnlyList<MaterialTag> Materials, IReadOnlyList<string> Tags)
{
    public string Level(int depth) => depth < Levels.Count ? Levels[depth] : "";
    public string DisplayPath => string.Join(" > ", new[] { Context }.Concat(Levels).Where(s => s.Length > 0));
    public string BrowserPath => string.Join(" > ", new[] { Context }.Concat(Levels.Take(3)).Where(s => s.Length > 0));

    private static readonly HashSet<string> MaterialNames = new(StringComparer.OrdinalIgnoreCase)
    { "Wood", "Brick", "Stone", "Adobe", "Metal", "Plaster", "Cloth", "Fabric", "Leather", "Fur", "Glass", "Bone", "Ice", "Snow", "Sand", "Dirt", "Clay", "Paper", "Rope", "Marble", "Tile", "Ceramic", "Porcelain", "Crystal", "Chitin", "Flesh", "Gold", "Silver", "Copper", "Brass", "Bronze", "Iron", "Steel" };
    private static readonly HashSet<string> Finishes = new(StringComparer.OrdinalIgnoreCase)
    { "Ashen", "Earthy", "Light", "Dark", "Walnut", "Rusty", "Slate", "Sandstone", "Redrock", "Volcanic", "Chalk", "Gray", "Grey", "Black", "White", "Red", "Green", "Blue", "Yellow", "Orange", "Purple", "Pink", "Brown", "Beige", "Tan", "Teal", "Gold", "Silver", "Copper", "Brass", "Bronze", "Frosty", "Mossy", "Rotten", "Snowy", "Soot", "Clear", "Polished", "Pale", "Creamy", "Navy", "Olive", "Terracotta", "Moonlit", "Eldritch" };

    public static SourceTaxonomy Parse(AssetRecord asset, bool isFa = true)
    {
        var relative = string.IsNullOrWhiteSpace(asset.RelativePath) ? Path.GetRelativePath(asset.SourceRoot, asset.FilePath) : asset.RelativePath;
        var dirs = relative.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).SkipLast(1).ToArray();
        string biome = "", context = "", collection = "";
        int start = 0;
        if (isFa && dirs.Length > 0)
        {
            if (dirs[0].TrimStart('!').Equals("Core_Settlements", StringComparison.OrdinalIgnoreCase))
            { biome = "Base"; context = "Settlement"; collection = "Base"; start = 1; }
            else
            {
                // An explicit context marker is authoritative. Unknown packs remain discoverable;
                // never infer a race or a settlement from the absence of a Base_ prefix alone.
                var marker = Array.FindIndex(dirs, d => d.TrimStart('!').Equals("Wilderness", StringComparison.OrdinalIgnoreCase)
                    || d.EndsWith("_Settlement", StringComparison.OrdinalIgnoreCase) || d.TrimStart('!').Equals("Effects", StringComparison.OrdinalIgnoreCase));
                if (marker >= 0)
                {
                    biome = marker > 0 ? Humanize(dirs[0]) : "Base";
                    var folder = dirs[marker].TrimStart('!');
                    context = folder.EndsWith("_Settlement", StringComparison.OrdinalIgnoreCase) ? "Settlement" : Humanize(folder);
                    collection = context == "Settlement" ? folder.StartsWith("Base_", StringComparison.OrdinalIgnoreCase) ? "Base" : Humanize(folder[..^11]) : "";
                    start = marker + 1;
                }
            }
        }
        var levels = dirs.Skip(start).Select(Humanize).ToArray();
        var tokens = Path.GetFileNameWithoutExtension(asset.FileName).Split('_', StringSplitOptions.RemoveEmptyEntries);
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in dirs) { var h = Humanize(folder); if (h.Length > 0) tags.Add(h); }
        foreach (var token in tokens)
            if (!Regex.IsMatch(token, @"^(?:[A-Z]\d*|\d+(?:x\d+)?|Path|DIAG)$", RegexOptions.IgnoreCase) && token.Length > 1)
                tags.Add(Humanize(token));
        // Conservative lexical aliases bridge equivalent source folders without moving assets.
        if (tags.Any(t => t.Contains("Dwarf", StringComparison.OrdinalIgnoreCase) || t.Contains("Dwarven", StringComparison.OrdinalIgnoreCase))) tags.Add("Dwarven");
        if (tags.Any(t => t.Equals("Minecarts", StringComparison.OrdinalIgnoreCase) || t.Equals("Mine Carts", StringComparison.OrdinalIgnoreCase))) tags.Add("Mine Carts");
        // With explicit composition, Tile describes shape rather than a second material.
        // Do not guess that a bare Tile asset is stone, or turn ceramic's Sandstone color into stone.
        var explicitComposition = tokens.Contains("Ceramic", StringComparer.OrdinalIgnoreCase)
            || tokens.Contains("Stone", StringComparer.OrdinalIgnoreCase);
        var materialTokens = explicitComposition
            ? tokens.Where(t => !t.Equals("Tile", StringComparison.OrdinalIgnoreCase) && !t.Equals("Tiles", StringComparison.OrdinalIgnoreCase)).ToArray() : tokens;
        var isCeramicTile = tokens.Contains("Ceramic", StringComparer.OrdinalIgnoreCase) && tokens.Any(t => t.Equals("Tile", StringComparison.OrdinalIgnoreCase) || t.Equals("Tiles", StringComparison.OrdinalIgnoreCase));
        var materials = ParseMaterials(materialTokens, isCeramicTile).ToList();
        // In FA's explicit Stone_Floors texture collection, Dirt/Dirt1/etc
        // describes the surface finish, not a replacement construction material.
        // Keep this path-scoped: actual dirt terrain must remain Dirt.
        if (isFa && asset.Group == "Building" && asset.SubGroup == "Floors"
            && dirs.Any(d => d.Equals("Textures", StringComparison.OrdinalIgnoreCase))
            && dirs.Any(d => d.Equals("Stone_Floors", StringComparison.OrdinalIgnoreCase))
            && tokens.Any(t => Regex.IsMatch(t, @"^Dirt\d*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
        {
            materials.RemoveAll(m => m.Material.Equals("Dirt", StringComparison.OrdinalIgnoreCase)
                || m.Material.Equals("Stone", StringComparison.OrdinalIgnoreCase));
            materials.Add(new("Stone", "Dirt"));
            tags.Add("Dirt");
        }
        // Explicit source material folders can fill omissions (e.g. Stone_Floors textures).
        // Do not take material-looking words from arbitrary ancestors or named settlements.
        if (materials.Count == 0)
        {
            var folderMaterial = dirs.Skip(start).Reverse().Select(d => d.Split('_')).FirstOrDefault(p =>
                p.Length == 1 && MaterialNames.Contains(p[0]) || p.Length == 2 && MaterialNames.Contains(p[0]) && p[1].Equals("Floors", StringComparison.OrdinalIgnoreCase));
            if (folderMaterial is not null) materials.Add(new(Canonical(folderMaterial[0], MaterialNames)));
        }
        // A lone explicit material and lone finish can be separated by part/design tokens.
        // This covers Tile_Floor_Edge_C_White without guessing a compound's ownership.
        if (materials.Count == 1 && materials[0].Finish.Length == 0)
        {
            var finishes = tokens.Select(RecognizeFinish).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (finishes.Length == 1 && !finishes[0].Equals(materials[0].Material, StringComparison.OrdinalIgnoreCase))
                materials[0] = materials[0] with { Finish = finishes[0] };
        }
        return new(biome, context, collection, levels, materials, tags.Order(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static string? RecognizeFinish(string token)
    {
        if (Finishes.Contains(token)) return Canonical(token, Finishes);
        if (token.StartsWith("Pattern", StringComparison.OrdinalIgnoreCase)) token = token[7..];
        if (Finishes.Contains(token)) return Canonical(token, Finishes);
        var colors = Regex.Matches(token, @"[A-Z][a-z]*").Select(m => m.Value).ToArray();
        return colors.Length > 1 && string.Concat(colors) == token && colors.All(Finishes.Contains)
            ? string.Join(" / ", colors.Select(c => Canonical(c, Finishes))) : null;
    }

    public static IReadOnlyList<MaterialTag> ParseMaterials(string[] tokens, bool ceramicTile = false)
    {
        var result = new List<MaterialTag>();
        for (int i = 0; i < tokens.Length; i++)
        {
            // Gold in Ceramic_Tile_PatternB_Gold is a finish, not gold construction.
            if (ceramicTile && Finishes.Contains(tokens[i])) continue;
            // Split compact compounds only when every component is known.
            var compact = Regex.Matches(tokens[i], @"[A-Z][a-z]*").Select(m => m.Value).ToArray();
            string[] parts = MaterialNames.Contains(tokens[i]) ? [Canonical(tokens[i], MaterialNames)]
                : compact.Length > 1 && string.Concat(compact) == tokens[i] && compact.All(MaterialNames.Contains) ? compact : [];
            if (parts.Length == 0) continue;
            int next = i + 1;
            var materials = parts.ToList();
            while (next < tokens.Length && MaterialNames.Contains(tokens[next]) && !Finishes.Contains(tokens[next]))
                materials.Add(Canonical(tokens[next++], MaterialNames));
            var finishes = new List<string>();
            while (next < tokens.Length && Finishes.Contains(tokens[next])) finishes.Add(Canonical(tokens[next++], Finishes));
            for (int m = 0; m < materials.Count; m++)
            {
                // Pair ordered finishes only when count agrees; otherwise retain broad material.
                var finish = finishes.Count == materials.Count ? finishes[m] : materials.Count == 1 ? finishes.FirstOrDefault() ?? "" : "";
                result.Add(new MaterialTag(materials[m], finish));
            }
            i = next - 1;
        }
        return result.Distinct().ToArray();
    }

    private static string Canonical(string value, HashSet<string> choices) => choices.First(v => v.Equals(value, StringComparison.OrdinalIgnoreCase));
    public static string Humanize(string value) => value.TrimStart('!').Replace('_', ' ').Trim();
}

public sealed record UserAssetTags
{
    public List<string> Added { get; init; } = [];
    public List<string> Suppressed { get; init; } = [];
    public IEnumerable<string> Effective(IEnumerable<string> automatic) => automatic.Concat(Added)
        .Except(Suppressed, StringComparer.OrdinalIgnoreCase).Distinct(StringComparer.OrdinalIgnoreCase);
    public UserAssetTags Edit(IEnumerable<string> tags, bool remove, IEnumerable<string> sourceTags)
    {
        var values = tags.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var automatic = sourceTags.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new() { Added = remove ? Added.Except(values, StringComparer.OrdinalIgnoreCase).ToList() : Added.Union(values, StringComparer.OrdinalIgnoreCase).ToList(),
            Suppressed = remove
                ? Suppressed.Except(values, StringComparer.OrdinalIgnoreCase).Union(values.Where(automatic.Contains), StringComparer.OrdinalIgnoreCase).ToList()
                : Suppressed.Except(values, StringComparer.OrdinalIgnoreCase).ToList() };
    }
}

public sealed record SourceBrowserFilter
{
    public string SourceId { get; init; } = "All";
    public string[] Biomes { get; init; } = [];
    public string Context { get; init; } = "All";
    public string Collection { get; init; } = "All";
    public string[] Levels { get; init; } = [];
    public string[] Materials { get; init; } = [];
    public bool AllMaterials { get; init; }
    public bool ExcludeAdditionalMaterials { get; init; }
    // Planner sample palettes can exclude additional finishes as well as types.
    // Ordinary inclusive material filters keep their existing behavior.
    public bool ExactMaterialFinishes { get; init; }
    public string[] Tags { get; init; } = [];
    public bool AllTags { get; init; }
    public string Filename { get; init; } = "";
    public bool CaseSensitive { get; init; }
    public string Variant { get; init; } = "All";
    public HashSet<string>? Identities { get; init; }
}

public sealed class SourceBrowserIndex
{
    public sealed record Entry(AssetRecord Asset, SourceTaxonomy Taxonomy, IReadOnlyList<string> Tags);
    public IReadOnlyList<Entry> Entries { get; }
    public SourceBrowserIndex(IEnumerable<Entry> entries) => Entries = entries.ToArray();
    public SourceBrowserIndex(IEnumerable<AssetRecord> assets, IEnumerable<AssetLibrarySource> libraries, IReadOnlyDictionary<string, UserAssetTags> userTags)
    {
        var faIds = libraries.Where(l => l.ParserProfile == LibraryParserProfiles.Fa).Select(l => l.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Entries = assets.Select(a => { var t = SourceTaxonomy.Parse(a, faIds.Contains(a.SourceId));
            return new Entry(a, t, userTags.TryGetValue(a.StableIdentity, out var u) ? u.Effective(t.Tags).ToArray() : t.Tags); }).ToArray();
    }
    public string[] ResolveUpstream(SourceBrowserFilter filter, int selectedDepth)
    {
        // Depth 0 is Context. Only the selected level and its ancestors, library,
        // and biome define ancestry; search/materials must not invent a unique path.
        var selections = new[] { filter.Context }.Concat(filter.Levels).ToArray();
        if (selectedDepth <= 0 || selectedDepth >= selections.Length || Match("", selections[selectedDepth])) return selections;
        var matches = Filter(new SourceBrowserFilter
        {
            SourceId = filter.SourceId, Biomes = filter.Biomes, Context = filter.Context,
            Levels = filter.Levels.Take(selectedDepth).ToArray()
        });
        if (matches.Count == 0) return selections;
        for (int i = 0; i < selectedDepth; i++)
        {
            if (!Match("", selections[i])) continue;
            var values = matches.Select(e => i == 0 ? e.Taxonomy.Context : e.Taxonomy.Level(i - 1))
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
            if (values.Length == 1 && !string.IsNullOrWhiteSpace(values[0])) selections[i] = values[0];
        }
        return selections;
    }

    public List<Entry> Filter(SourceBrowserFilter filter, CancellationToken token = default)
    {
        var search = FilenameSearchQuery.Compile(filter.Filename, filter.CaseSensitive);
        var allowedMaterials = filter.ExcludeAdditionalMaterials && filter.Materials.Length > 0
            ? filter.Materials.Select(k => k.Split(':')[0].Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
        var result = new List<Entry>(); int n = 0;
        foreach (var e in Entries)
        {
            if ((n++ & 255) == 0) token.ThrowIfCancellationRequested();
            if (!Match(e.Asset.SourceId, filter.SourceId) || !Match(e.Taxonomy.Context, filter.Context) || !Match(e.Taxonomy.Collection, filter.Collection)
                || !Match(e.Asset.Variant, filter.Variant)) continue;
            if (filter.Biomes.Length > 0 && !filter.Biomes.Contains(e.Taxonomy.Biome, StringComparer.OrdinalIgnoreCase)) continue;
            if (filter.Levels.Where((value, depth) => !Match(e.Taxonomy.Level(depth), value)).Any()) continue;
            if (filter.Identities is not null && !filter.Identities.Contains(e.Asset.StableIdentity)) continue;
            bool MaterialMatches(string key) => e.Taxonomy.Materials.Any(m => m.Key.Equals(key, StringComparison.OrdinalIgnoreCase) || m.Material.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (filter.Materials.Length > 0 && !(filter.AllMaterials ? filter.Materials.All(MaterialMatches) : filter.Materials.Any(MaterialMatches))) continue;
            if (allowedMaterials is not null && e.Taxonomy.Materials.Any(m => !allowedMaterials.Contains(m.Material))) continue;
            if (filter.ExactMaterialFinishes && filter.Materials.Length > 0 && e.Taxonomy.Materials.Any(m =>
                !filter.Materials.Contains(m.Key, StringComparer.OrdinalIgnoreCase)
                && !filter.Materials.Contains(m.Material, StringComparer.OrdinalIgnoreCase))) continue;
            bool TagMatches(string tag) => e.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);
            if (filter.Tags.Length > 0 && !(filter.AllTags ? filter.Tags.All(TagMatches) : filter.Tags.Any(TagMatches))) continue;
            if (search.Matches(e.Asset.FileName)) result.Add(e);
        }
        return result;
    }
    public static bool Match(string value, string filter) => string.IsNullOrWhiteSpace(filter) || filter == "All" || value.Equals(filter, StringComparison.OrdinalIgnoreCase);
}
