using DymndAssetBrowser.Core.Models;

namespace DymndAssetBrowser.App.ViewModels;

public sealed partial class MainViewModel
{
    private SourceBrowserIndex.Entry? ShortcutEntry(AssetTileViewModel tile) =>
        _sourceBrowser.Entries.FirstOrDefault(e => e.Asset.StableIdentity.Equals(tile.Asset.StableIdentity, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<AssetFilterOption> BrowserFilterOptions(AssetTileViewModel tile) =>
        !IsBuildMode && ShortcutEntry(tile) is { } entry ? AssetFilterShortcut.Options(entry) : [];

    public bool ApplyBrowserFilter(AssetTileViewModel tile, IEnumerable<string> keys)
    {
        if (IsBuildMode || ShortcutEntry(tile) is not { } entry ||
            AssetFilterShortcut.Create(entry, keys, SelectedSourceId) is not { } filter) return false;
        _searchDebounceCancellation?.Cancel();
        _updatingSourceFacets = true;
        try
        {
            _biomes = filter.Biomes;
            _searchText = ""; _filenameCaseSensitive = false; _newOnly = false;
            _tagSearch = string.Join(", ", filter.Tags); _allTags = filter.AllTags;
            _allMaterials = filter.AllMaterials; _materialSearch = "";
            SourceFacets[0].Selected = string.IsNullOrEmpty(filter.Context) ? All : filter.Context;
            for (int i = 0; i < 3; i++) SourceFacets[i + 1].Selected = string.IsNullOrEmpty(filter.Levels[i]) ? All : filter.Levels[i];
            CollectionFacet.Selected = filter.Collection; VariantFacet.Selected = filter.Variant;
            foreach (var c in _materialChoices) c.IsSelected = false;
        }
        finally { _updatingSourceFacets = false; }
        RebuildSourceFacets();
        _updatingSourceFacets = true;
        try { foreach (var c in _materialChoices) c.IsSelected = filter.Materials.Contains(c.Key, StringComparer.OrdinalIgnoreCase); }
        finally { _updatingSourceFacets = false; }
        foreach (var name in new[] { nameof(SearchText), nameof(FilenameCaseSensitive), nameof(TagSearch), nameof(MatchAllTags),
            nameof(MatchAllMaterials), nameof(MaterialSearch), nameof(MaterialSummary), nameof(BiomeSummary), nameof(NewAssetsLabel) }) OnPropertyChanged(name);
        Refresh();
        return true;
    }
}
