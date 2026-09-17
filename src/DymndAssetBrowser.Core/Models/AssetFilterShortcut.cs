namespace DymndAssetBrowser.Core.Models;

public sealed record AssetFilterOption(string Key, string Label, string Value, bool IsTag = false);

// Construct explicit filters from indexed metadata, never guessed display labels.
public static class AssetFilterShortcut
{
    public static IReadOnlyList<AssetFilterOption> Options(SourceBrowserIndex.Entry entry)
    {
        var t = entry.Taxonomy;
        var result = new List<AssetFilterOption>();
        void Add(string key, string label, string value)
        { if (!string.IsNullOrWhiteSpace(value) && value != "Unspecified") result.Add(new(key, label, value)); }
        Add("Biome", "Biome", t.Biome);
        Add("Context", "Context", t.Context);
        var labels = new[] { "Category", "Subcategory", "Type" };
        for (int i = 0; i < labels.Length; i++) Add("Level:" + i, labels[i], t.Level(i));
        Add("Collection", "Settlement collection", t.Collection);
        foreach (var m in t.Materials)
        {
            Add("Material:" + m.Material, "Material", m.Material);
            if (m.Finish.Length > 0) Add("Material:" + m.Key, "Material + finish", m.Key);
        }
        Add("Variant", "Variant", entry.Asset.Variant);
        foreach (var tag in entry.Tags.Where(v => !string.IsNullOrWhiteSpace(v) && !v.Contains(','))
                     .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
            result.Add(new("Tag:" + tag, "Tag", tag, true));
        return result.DistinctBy(o => o.Key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static SourceBrowserFilter? Create(SourceBrowserIndex.Entry entry, IEnumerable<string> keys, string sourceId)
    {
        var requested = keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var chosen = Options(entry).Where(o => requested.Contains(o.Key)).ToArray();
        if (chosen.Length == 0) return null;
        bool Has(string key) => chosen.Any(o => o.Key == key);
        int depth = Enumerable.Range(0, 3).Where(i => Has("Level:" + i)).DefaultIfEmpty(-1).Max();
        var materials = chosen.Where(o => o.Key.StartsWith("Material:")).Select(o => o.Value).ToArray();
        // A selected finish supersedes its redundant broad-material checkbox.
        materials = materials.Where(m => m.Contains(':') || !materials.Any(f => f.StartsWith(m + ": ", StringComparison.OrdinalIgnoreCase))).ToArray();
        var tags = chosen.Where(o => o.IsTag).Select(o => o.Value).ToArray();
        return new()
        {
            SourceId = sourceId,
            Biomes = Has("Biome") ? [entry.Taxonomy.Biome] : [],
            Context = Has("Context") || depth >= 0 ? entry.Taxonomy.Context : "All",
            Levels = Enumerable.Range(0, 3).Select(i => i <= depth ? entry.Taxonomy.Level(i) : "All").ToArray(),
            Collection = Has("Collection") ? entry.Taxonomy.Collection : "All",
            Variant = Has("Variant") ? entry.Asset.Variant : "All",
            Materials = materials, AllMaterials = materials.Length > 1,
            Tags = tags, AllTags = tags.Length > 1
        };
    }
}
