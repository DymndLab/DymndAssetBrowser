using System.Collections.ObjectModel;
using FAFamilyBrowser.App.Infrastructure;
using FAFamilyBrowser.Core.Models;

namespace FAFamilyBrowser.App.ViewModels;

public sealed class WallSetSelectorViewModel : ViewModelBase
{
    private readonly Func<WallConstructionCatalog> _catalog;
    private readonly Func<string> _sourceId;
    private bool _suppress;
    private string _setId = AssetQueries.All;
    private string _wallSystem = AssetQueries.All;
    private string _primaryAppearance = AssetQueries.All;
    private string _secondaryAppearance = AssetQueries.All;
    private string _theme = AssetQueries.All;
    private string _profile = AssetQueries.All;
    private string _variant = AssetQueries.All;

    public WallSetSelectorViewModel(Func<WallConstructionCatalog> catalog, Func<string> sourceId)
    {
        _catalog = catalog;
        _sourceId = sourceId;
    }

    public event EventHandler? SelectionChanged;
    public string Title => "Walls";
    public ObservableCollection<string> WallSystems { get; } = [];
    public ObservableCollection<string> PrimaryAppearances { get; } = [];
    public ObservableCollection<string> SecondaryAppearances { get; } = [];
    public ObservableCollection<string> Themes { get; } = [];
    public ObservableCollection<string> Profiles { get; } = [];
    public ObservableCollection<string> Variants { get; } = [];

    public string SelectedWallSystem { get => _wallSystem; set => SetSelection(ref _wallSystem, value, WallSetFacet.WallSystem); }
    public string SelectedPrimaryAppearance { get => _primaryAppearance; set => SetSelection(ref _primaryAppearance, value, WallSetFacet.PrimaryAppearance); }
    public string SelectedSecondaryAppearance { get => _secondaryAppearance; set => SetSelection(ref _secondaryAppearance, value, WallSetFacet.SecondaryAppearance); }
    public string SelectedTheme { get => _theme; set => SetSelection(ref _theme, value, WallSetFacet.Theme); }
    public string SelectedProfile { get => _profile; set => SetSelection(ref _profile, value, WallSetFacet.Profile); }
    public string SelectedVariant { get => _variant; set => SetSelection(ref _variant, value, WallSetFacet.Variant); }

    public string PrimaryComponentLabel => Components.ElementAtOrDefault(0) is { Length: > 0 } component
        ? component
        : "Primary appearance";
    public string SecondaryComponentLabel => Components.ElementAtOrDefault(1) is { Length: > 0 } component
        ? component
        : "Secondary appearance";
    public bool HasSecondaryComponent => Components.Count > 1;
    public int CandidateCount => MatchingSets.Count;
    public IReadOnlyList<WallConstructionSet> CandidateSets => MatchingSets;
    public WallConstructionSet? SelectedSet => CandidateCount == 1 ? MatchingSets[0] : null;
    public WallSetSelection PersistedSelection => SelectedSet is { } set ? Selection with { SetId = set.Id } : Selection;
    public string CandidateSummary => SelectedSet is { } set
        ? $"1 wall set · {set.WallPieces.Count:N0} pieces · {set.Details.Count:N0} detailing · {RibbonStatus(set)}"
        : CandidateCount == 0 ? "No wall set matches these choices"
        : $"{CandidateCount:N0} wall sets · narrow the choices until one remains";
    public string Summary => SelectedSet?.DisplayName ?? SelectedValuesSummary();
    public IReadOnlyList<WallComponentAppearance> SelectedComponentAppearances
    {
        get
        {
            if (SelectedWallSystem.Equals(AssetQueries.All, StringComparison.OrdinalIgnoreCase)) return [];
            var components = Components;
            var result = new List<WallComponentAppearance>();
            if (components.ElementAtOrDefault(0) is { Length: > 0 } primary)
                result.Add(new WallComponentAppearance(primary, SelectedPrimaryAppearance));
            if (components.ElementAtOrDefault(1) is { Length: > 0 } secondary)
                result.Add(new WallComponentAppearance(secondary, SelectedSecondaryAppearance));
            return result;
        }
    }

    public WallSetSelection Selection => new()
    {
        SetId = _setId,
        WallSystem = SelectedWallSystem,
        PrimaryAppearance = SelectedPrimaryAppearance,
        SecondaryAppearance = SelectedSecondaryAppearance,
        Theme = SelectedTheme,
        Profile = SelectedProfile,
        Variant = SelectedVariant
    };

    public void Restore(WallSetSelection? selection)
    {
        selection ??= new WallSetSelection();
        _suppress = true;
        _setId = Normalize(selection.SetId);
        _wallSystem = Normalize(selection.WallSystem);
        _primaryAppearance = Normalize(selection.PrimaryAppearance);
        _secondaryAppearance = Normalize(selection.SecondaryAppearance);
        _theme = Normalize(selection.Theme);
        _profile = Normalize(selection.Profile);
        _variant = Normalize(selection.Variant);
        RefreshChoicesCore();
        NotifyAll();
        _suppress = false;
    }

    public void RefreshChoices()
    {
        _suppress = true;
        RefreshChoicesCore();
        NotifyAll();
        _suppress = false;
    }

    public void Reset() => Restore(new WallSetSelection());

    public bool SelectSet(string setId)
    {
        var sourceId = _sourceId();
        var set = _catalog().Sets.FirstOrDefault(candidate =>
            candidate.Id.Equals(setId, StringComparison.OrdinalIgnoreCase)
            && (sourceId.Equals(AssetQueries.All, StringComparison.OrdinalIgnoreCase)
                || candidate.SourceId.Equals(sourceId, StringComparison.OrdinalIgnoreCase)));
        if (set is null) return false;
        _suppress = true;
        _setId = set.Id;
        _wallSystem = set.WallSystem;
        _primaryAppearance = set.PrimaryAppearance;
        _secondaryAppearance = set.SecondaryAppearance;
        _theme = set.Theme;
        _profile = set.Profile;
        _variant = set.Variant;
        RefreshChoicesCore();
        NotifyAll();
        _suppress = false;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void FilterTo(WallSetFacet facet, string value)
    {
        if (facet is not (WallSetFacet.WallSystem or WallSetFacet.PrimaryAppearance or WallSetFacet.SecondaryAppearance
            or WallSetFacet.Theme or WallSetFacet.Profile or WallSetFacet.Variant))
            return;

        // Component appearances, profiles, and variants only have a stable meaning inside a wall system.
        // Keep that system as context; every other facet is deliberately broadened back to All.
        var keepWallSystem = facet != WallSetFacet.WallSystem;
        var selection = new WallSetSelection
        {
            WallSystem = facet == WallSetFacet.WallSystem ? value : keepWallSystem ? SelectedWallSystem : AssetQueries.All,
            PrimaryAppearance = facet == WallSetFacet.PrimaryAppearance ? value : AssetQueries.All,
            SecondaryAppearance = facet == WallSetFacet.SecondaryAppearance ? value : AssetQueries.All,
            Theme = facet == WallSetFacet.Theme ? value : AssetQueries.All,
            Profile = facet == WallSetFacet.Profile ? value : AssetQueries.All,
            Variant = facet == WallSetFacet.Variant ? value : AssetQueries.All
        };

        Restore(selection);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<string> Components => _catalog().ComponentsForSystem(SelectedWallSystem);
    private IReadOnlyList<WallConstructionSet> MatchingSets => _catalog().Filter(Selection, _sourceId());

    private void SetSelection(ref string field, string? value, WallSetFacet changedFacet)
    {
        if (_suppress || !SetProperty(ref field, Normalize(value))) return;
        _suppress = true;
        _setId = AssetQueries.All;
        RefreshChoicesCore(changedFacet);
        NotifyAll();
        _suppress = false;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshChoicesCore(WallSetFacet? protectedFacet = null)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            var selection = Selection;
            var wallSystem = _wallSystem;
            var primary = _primaryAppearance;
            var secondary = _secondaryAppearance;
            var theme = _theme;
            var profile = _profile;
            var variant = _variant;

            Replace(WallSystems, _catalog().Values(selection, WallSetFacet.WallSystem, _sourceId()));
            Replace(PrimaryAppearances, _catalog().Values(selection, WallSetFacet.PrimaryAppearance, _sourceId()));
            Replace(SecondaryAppearances, _catalog().Values(selection, WallSetFacet.SecondaryAppearance, _sourceId()));
            Replace(Themes, _catalog().Values(selection, WallSetFacet.Theme, _sourceId()));
            Replace(Profiles, _catalog().Values(selection, WallSetFacet.Profile, _sourceId()));
            Replace(Variants, _catalog().Values(selection, WallSetFacet.Variant, _sourceId()));

            if (pass > 0 || protectedFacet != WallSetFacet.WallSystem) _wallSystem = Pick(WallSystems, wallSystem);
            if (pass > 0 || protectedFacet != WallSetFacet.PrimaryAppearance) _primaryAppearance = Pick(PrimaryAppearances, primary);
            if (pass > 0 || protectedFacet != WallSetFacet.SecondaryAppearance) _secondaryAppearance = Pick(SecondaryAppearances, secondary);
            if (pass > 0 || protectedFacet != WallSetFacet.Theme) _theme = Pick(Themes, theme);
            if (pass > 0 || protectedFacet != WallSetFacet.Profile) _profile = Pick(Profiles, profile);
            if (pass > 0 || protectedFacet != WallSetFacet.Variant) _variant = Pick(Variants, variant);
        }
    }

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(SelectedWallSystem));
        OnPropertyChanged(nameof(SelectedPrimaryAppearance));
        OnPropertyChanged(nameof(SelectedSecondaryAppearance));
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(SelectedProfile));
        OnPropertyChanged(nameof(SelectedVariant));
        OnPropertyChanged(nameof(PrimaryComponentLabel));
        OnPropertyChanged(nameof(SecondaryComponentLabel));
        OnPropertyChanged(nameof(HasSecondaryComponent));
        OnPropertyChanged(nameof(CandidateCount));
        OnPropertyChanged(nameof(CandidateSummary));
        OnPropertyChanged(nameof(Summary));
    }

    private string SelectedValuesSummary()
    {
        var values = new[] { SelectedWallSystem, SelectedPrimaryAppearance, SelectedSecondaryAppearance, SelectedTheme, SelectedProfile, SelectedVariant }
            .Where(value => !value.Equals(AssetQueries.All, StringComparison.OrdinalIgnoreCase)
                && !value.Equals("Unspecified", StringComparison.OrdinalIgnoreCase));
        var result = string.Join(" > ", values);
        return string.IsNullOrWhiteSpace(result) ? "Choose one wall set" : result;
    }

    private static string RibbonStatus(WallConstructionSet set) => set.AutomaticRibbon.Status switch
    {
        WallRibbonResolutionStatus.Resolved => "ribbon resolved",
        WallRibbonResolutionStatus.Ambiguous => "ribbon ambiguous",
        _ => "ribbon needs mapping"
    };

    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? AssetQueries.All : value;
    private static string Pick(IEnumerable<string> values, string preferred) =>
        values.FirstOrDefault(value => value.Equals(preferred, StringComparison.OrdinalIgnoreCase)) ?? AssetQueries.All;
    private static void Replace(ObservableCollection<string> target, IEnumerable<string> desiredValues)
    {
        var desired = desiredValues.ToList();
        for (var index = target.Count - 1; index >= 0; index--)
            if (!desired.Contains(target[index], StringComparer.OrdinalIgnoreCase)) target.RemoveAt(index);
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
