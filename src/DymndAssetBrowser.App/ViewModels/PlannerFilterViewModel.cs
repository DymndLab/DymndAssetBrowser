using System.Collections.ObjectModel;
using DymndAssetBrowser.App.Infrastructure;
using DymndAssetBrowser.Core.Models;

namespace DymndAssetBrowser.App.ViewModels;

public sealed class PlannerVariantChoice(string value) : ViewModelBase
{
    private bool _hasMatches = true;
    public string Value { get; } = value;
    public bool HasMatches { get => _hasMatches; set => SetProperty(ref _hasMatches, value); }
}

public sealed class PlannerFilterViewModel : ViewModelBase
{
    private readonly Func<SourcePlannerCatalog> _catalog;
    private readonly Func<string> _sourceId;
    private bool _updating;
    private string[] _biomes = [];
    private string _tags = "", _materialSearch = "", _identity = "";
    private bool _allTags, _allMaterials, _excludeAdditionalMaterials;
    private bool _exactMaterialFinishes, _materialsFollowWalls;
    private readonly List<MaterialChoice> _materials = [];
    public PlannerFilterViewModel(BuildComponent component, Func<SourcePlannerCatalog> catalog, Func<string> sourceId)
    {
        Component = component; _catalog = catalog; _sourceId = sourceId;
        CollectionFacet = new("Settlement type", HierarchyChanged);
        VariantFacet = new("Variant", Changed);
    }
    public event EventHandler? SelectionChanged;
    public BuildComponent Component { get; }
    public string Title => Component.ToString();
    public ObservableCollection<string> BiomeOptions { get; } = [];
    public ObservableCollection<MaterialChoiceGroup> MaterialGroups { get; } = [];
    public ObservableCollection<PlannerVariantChoice> VariantOptions { get; } = [];
    public SourceFacetChoice CollectionFacet { get; }
    public SourceFacetChoice VariantFacet { get; }
    public IReadOnlyList<string> SelectedBiomes => _biomes;
    public string BiomeSummary => _biomes.Length == 0 ? "All biomes" : string.Join(", ", _biomes);
    public string MaterialSummary => _materials.Where(m => m.IsSelected).Select(m => m.Key).ToArray() is { Length: > 0 } keys ? string.Join(", ", keys) + (_exactMaterialFinishes ? " (only these finishes)" : _excludeAdditionalMaterials ? " (no other materials)" : "") : "All materials";
    public string Tags { get => _tags; set { if (SetProperty(ref _tags, value)) Changed(); } }
    public bool MatchAllTags { get => _allTags; set { if (SetProperty(ref _allTags, value)) Changed(); } }
    public bool MatchAllMaterials { get => _allMaterials; set { if (SetProperty(ref _allMaterials, value)) Changed(); } }
    public bool ExcludeAdditionalMaterials { get => _excludeAdditionalMaterials; set { if (SetProperty(ref _excludeAdditionalMaterials, value)) { _exactMaterialFinishes = false; Changed(); } } }
    public string MaterialSearch { get => _materialSearch; set { if (SetProperty(ref _materialSearch, value)) UpdateMaterialGroups(); } }
    public PlannerSourceSelection Selection => new()
    {
        SelectedIdentity = _identity,
        MaterialsFollowWalls = _materialsFollowWalls,
        Filter = new() { Biomes = _biomes, Collection = CollectionFacet.Selected, Materials = _materials.Where(m => m.IsSelected).Select(m => m.Key).ToArray(),
            AllMaterials = _allMaterials, ExcludeAdditionalMaterials = _excludeAdditionalMaterials, ExactMaterialFinishes = _exactMaterialFinishes, Tags = _tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries), AllTags = _allTags, Variant = VariantFacet.Selected }
    };
    public bool CanReceiveWallMaterials => _materialsFollowWalls || (_identity.Length == 0 && _biomes.Length == 0
        && CollectionFacet.Selected == "All" && VariantFacet.Selected == "All" && string.IsNullOrWhiteSpace(_tags)
        && !_materials.Any(m => m.IsSelected) && !_allTags && !_allMaterials && !_excludeAdditionalMaterials);
    public int MatchingCount => Component == BuildComponent.Walls ? _catalog().WallSets(Selection, _sourceId()).Count : _catalog().Assets(Component, Selection, _sourceId()).Count;
    public bool HasPinnedSelection => _identity.Length > 0;
    public string Summary
    {
        get
        {
            if (Component == BuildComponent.Walls)
            {
                var sets = _catalog().WallSets(Selection, _sourceId());
                return sets.Count == 1 ? sets[0].DisplayName : $"{sets.Count:N0} matching wall sets";
            }
            var assets = _catalog().Assets(Component, Selection, _sourceId());
            return _identity.Length > 0 && assets.Count == 1 ? assets[0].FileName : $"{assets.Count:N0} matching assets";
        }
    }
    public void SetBiomes(IEnumerable<string> values) { _biomes = values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); HierarchyChanged(); }
    public void ClearMaterials() { _updating = true; foreach (var m in _materials) m.IsSelected = false; _exactMaterialFinishes = false; _updating = false; Changed(); }
    public void Reset() { Restore(new()); SelectionChanged?.Invoke(this, EventArgs.Empty); }
    public void ClearPinnedSelection() { _identity = ""; Notify(); SelectionChanged?.Invoke(this, EventArgs.Empty); }
    public bool SelectIdentity(string identity)
    {
        if (_identity.Equals(identity, StringComparison.OrdinalIgnoreCase)) return true;
        var candidate = Selection with { SelectedIdentity = identity };
        var valid = Component == BuildComponent.Walls ? _catalog().WallSets(candidate, _sourceId()).Count == 1 : _catalog().Assets(Component, candidate, _sourceId()).Count == 1;
        if (!valid) return false;
        _identity = identity; _materialsFollowWalls = false; Notify(); SelectionChanged?.Invoke(this, EventArgs.Empty); return true;
    }
    public void SelectMaterials(IEnumerable<string> keys, bool? matchAll = null, bool exactFinishes = false, bool followWalls = false)
    {
        var set = keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _updating = true;
        if (matchAll.HasValue) _allMaterials = matchAll.Value;
        _exactMaterialFinishes = exactFinishes;
        if (exactFinishes) _excludeAdditionalMaterials = true;
        var addedChoices = false;
        // Retain transferred finishes even if the current biome/collection has no
        // matching trim. Dropping them would silently turn the query into All.
        foreach (var key in set.SelectMany(k => new[] { k.Split(':')[0], k }).Distinct(StringComparer.OrdinalIgnoreCase))
            if (!_materials.Any(m => m.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                _materials.Add(new MaterialChoice(key, MaterialChanged));
                addedChoices = true;
            }
        foreach (var m in _materials) m.IsSelected = set.Contains(m.Key);
        foreach (var parent in _materials.Where(m => m.IsSelected && !m.Key.Contains(':')))
            foreach (var child in _materials.Where(m => m.Key.StartsWith(parent.Key + ": ", StringComparison.OrdinalIgnoreCase))) child.IsSelected = false;
        _updating = false;
        if (addedChoices) UpdateMaterialGroups();
        _identity = ""; _materialsFollowWalls = followWalls;
        Notify(); SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
    public void Restore(PlannerSourceSelection state)
    {
        _updating = true;
        _biomes = state.Filter.Biomes; _tags = string.Join(", ", state.Filter.Tags); _allTags = state.Filter.AllTags; _allMaterials = state.Filter.AllMaterials;
        _excludeAdditionalMaterials = state.Filter.ExcludeAdditionalMaterials;
        _exactMaterialFinishes = state.Filter.ExactMaterialFinishes;
        _materialsFollowWalls = state.MaterialsFollowWalls;
        CollectionFacet.Selected = state.Filter.Collection; VariantFacet.Selected = state.Filter.Variant; _identity = state.SelectedIdentity;
        _updating = false;
        RefreshChoices(state.Filter.Materials);
    }
    public void RefreshChoices() => RefreshChoices(null);
    private void RefreshChoices(string[]? restoringMaterials)
    {
        if (_updating) return;
        _updating = true;
        try
        {
            var scope = _catalog().Scope(Component).Filter(new() { SourceId = _sourceId() });
            var biomes = scope.Select(e => e.Taxonomy.Biome).Concat(_biomes).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
            if (!BiomeOptions.SequenceEqual(biomes)) { BiomeOptions.Clear(); foreach (var b in biomes) BiomeOptions.Add(b); }
            scope = scope.Where(e => _biomes.Length == 0 || _biomes.Contains(e.Taxonomy.Biome, StringComparer.OrdinalIgnoreCase)).ToList();
            CollectionFacet.SetOptions(scope.Select(e => e.Taxonomy.Collection).Append(CollectionFacet.Selected).Where(v => v != "All"));
            scope = scope.Where(e => SourceBrowserIndex.Match(e.Taxonomy.Collection, CollectionFacet.Selected)).ToList();
            VariantFacet.SetOptions(scope.Select(e => e.Asset.Variant).Append(VariantFacet.Selected).Where(v => v is not "Unspecified" and not "All"));
            var selected = (restoringMaterials ?? _materials.Where(m => m.IsSelected).Select(m => m.Key).ToArray()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _materials.Clear();
            foreach (var key in scope.SelectMany(e => e.Taxonomy.Materials.SelectMany(m => new[] { m.Material, m.Key }))
                .Concat(selected.SelectMany(k => new[] { k.Split(':')[0], k })).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
                _materials.Add(new MaterialChoice(key, MaterialChanged) { IsSelected = selected.Contains(key) });
            UpdateMaterialGroups();
        }
        finally { _updating = false; }
        // Retain a saved pin even if its file is offline/missing: show zero rather
        // than silently replacing the user's choice with every available asset.
        Notify();
    }
    private void Changed()
    {
        if (_updating) return;
        _identity = ""; _materialsFollowWalls = false; Notify(); SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
    private void HierarchyChanged()
    {
        if (_updating) return;
        _identity = ""; _materialsFollowWalls = false; RefreshChoices(); SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
    private void MaterialChanged(MaterialChoice choice)
    {
        if (_updating) return;
        _exactMaterialFinishes = false;
        _updating = true;
        if (choice.IsSelected)
        {
            var material = choice.Key.Split(':')[0];
            foreach (var other in _materials.Where(m => m != choice && m.Key.Split(':')[0] == material))
                if (choice.Key == material || other.Key == material) other.IsSelected = false;
        }
        _updating = false; Changed();
    }
    private void UpdateMaterialGroups()
    {
        MaterialGroups.Clear();
        foreach (var group in _materials.GroupBy(m => m.Key.Split(':')[0]))
            if (group.Any(m => m.Key.Contains(_materialSearch, StringComparison.OrdinalIgnoreCase)))
                MaterialGroups.Add(new(group.Key, group.First(m => m.Key == group.Key), group.Where(m => m.Key.Contains(':') && m.Key.Contains(_materialSearch, StringComparison.OrdinalIgnoreCase)).ToArray()));
    }
    private void Notify()
    {
        UpdateMaterialAvailability();
        UpdateVariantAvailability();
        foreach (var name in new[] { nameof(BiomeSummary), nameof(MaterialSummary), nameof(Tags), nameof(MatchAllTags), nameof(MatchAllMaterials), nameof(ExcludeAdditionalMaterials), nameof(Summary), nameof(MatchingCount), nameof(HasPinnedSelection) }) OnPropertyChanged(name);
    }

    private void UpdateMaterialAvailability()
    {
        var filter = Selection.Filter;
        // A material edit clears the exact-item/set pin. Evaluate the choice as
        // that edit would behave, retaining biome, collection, tags and variant.
        var entries = _catalog().Scope(Component).Filter(filter with { SourceId = _sourceId(), Materials = [] })
            .Select(e => (Keys: e.Taxonomy.Materials.SelectMany(m => new[] { m.Material, m.Key }).ToHashSet(StringComparer.OrdinalIgnoreCase),
                Types: e.Taxonomy.Materials.Select(m => m.Material).Distinct(StringComparer.OrdinalIgnoreCase).ToArray())).ToArray();
        foreach (var choice in _materials)
        {
            var material = choice.Key.Split(':')[0];
            // Selecting a finish replaces its Any parent; selecting Any replaces
            // its finishes. Other selected finishes remain constraints in All mode.
            var remaining = filter.Materials.Where(k => !k.Equals(choice.Key, StringComparison.OrdinalIgnoreCase)
                && !(choice.Key == material ? k.StartsWith(material + ":", StringComparison.OrdinalIgnoreCase) : k.Equals(material, StringComparison.OrdinalIgnoreCase))).ToArray();
            var required = filter.AllMaterials ? remaining : [];
            var allowed = remaining.Append(choice.Key).Select(k => k.Split(':')[0]).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // In Any mode the candidate must itself contribute matches; results
            // supplied solely by another checked material must not mask a dead end.
            choice.HasMatches = entries.Any(e => e.Keys.Contains(choice.Key) && required.All(e.Keys.Contains)
                && (!filter.ExcludeAdditionalMaterials || e.Types.All(allowed.Contains)));
        }
    }

    private void UpdateVariantAvailability()
    {
        // Replace the current variant, not intersect it with the candidate. Like
        // other filter edits, choosing a variant clears the exact preview pin.
        var matches = _catalog().Scope(Component).Filter(Selection.Filter with { SourceId = _sourceId(), Variant = "All" })
            .Select(e => e.Asset.Variant).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selected = VariantFacet.Selected;
        var wasUpdating = _updating;
        _updating = true;
        try
        {
            foreach (var old in VariantOptions.Where(o => !VariantFacet.Options.Contains(o.Value)).ToArray()) VariantOptions.Remove(old);
            for (var i = 0; i < VariantFacet.Options.Count; i++)
            {
                var value = VariantFacet.Options[i];
                var option = VariantOptions.FirstOrDefault(o => o.Value == value);
                if (option is null) { option = new(value); VariantOptions.Insert(i, option); }
                else if (VariantOptions.IndexOf(option) != i) VariantOptions.Move(VariantOptions.IndexOf(option), i);
                // All is always an escape hatch, even when another filter has zero matches.
                option.HasMatches = value == "All" || matches.Contains(value);
            }
            VariantFacet.Selected = selected;
        }
        finally { _updating = wasUpdating; }
    }
}
