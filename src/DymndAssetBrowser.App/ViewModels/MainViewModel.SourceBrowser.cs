using System.Collections.ObjectModel;
using DymndAssetBrowser.App.Infrastructure;
using DymndAssetBrowser.Core.Models;

namespace DymndAssetBrowser.App.ViewModels;

public sealed class SourceFacetChoice(string label, Action changed) : ViewModelBase
{
    private string _selected = "All";
    public string Label { get; } = label;
    public ObservableCollection<string> Options { get; } = ["All"];
    public bool HasOptions => Options.Count > 1;
    public string Selected { get => _selected; set { if (SetProperty(ref _selected, value ?? "All")) changed(); } }
    public bool SetOptions(IEnumerable<string> values)
    {
        var previous = _selected;
        var options = new[] { "All" }.Concat(values.Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)).ToArray();
        // Keep All and surviving items in the collection. Clearing an ItemsSource during
        // a two-way selection update can leave WPF displaying a blank selection.
        foreach (var v in Options.Where(v => !options.Contains(v)).ToArray()) Options.Remove(v);
        for (int i = 0; i < options.Length; i++)
        {
            var oldIndex = Options.IndexOf(options[i]);
            if (oldIndex < 0) Options.Insert(i, options[i]);
            else if (oldIndex != i) Options.Move(oldIndex, i);
        }
        _selected = options.FirstOrDefault(v => v.Equals(previous, StringComparison.OrdinalIgnoreCase)) ?? "All";
        OnPropertyChanged(nameof(Selected)); OnPropertyChanged(nameof(HasOptions));
        return previous != _selected;
    }
}

public sealed class MaterialChoice(string key, Action<MaterialChoice> changed) : ViewModelBase
{
    private bool _selected;
    private bool _hasMatches = true;
    public string Key { get; } = key;
    public string Label => Key.Contains(": ") ? Key[(Key.IndexOf(": ", StringComparison.Ordinal) + 2)..] : "Any finish";
    public bool HasMatches { get => _hasMatches; set { if (SetProperty(ref _hasMatches, value)) OnPropertyChanged(nameof(CanToggle)); } }
    public bool CanToggle => IsSelected || HasMatches;
    public bool IsSelected { get => _selected; set { if (SetProperty(ref _selected, value)) { OnPropertyChanged(nameof(CanToggle)); changed(this); } } }
}
public sealed record MaterialChoiceGroup(string Name, MaterialChoice Parent, IReadOnlyList<MaterialChoice> Choices);

public sealed partial class MainViewModel
{
    private SourceBrowserIndex _sourceBrowser = new([], [], new Dictionary<string, UserAssetTags>());
    private bool _updatingSourceFacets;
    private string[] _biomes = [];
    private List<AssetRecord> _matchingAssets = [];
    private bool _allResultsSelected;
    private string _tagSearch = "", _materialSearch = "";
    private bool _allTags, _allMaterials;
    private HashSet<string>? _newIdentities;
    private bool _newOnly;
    private double _thumbnailSize = 220, _viewportWidth = 1080;
    public int MatchingAssetCount => _matchingAssets.Count;
    private readonly List<MaterialChoice> _materialChoices = [];

    public ObservableCollection<string> BiomeOptions { get; } = [];
    public ObservableCollection<SourceFacetChoice> SourceFacets { get; } = [];
    public IEnumerable<SourceFacetChoice> PrimarySourceFacets => SourceFacets.Take(4);
    public ObservableCollection<MaterialChoiceGroup> MaterialGroups { get; } = [];
    public SourceFacetChoice CollectionFacet { get; private set; } = null!;
    public SourceFacetChoice VariantFacet { get; private set; } = null!;
    public string BiomeSummary => _biomes.Length == 0 ? "All biomes" : string.Join(", ", _biomes);
    public IReadOnlyList<string> SelectedBiomes => _biomes;
    public string MaterialSummary => _materialChoices.Where(c => c.IsSelected).Select(c => c.Key).ToList() is { Count: > 0 } keys
        ? keys[0] + (keys.Count > 1 ? $" + {keys.Count - 1}" : "") : "All materials";
    public string TagSearch { get => _tagSearch; set { if (SetProperty(ref _tagSearch, value)) Refresh(); } }
    public bool MatchAllTags { get => _allTags; set { if (SetProperty(ref _allTags, value)) Refresh(); } }
    public bool MatchAllMaterials { get => _allMaterials; set { if (SetProperty(ref _allMaterials, value)) Refresh(); } }
    public string MaterialSearch { get => _materialSearch; set { if (SetProperty(ref _materialSearch, value)) UpdateMaterialGroups(); } }
    public string SelectionSummary => _allResultsSelected ? $"All {_matchingAssets.Count:N0} results selected" : SelectedAssetName;
    public string NewAssetsLabel => _newIdentities is { Count: > 0 } ? $"{_newIdentities.Count:N0} new assets — {(_newOnly ? "Show all" : "View")}" : "No new assets";
    public bool HasNewAssets => _newIdentities is { Count: > 0 };
    public double ThumbnailSize { get => _thumbnailSize; set { if (SetProperty(ref _thumbnailSize, Math.Clamp(value, 110, 280))) { OnPropertyChanged(nameof(TileWidth)); OnPropertyChanged(nameof(TileHeight)); SetBrowserViewportWidth(_viewportWidth); } } }
    public double TileWidth => ThumbnailSize + 30;
    public double TileHeight => ThumbnailSize + 72;

    private void InitializeSourceControls()
    {
        CollectionFacet = new("Collection", SourceFiltersChanged);
        VariantFacet = new("Variant", SourceFiltersChanged);
        foreach (var label in new[] { "Context", "Category", "Subcategory", "Type" })
        {
            var depth = SourceFacets.Count;
            SourceFacets.Add(new(label, () => SourceHierarchyChanged(depth)));
        }
    }
    private void SourceHierarchyChanged(int depth)
    {
        if (_updatingSourceFacets) return;
        var selections = _sourceBrowser.ResolveUpstream(SourceFilter(), depth);
        _updatingSourceFacets = true;
        try
        {
            for (int i = 0; i < depth; i++) SourceFacets[i].Selected = selections[i];
        }
        finally { _updatingSourceFacets = false; }
        SourceFiltersChanged();
    }
    private void RestoreSourceControls()
    {
        var saved = _state.BrowserFilter;
        _updatingSourceFacets = true;
        _biomes = saved.Biomes;
        SourceFacets[0].Selected = saved.Context;
        for (int i = 0; i < Math.Min(3, saved.Levels.Length); i++)
        {
            SourceFacets[i + 1].Selected = saved.Levels[i];
        }
        CollectionFacet.Selected = saved.Collection;
        VariantFacet.Selected = saved.Variant;
        _tagSearch = string.Join(", ", saved.Tags); _allTags = saved.AllTags; _allMaterials = saved.AllMaterials;
        _searchText = saved.Filename; _filenameCaseSensitive = saved.CaseSensitive;
        _updatingSourceFacets = false;
        RebuildSourceFacets();
        _updatingSourceFacets = true;
        foreach (var choice in _materialChoices) choice.IsSelected = saved.Materials.Contains(choice.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var parent in _materialChoices.Where(c => c.IsSelected && !c.Key.Contains(':')))
            foreach (var child in _materialChoices.Where(c => c.Key.StartsWith(parent.Key + ": ", StringComparison.OrdinalIgnoreCase))) child.IsSelected = false;
        _updatingSourceFacets = false;
        ThumbnailSize = _state.ThumbnailSize;
        foreach (var name in new[] { nameof(BiomeSummary), nameof(MaterialSummary), nameof(TagSearch), nameof(SearchText), nameof(MatchAllTags), nameof(MatchAllMaterials), nameof(FilenameCaseSensitive) }) OnPropertyChanged(name);
    }
    public void SetBiomes(IEnumerable<string> values)
    {
        _biomes = values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); OnPropertyChanged(nameof(BiomeSummary)); SourceFiltersChanged();
    }
    private void SourceFiltersChanged()
    {
        if (_updatingSourceFacets) return;
        RebuildSourceFacets(); Refresh();
    }
    private SourceBrowserFilter SourceFilter() => new()
    {
        SourceId = SelectedSourceId, Biomes = _biomes, Context = SourceFacets[0].Selected,
        Collection = CollectionFacet.Selected, Levels = SourceFacets.Skip(1).Select(f => f.Selected).ToArray(),
        Materials = _materialChoices.Where(m => m.IsSelected).Select(m => m.Key).ToArray(), AllMaterials = MatchAllMaterials,
        Tags = SplitTags(TagSearch), AllTags = MatchAllTags, Filename = SearchText, CaseSensitive = FilenameCaseSensitive,
        Variant = VariantFacet.Selected,
        Identities = _newOnly ? _newIdentities : null
    };
    private void RebuildSourceFacets()
    {
        if (SourceFacets.Count == 0) return;
        _updatingSourceFacets = true;
        try
        {
            var baseFilter = new SourceBrowserFilter { SourceId = SelectedSourceId };
            var scope = _sourceBrowser.Filter(baseFilter);
            var biomeValues = scope.Select(e => e.Taxonomy.Biome).Where(s => s.Length > 0).Distinct().Order(StringComparer.OrdinalIgnoreCase).ToArray();
            if (!BiomeOptions.SequenceEqual(biomeValues)) Replace(BiomeOptions, biomeValues);
            _biomes = _biomes.Where(b => BiomeOptions.Contains(b)).ToArray();
            scope = _sourceBrowser.Filter(baseFilter with { Biomes = _biomes });
            bool cleared = false;
            // Only upstream facets affect available values. Skipping any level remains valid.
            for (int i = 0; i < SourceFacets.Count; i++)
            {
                var depth = i - 1;
                cleared |= SourceFacets[i].SetOptions(scope.Select(e => depth < 0 ? e.Taxonomy.Context : e.Taxonomy.Level(depth)));
                var selected = SourceFacets[i].Selected;
                scope = scope.Where(e => SourceBrowserIndex.Match(depth < 0 ? e.Taxonomy.Context : e.Taxonomy.Level(depth), selected)).ToList();
            }
            cleared |= CollectionFacet.SetOptions(scope.Select(e => e.Taxonomy.Collection));
            scope = scope.Where(e => SourceBrowserIndex.Match(e.Taxonomy.Collection, CollectionFacet.Selected)).ToList();
            cleared |= VariantFacet.SetOptions(scope.Select(e => e.Asset.Variant).Where(v => v != "Unspecified"));
            var selectedKeys = _materialChoices.Where(c => c.IsSelected).Select(c => c.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var keys = scope.SelectMany(e => e.Taxonomy.Materials.SelectMany(m => new[] { m.Material, m.Key })).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase);
            _materialChoices.Clear();
            foreach (var key in keys) _materialChoices.Add(new MaterialChoice(key, MaterialSelectionChanged) { IsSelected = selectedKeys.Contains(key) });
            if (selectedKeys.Except(_materialChoices.Select(c => c.Key), StringComparer.OrdinalIgnoreCase).Any()) cleared = true;
            UpdateMaterialGroups(); OnPropertyChanged(nameof(MaterialSummary)); OnPropertyChanged(nameof(BiomeSummary));
            OnPropertyChanged(nameof(PrimarySourceFacets));
            if (cleared) StatusText = "Unavailable downstream selections cleared to All.";
        }
        finally { _updatingSourceFacets = false; }
    }
    private void UpdateMaterialGroups()
    {
        MaterialGroups.Clear();
        foreach (var g in _materialChoices.GroupBy(c => c.Key.Split(':')[0]))
        {
            if (!g.Any(c => c.Key.Contains(MaterialSearch, StringComparison.OrdinalIgnoreCase))) continue;
            MaterialGroups.Add(new(g.Key, g.First(c => c.Key == g.Key),
                g.Where(c => c.Key != g.Key && c.Key.Contains(MaterialSearch, StringComparison.OrdinalIgnoreCase)).ToArray()));
        }
    }
    private void MaterialSelectionChanged(MaterialChoice choice)
    {
        if (_updatingSourceFacets) return;
        _updatingSourceFacets = true;
        try
        {
            if (choice.IsSelected)
            {
                var material = choice.Key.Split(':')[0];
                foreach (var other in _materialChoices.Where(c => !ReferenceEquals(c, choice) && c.Key.Split(':')[0] == material))
                    if (choice.Key == material || other.Key == material) other.IsSelected = false;
            }
        }
        finally { _updatingSourceFacets = false; }
        OnPropertyChanged(nameof(MaterialSummary)); Refresh();
    }
    public void ClearMaterials()
    {
        _updatingSourceFacets = true; foreach (var c in _materialChoices) c.IsSelected = false; _updatingSourceFacets = false;
        OnPropertyChanged(nameof(MaterialSummary)); Refresh();
    }
    private void ResetSourceFilters()
    {
        _updatingSourceFacets = true; _biomes = []; _tagSearch = ""; _newOnly = false;
        foreach (var facet in SourceFacets) facet.Selected = All;
        CollectionFacet.Selected = All; foreach (var c in _materialChoices) c.IsSelected = false;
        VariantFacet.Selected = All;
        _updatingSourceFacets = false; RebuildSourceFacets(); OnPropertyChanged(nameof(TagSearch)); OnPropertyChanged(nameof(NewAssetsLabel));
    }
    public void SelectAllBrowserResults()
    {
        _allResultsSelected = true;
        foreach (var tile in _visibleTiles) tile.IsSelected = true;
        OnPropertyChanged(nameof(SelectionSummary)); StatusText = SelectionSummary + "; tag edits include results beyond the preview.";
    }
    public AssetRecord[] GetTagEditTargets() => (_allResultsSelected ? _matchingAssets : SelectedAssets.Select(t => t.Asset))
        .DistinctBy(a => a.StableIdentity, StringComparer.OrdinalIgnoreCase).ToArray();
    public Task EditSelectedTagsAsync(string text, bool remove) => EditTagsAsync(GetTagEditTargets(), text, remove);
    public async Task EditTagsAsync(IReadOnlyList<AssetRecord> targets, string text, bool remove)
    {
        var tags = SplitTags(text); if (tags.Length == 0) return;
        if (targets.Count == 0) return;
        var automaticById = _sourceBrowser.Entries.ToDictionary(e => e.Asset.StableIdentity, e => e.Taxonomy.Tags, StringComparer.OrdinalIgnoreCase);
        var previousTags = new Dictionary<string, UserAssetTags>(_state.UserTags, StringComparer.OrdinalIgnoreCase);
        foreach (var asset in targets)
        {
            _state.UserTags.TryGetValue(asset.StableIdentity, out var previous);
            if (!automaticById.TryGetValue(asset.StableIdentity, out var sourceTags)) continue;
            var edited = (previous ?? new()).Edit(tags, remove, sourceTags);
            if (edited.Added.Count == 0 && edited.Suppressed.Count == 0) _state.UserTags.Remove(asset.StableIdentity);
            else _state.UserTags[asset.StableIdentity] = edited;
        }
        try { await SaveStateAsync(); }
        catch
        {
            _state.UserTags.Clear();
            foreach (var pair in previousTags) _state.UserTags[pair.Key] = pair.Value;
            throw;
        }
        await RebuildSourceIndexAsync(); await RefreshSectionsAsync();
        StatusText = $"Saved: {(remove ? "removed" : "added")} tags on {targets.Count:N0} assets. Source files unchanged.";
    }
    public string SelectedTagDetails() => TagDetails(SelectedAsset?.Asset);
    public string TagDetails(AssetRecord? asset)
    {
        if (asset is null) return "Select an asset to inspect tags.";
        var entry = _sourceBrowser.Entries.FirstOrDefault(e => e.Asset.StableIdentity == asset.StableIdentity);
        if (entry is null) return "No indexed metadata.";
        _state.UserTags.TryGetValue(entry.Asset.StableIdentity, out var user);
        return $"{entry.Taxonomy.Biome} > {entry.Taxonomy.DisplayPath}\nCollection: {entry.Taxonomy.Collection}\nVariant: {entry.Asset.Variant}\nMaterials: {string.Join(", ", entry.Taxonomy.Materials.Select(m => m.Key))}\n\nSource tags: {string.Join(", ", entry.Taxonomy.Tags)}\n\nUser tags: {string.Join(", ", user?.Added ?? [])}\nSuppressed: {string.Join(", ", user?.Suppressed ?? [])}";
    }
    private async Task RebuildSourceIndexAsync()
    {
        _sourceBrowser = await Task.Run(() => new SourceBrowserIndex(_assets, _state.Libraries, _state.UserTags));
        if (_plannerReady)
        {
            _sourcePlannerCatalog = new SourcePlannerCatalog(_sourceBrowser, _catalog, _wallCatalog);
            WallsPlannerFilter.RefreshChoices(); FloorPlannerFilter.RefreshChoices(); TrimPlannerFilter.RefreshChoices();
            _buildIsPopulated = false;
            if (IsBuildMode) RefreshWallSetPreview();
        }
    }
    private void RecordNewAssets(HashSet<string> before)
    {
        _newIdentities = _assets.Where(a => !before.Contains(a.StableIdentity)).Select(a => a.StableIdentity).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _newOnly = false; OnPropertyChanged(nameof(NewAssetsLabel)); OnPropertyChanged(nameof(HasNewAssets));
    }
    public void ToggleNewAssets() { _newOnly = !_newOnly; OnPropertyChanged(nameof(NewAssetsLabel)); Refresh(); }
    private static string[] SplitTags(string text) => text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
