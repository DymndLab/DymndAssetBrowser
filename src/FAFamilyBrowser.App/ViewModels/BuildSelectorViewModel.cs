using System.Collections.ObjectModel;
using FAFamilyBrowser.App.Infrastructure;
using FAFamilyBrowser.Core.Models;

namespace FAFamilyBrowser.App.ViewModels;

public sealed class BuildSelectorViewModel : ViewModelBase
{
    private readonly BuildComponent _component;
    private readonly Func<AssetCatalogIndex> _assets;
    private readonly Func<string> _sourceId;
    private bool _suppress;
    private string _selectedGroup = AssetQueries.All;
    private string _selectedSubGroup = AssetQueries.All;
    private string _selectedMaterial = AssetQueries.All;
    private string _selectedStyle = AssetQueries.All;
    private string _selectedTheme = AssetQueries.All;
    private string _selectedFamily = AssetQueries.All;
    private string _selectedVariant = AssetQueries.All;
    private string _selectedAssetIdentity = string.Empty;
    private string _selectedAssetName = string.Empty;
    private bool _acceptWallDefaults = true;

    public BuildSelectorViewModel(string title, BuildComponent component, Func<AssetCatalogIndex> assets,
        Func<string> sourceId)
    {
        Title = title;
        _component = component;
        _assets = assets;
        _sourceId = sourceId;
    }

    public event EventHandler? SelectionChanged;
    public string Title { get; }
    public string FamilyLabel => _component == BuildComponent.Floor ? "Texture Set" : "Opening Type";
    public ObservableCollection<string> Groups { get; } = [];
    public ObservableCollection<string> SubGroups { get; } = [];
    public ObservableCollection<string> Materials { get; } = [];
    public ObservableCollection<string> Styles { get; } = [];
    public ObservableCollection<string> Themes { get; } = [];
    public ObservableCollection<string> Families { get; } = [];
    public ObservableCollection<string> Variants { get; } = [];

    public string SelectedGroup { get => _selectedGroup; set => SetSelection(ref _selectedGroup, value, AssetFacet.Group); }
    public string SelectedSubGroup { get => _selectedSubGroup; set => SetSelection(ref _selectedSubGroup, value, AssetFacet.SubGroup); }
    public string SelectedMaterial { get => _selectedMaterial; set => SetSelection(ref _selectedMaterial, value, AssetFacet.Material); }
    public string SelectedStyle { get => _selectedStyle; set => SetSelection(ref _selectedStyle, value, AssetFacet.Style); }
    public string SelectedTheme { get => _selectedTheme; set => SetSelection(ref _selectedTheme, value, AssetFacet.Theme); }
    public string SelectedFamily { get => _selectedFamily; set => SetSelection(ref _selectedFamily, value, AssetFacet.Family); }
    public string SelectedVariant { get => _selectedVariant; set => SetSelection(ref _selectedVariant, value, AssetFacet.Variant); }
    public bool IsUnfiltered => string.IsNullOrWhiteSpace(_selectedAssetIdentity)
        && new[] { SelectedMaterial, SelectedStyle, SelectedTheme, SelectedFamily, SelectedVariant }
            .All(IsAll);
    public bool CanReceiveWallDefaults => _acceptWallDefaults;

    public string Summary
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_selectedAssetName)) return _selectedAssetName;
            var values = new[] { SelectedMaterial, SelectedStyle, SelectedTheme, SelectedFamily, SelectedVariant }
                .Where(value => value != AssetQueries.All && !value.Equals("Unspecified", StringComparison.OrdinalIgnoreCase));
            var summary = string.Join(" > ", values);
            return string.IsNullOrWhiteSpace(summary) ? "All matching assets" : summary;
        }
    }

    public BuildSelection Selection => new()
    {
        AssetIdentity = _selectedAssetIdentity,
        Group = SelectedGroup,
        SubGroup = SelectedSubGroup,
        Material = SelectedMaterial,
        Style = SelectedStyle,
        Theme = SelectedTheme,
        Family = SelectedFamily,
        Variant = SelectedVariant
    };

    public void Restore(BuildSelection selection)
    {
        _suppress = true;
        _selectedGroup = Normalize(selection.Group);
        _selectedSubGroup = Normalize(selection.SubGroup);
        _selectedMaterial = Normalize(selection.Material);
        _selectedStyle = Normalize(selection.Style);
        _selectedTheme = Normalize(selection.Theme);
        _selectedFamily = Normalize(selection.Family);
        _selectedVariant = Normalize(selection.Variant);
        _selectedAssetIdentity = selection.AssetIdentity ?? string.Empty;
        _selectedAssetName = string.IsNullOrWhiteSpace(_selectedAssetIdentity)
            ? string.Empty
            : Path.GetFileNameWithoutExtension(_assets().Assets.FirstOrDefault(asset =>
                asset.StableIdentity.Equals(_selectedAssetIdentity, StringComparison.OrdinalIgnoreCase))?.FileName ?? string.Empty);
        RefreshChoicesCore();
        NotifySelections();
        _acceptWallDefaults = IsUnfiltered;
        _suppress = false;
    }

    public void RefreshChoices()
    {
        _suppress = true;
        RefreshChoicesCore();
        NotifySelections();
        _suppress = false;
    }

    public void Reset()
    {
        Restore(BuildModeService.Canonicalize(new BuildSelection(), _component));
        _acceptWallDefaults = true;
    }

    public bool SelectAsset(AssetRecord asset)
    {
        if (asset.StableIdentity.Equals(_selectedAssetIdentity, StringComparison.OrdinalIgnoreCase)) return false;
        _suppress = true;
        _selectedAssetIdentity = asset.StableIdentity;
        _selectedAssetName = Path.GetFileNameWithoutExtension(asset.FileName);
        _acceptWallDefaults = false;
        _selectedGroup = asset.Group;
        _selectedSubGroup = asset.SubGroup;
        _selectedMaterial = asset.Material;
        _selectedStyle = asset.Style;
        _selectedTheme = asset.Theme;
        _selectedFamily = asset.Family;
        _selectedVariant = asset.Variant;
        RefreshChoicesCore();
        NotifySelections();
        _suppress = false;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool SelectMaterialAppearance(AssetRecord asset)
    {
        var selection = BuildModeService.Canonicalize(new BuildSelection(), _component) with
        {
            Material = asset.Material,
            Style = Specified(asset.Style) ? asset.Style : AssetQueries.All,
            Theme = SelectedTheme
        };
        if (Selection == selection) return false;
        Restore(selection);
        _acceptWallDefaults = false;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool TrySeedMaterialAppearance(string material, string appearance)
    {
        if (!CanReceiveWallDefaults || !Specified(material)) return false;
        var materialChoice = Materials.FirstOrDefault(value => Equivalent(value, material));
        if (materialChoice is null) return false;

        var selection = BuildModeService.Canonicalize(new BuildSelection(), _component) with
        {
            Material = materialChoice
        };
        var appearanceChoice = Styles.FirstOrDefault(value => Equivalent(value, appearance));
        if (appearanceChoice is not null)
        {
            var withAppearance = selection with { Style = appearanceChoice };
            if (BuildModeService.MatchSelection(_assets(), _component, withAppearance, _sourceId()).Count > 0)
                selection = withAppearance;
        }
        if (BuildModeService.MatchSelection(_assets(), _component, selection, _sourceId()).Count == 0) return false;
        Restore(selection);
        _acceptWallDefaults = true;
        return true;
    }

    public void ClearAutomaticWallDefaults()
    {
        if (!_acceptWallDefaults || IsUnfiltered) return;
        Reset();
    }

    public void FilterTo(AssetFacet facet, string value)
    {
        if (facet is not (AssetFacet.Material or AssetFacet.Style or AssetFacet.Theme or AssetFacet.Family or AssetFacet.Variant))
            return;

        var selection = BuildModeService.Canonicalize(new BuildSelection(), _component) with
        {
            Material = facet == AssetFacet.Material ? value : AssetQueries.All,
            Style = facet == AssetFacet.Style ? value : AssetQueries.All,
            Theme = facet == AssetFacet.Theme ? value : AssetQueries.All,
            Family = facet == AssetFacet.Family ? value : AssetQueries.All,
            Variant = facet == AssetFacet.Variant ? value : AssetQueries.All
        };

        Restore(selection);
        _acceptWallDefaults = false;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetSelection(ref string field, string? value, AssetFacet changedFacet)
    {
        // Replacing an ItemsSource makes WPF briefly push null through SelectedItem.
        // Ignore that transient binding update while choices are being rebuilt.
        if (_suppress || !SetProperty(ref field, Normalize(value))) return;
        _selectedAssetIdentity = string.Empty;
        _selectedAssetName = string.Empty;
        _acceptWallDefaults = false;
        _suppress = true;
        RefreshChoicesCore(changedFacet);
        NotifySelections();
        _suppress = false;
        OnPropertyChanged(nameof(Summary));
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshChoicesCore(AssetFacet? protectedFacet = null)
    {
        // Two passes settle dependent values after an incompatible prior choice is cleared to All.
        // The facet being actively changed remains available because ValuesForFacet ignores itself.
        for (var pass = 0; pass < 2; pass++)
        {
            var selection = Selection;
            var group = _selectedGroup;
            var subGroup = _selectedSubGroup;
            var material = _selectedMaterial;
            var style = _selectedStyle;
            var theme = _selectedTheme;
            var family = _selectedFamily;
            var variant = _selectedVariant;

            Replace(Groups, BuildModeService.ValuesForFacet(_assets(), _component, selection, AssetFacet.Group, _sourceId()));
            Replace(SubGroups, BuildModeService.ValuesForFacet(_assets(), _component, selection, AssetFacet.SubGroup, _sourceId()));
            Replace(Materials, BuildModeService.ValuesForFacet(_assets(), _component, selection, AssetFacet.Material, _sourceId()));
            Replace(Styles, BuildModeService.ValuesForFacet(_assets(), _component, selection, AssetFacet.Style, _sourceId()));
            Replace(Themes, BuildModeService.ValuesForFacet(_assets(), _component, selection, AssetFacet.Theme, _sourceId()));
            Replace(Families, BuildModeService.ValuesForFacet(_assets(), _component, selection, AssetFacet.Family, _sourceId()));
            Replace(Variants, BuildModeService.ValuesForFacet(_assets(), _component, selection, AssetFacet.Variant, _sourceId()));

            if (pass > 0 || protectedFacet != AssetFacet.Group) _selectedGroup = Pick(Groups, group);
            if (pass > 0 || protectedFacet != AssetFacet.SubGroup) _selectedSubGroup = Pick(SubGroups, subGroup);
            if (pass > 0 || protectedFacet != AssetFacet.Material) _selectedMaterial = Pick(Materials, material);
            if (pass > 0 || protectedFacet != AssetFacet.Style) _selectedStyle = Pick(Styles, style);
            if (pass > 0 || protectedFacet != AssetFacet.Theme) _selectedTheme = Pick(Themes, theme);
            if (pass > 0 || protectedFacet != AssetFacet.Family) _selectedFamily = Pick(Families, family);
            if (pass > 0 || protectedFacet != AssetFacet.Variant) _selectedVariant = Pick(Variants, variant);
        }
    }

    private void NotifySelections()
    {
        OnPropertyChanged(nameof(SelectedGroup));
        OnPropertyChanged(nameof(SelectedSubGroup));
        OnPropertyChanged(nameof(SelectedMaterial));
        OnPropertyChanged(nameof(SelectedStyle));
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(SelectedFamily));
        OnPropertyChanged(nameof(SelectedVariant));
        OnPropertyChanged(nameof(Summary));
    }

    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? AssetQueries.All : value;
    private static bool IsAll(string? value) => string.IsNullOrWhiteSpace(value)
        || value.Equals(AssetQueries.All, StringComparison.OrdinalIgnoreCase);
    private static bool Specified(string? value) => !IsAll(value)
        && !value!.Equals("Unspecified", StringComparison.OrdinalIgnoreCase)
        && !value.Equals("N/A", StringComparison.OrdinalIgnoreCase);
    private static bool Equivalent(string? left, string? right) =>
        (left ?? string.Empty).Replace('_', ' ').Equals((right ?? string.Empty).Replace('_', ' '), StringComparison.OrdinalIgnoreCase);
    private static string Pick(IEnumerable<string> values, string preferred) =>
        values.FirstOrDefault(value => value.Equals(preferred, StringComparison.OrdinalIgnoreCase)) ?? AssetQueries.All;
    private static void Replace(ObservableCollection<string> target, IEnumerable<string> values)
    {
        var desired = values.ToList();
        for (var index = target.Count - 1; index >= 0; index--)
        {
            if (!desired.Contains(target[index], StringComparer.OrdinalIgnoreCase)) target.RemoveAt(index);
        }

        for (var desiredIndex = 0; desiredIndex < desired.Count; desiredIndex++)
        {
            var existingIndex = IndexOf(target, desired[desiredIndex]);
            if (existingIndex < 0) target.Insert(desiredIndex, desired[desiredIndex]);
            else if (existingIndex != desiredIndex) target.Move(existingIndex, desiredIndex);
        }
    }

    private static int IndexOf(IEnumerable<string> values, string sought)
    {
        var index = 0;
        foreach (var value in values)
        {
            if (value.Equals(sought, StringComparison.OrdinalIgnoreCase)) return index;
            index++;
        }
        return -1;
    }
}
