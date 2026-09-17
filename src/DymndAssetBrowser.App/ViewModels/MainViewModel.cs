using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using DymndAssetBrowser.App.Infrastructure;
using DymndAssetBrowser.App.Services;
using DymndAssetBrowser.Core.Indexing;
using DymndAssetBrowser.Core.Models;
using DymndAssetBrowser.Core.Persistence;

namespace DymndAssetBrowser.App.ViewModels;

public sealed record LibraryChoice(string Id, string Name)
{
    public override string ToString() => Name;
}

public sealed partial class MainViewModel : ViewModelBase
{
    private const string All = AssetQueries.All;
    private const int UnfilteredPreviewLimit = 240;
    private const int TileCacheSoftLimit = 600;
    private static readonly TimeSpan SearchDebounceDelay = TimeSpan.FromMilliseconds(275);
    private readonly ApplicationStateStore _stateStore;
    private readonly AssetLibraryIndexer _indexer = new();
    private readonly ExplorerSelection _selection = new();
    private ImageCacheService _imageCache;
    private ApplicationState _state = new();
    private List<AssetRecord> _assets = [];
    private AssetCatalogIndex _catalog = AssetCatalogIndex.Empty;
    private WallConstructionCatalog _wallCatalog = WallConstructionCatalog.Empty;
    private List<AssetTileViewModel> _visibleTiles = [];
    private readonly ConcurrentDictionary<string, AssetTileViewModel> _tileCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _browserSectionExpansion = new(StringComparer.OrdinalIgnoreCase);
    private LibraryChoice? _selectedLibrary;
    private string _selectedGroup = All, _selectedSubGroup = All, _selectedMaterial = All,
        _selectedStyle = All, _selectedTheme = All, _selectedSourceSet = All, _selectedFilenameVariant = All,
        _selectedCategory = All, _selectedType = All, _selectedSubtype = All, _selectedContext = All, _searchText = string.Empty;
    private AssetTileViewModel? _selectedAsset;
    private string _statusText = "Add or reindex an asset library. Source files remain read-only.";
    private bool _isBusy, _suppressRefresh, _filenameCaseSensitive;
    private string _cspTool = string.Empty, _cspToolGroup = string.Empty, _cspRibbonName = string.Empty;
    private string _cspMappingDescription = "Select or filter to one wall set to inspect its association.";
    private int _refreshGeneration;
    private int _browserColumnCount = 4;
    private CancellationTokenSource? _searchDebounceCancellation;
    private CancellationTokenSource? _refreshCancellation;

    public MainViewModel(ApplicationStateStore? stateStore = null)
    {
        InitializeSourceControls();
        _stateStore = stateStore ?? new ApplicationStateStore();
        _imageCache = new ImageCacheService(_stateStore.CacheRoot);
        WallsBuildSelector = new WallSetSelectorViewModel(() => _wallCatalog, () => SelectedSourceId);
        FloorBuildSelector = new BuildSelectorViewModel("Floor", BuildComponent.Floor, () => _catalog, () => SelectedSourceId);
        TrimBuildSelector = new BuildSelectorViewModel("Trim", BuildComponent.Trim, () => _catalog, () => SelectedSourceId);
        WallsBuildSelector.SelectionChanged += BuildSelector_SelectionChanged;
        FloorBuildSelector.SelectionChanged += BuildSelector_SelectionChanged;
        TrimBuildSelector.SelectionChanged += BuildSelector_SelectionChanged;
        InitializePlannerFilters();
    }

    public ObservableCollection<LibraryChoice> Libraries { get; } = [];
    public ObservableCollection<string> Groups { get; } = [];
    public ObservableCollection<string> SubGroups { get; } = [];
    public ObservableCollection<string> Materials { get; } = [];
    public ObservableCollection<string> Styles { get; } = [];
    public ObservableCollection<string> Themes { get; } = [];
    public ObservableCollection<string> SourceSets { get; } = [];
    public ObservableCollection<string> FilenameVariants { get; } = [];
    public ObservableCollection<string> Categories { get; } = [];
    public ObservableCollection<string> Types { get; } = [];
    public ObservableCollection<string> Subtypes { get; } = [];
    public ObservableCollection<string> Contexts { get; } = [];
    public ObservableCollection<PartSectionViewModel> Sections { get; } = [];
    public BulkObservableCollection<BrowserDisplayRowViewModel> BrowserRows { get; } = [];

    public LibraryChoice? SelectedLibrary
    {
        get => _selectedLibrary;
        set
        {
            if (!SetProperty(ref _selectedLibrary, value) || _suppressRefresh) return;
            OnPropertyChanged(nameof(SelectedSourceRoot));
            OnPropertyChanged(nameof(CanManageSelectedLibrary));
            RebuildFilters(All, All, All, All, All, All, All);
            RebuildSourceFacets();
            RefreshBuildSelectors();
            Refresh();
            if (IsBuildMode && _buildIsPopulated) _ = PopulateBuildAsync(saveState: false);
        }
    }
    public string SelectedSourceRoot => SelectedSource is { } source ? source.RootPath : "All registered libraries";
    public bool CanManageSelectedLibrary => SelectedSource is not null;
    public bool HasRegisteredLibraries => _state.Libraries.Count > 0;
    public string SelectedSourceId => SelectedLibrary?.Id ?? All;
    public AssetLibrarySource? SelectedSource => _state.Libraries.FirstOrDefault(source => source.Id.Equals(SelectedSourceId, StringComparison.OrdinalIgnoreCase));

    public string SelectedGroup { get => _selectedGroup; set { if (SetProperty(ref _selectedGroup, value) && !_suppressRefresh) { RebuildFacets(AssetFacet.Group); Refresh(); } } }
    public string SelectedSubGroup { get => _selectedSubGroup; set { if (SetProperty(ref _selectedSubGroup, value) && !_suppressRefresh) { RebuildFacets(AssetFacet.SubGroup); Refresh(); } } }
    public string SelectedMaterial { get => _selectedMaterial; set { if (SetProperty(ref _selectedMaterial, value) && !_suppressRefresh) { RebuildFacets(AssetFacet.MaterialFacet); Refresh(); } } }
    public string SelectedStyle { get => _selectedStyle; set { if (SetProperty(ref _selectedStyle, value) && !_suppressRefresh) { RebuildFacets(AssetFacet.AppearanceFacet); Refresh(); } } }
    public string SelectedTheme { get => _selectedTheme; set { if (SetProperty(ref _selectedTheme, value) && !_suppressRefresh) { RebuildFacets(AssetFacet.Theme); Refresh(); } } }
    public string SelectedSourceSet { get => _selectedSourceSet; set { if (SetProperty(ref _selectedSourceSet, value) && !_suppressRefresh) { RebuildFacets(AssetFacet.SourceSet); Refresh(); } } }
    public string SelectedFilenameVariant { get => _selectedFilenameVariant; set { if (SetProperty(ref _selectedFilenameVariant, value) && !_suppressRefresh) { RebuildFacets(AssetFacet.FilenameVariant); Refresh(); } } }
    public string SelectedCategory { get => _selectedCategory; set { if (SetProperty(ref _selectedCategory, value) && !_suppressRefresh) { RebuildFacets(AssetFacet.Category); Refresh(); } } }
    public string SelectedType { get => _selectedType; set { if (SetProperty(ref _selectedType, value) && !_suppressRefresh) { RebuildFacets(AssetFacet.Type); Refresh(); } } }
    public string SelectedSubtype { get => _selectedSubtype; set { if (SetProperty(ref _selectedSubtype, value) && !_suppressRefresh) { RebuildFacets(AssetFacet.Subtype); Refresh(); } } }
    public string SelectedContext { get => _selectedContext; set { if (SetProperty(ref _selectedContext, value) && !_suppressRefresh) { RebuildFacets(AssetFacet.Context); Refresh(); } } }
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value ?? string.Empty)) ScheduleSearchRefresh(); } }
    public bool FilenameCaseSensitive { get => _filenameCaseSensitive; set { if (SetProperty(ref _filenameCaseSensitive, value)) Refresh(); } }

    public AssetTileViewModel? SelectedAsset
    {
        get => _selectedAsset;
        private set { if (SetProperty(ref _selectedAsset, value)) { OnPropertyChanged(nameof(SelectedAssetName)); OnPropertyChanged(nameof(SelectedRotation)); } }
    }
    public string SelectedAssetName => _selection.Selected.Count switch { 0 => "No asset selected", 1 => SelectedAsset?.Asset.FileName ?? "1 asset selected", _ => $"{_selection.Selected.Count:N0} assets selected" };
    public string SelectedRotation => SelectedAsset?.TransformLabel ?? "0°";
    public IReadOnlyList<AssetTileViewModel> SelectedAssets => ActiveVisibleTiles().Where(tile => tile.IsSelected).ToList();
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    private bool _isStartupLoading = true;
    public bool IsStartupLoading { get => _isStartupLoading; private set => SetProperty(ref _isStartupLoading, value); }
    public string CspTool { get => _cspTool; set => SetProperty(ref _cspTool, value); }
    public string CspToolGroup { get => _cspToolGroup; set => SetProperty(ref _cspToolGroup, value); }
    public string CspRibbonName { get => _cspRibbonName; set => SetProperty(ref _cspRibbonName, value); }
    public string CspMappingDescription { get => _cspMappingDescription; private set => SetProperty(ref _cspMappingDescription, value); }
    public int IndexedAssetCount => _assets.Count;
    public Dictionary<string, double> StartupTimingsMs { get; } = new();
    public bool IsInitialized { get; private set; }
    public string? StartupError { get; private set; }
    public bool HasSettingsBackup => _stateStore.HasSettingsBackup;
    public string StateDirectory => _stateStore.AppDataRoot;
    public Task RecoverSettingsAsync(bool restoreBackup) => _stateStore.RecoverSettingsAsync(restoreBackup);

    public async Task InitializeAsync()
    {
        IsInitialized = false; StartupError = null;
        var startup = System.Diagnostics.Stopwatch.StartNew();
        var phase = System.Diagnostics.Stopwatch.StartNew();
        IsBusy = true;
        StatusText = "Loading the asset index…";
        IsStartupLoading = true;
        try
        {
            _state = await Task.Run(async () => await _stateStore.LoadAsync());
            StartupTimingsMs["Load SQLite and settings"] = phase.Elapsed.TotalMilliseconds; phase.Restart();
            _imageCache = new ImageCacheService(_state.CacheRoot ?? _stateStore.CacheRoot);
            // Reindexing owns path validation. Keeping an offline library indexed makes startup
            // deterministic and avoids 166k synchronous filesystem probes on every launch.
            _assets = _state.Assets.Select(NormalizeLoadedAsset).ToList();
            _state = _state with { Assets = [] };
            await RebuildCatalogAsync();
            StartupTimingsMs["Build catalogs total"] = phase.Elapsed.TotalMilliseconds; phase.Restart();
            RebuildLibraryChoices(_state.LastSourceId);
            StartupTimingsMs["Legacy browser facets"] = 0;
            StartupTimingsMs["Planner selectors"] = 0;
            RestoreSourceControls();
            StartupTimingsMs["Source browser facets"] = phase.Elapsed.TotalMilliseconds; phase.Restart();
            await Task.Yield();
            await RefreshSectionsAsync();
            StartupTimingsMs["First results"] = phase.Elapsed.TotalMilliseconds;
            StartupTimingsMs["Total"] = startup.Elapsed.TotalMilliseconds;
            StatusText = _assets.Count == 0
                ? "No index loaded. Add or reindex a library to discover image files."
                : $"Loaded {_assets.Count:N0} indexed assets from {_state.Libraries.Count:N0} libraries.";
            IsInitialized = true;
        }
        catch (Exception ex) { StartupError = ex.Message; StatusText = "Could not load saved data. Existing files have not been replaced."; }
        finally { IsBusy = false; IsStartupLoading = false; }
    }

    public async Task AddLibraryAsync(string name, string rootPath)
    {
        var normalizedRoot = AssetIdentity.NormalizePath(rootPath);
        if (!Directory.Exists(normalizedRoot)) { StatusText = "Choose an existing asset directory."; return; }
        if (_state.Libraries.Any(source => PathsEqual(source.RootPath, normalizedRoot))) { StatusText = "That folder is already registered."; return; }
        var source = new AssetLibrarySource { Id = $"library-{Guid.NewGuid():N}", Name = NormalizeLibraryName(name, normalizedRoot), RootPath = normalizedRoot, ParserProfile = LibraryParserProfiles.Generic };
        _state.Libraries.Add(source); RebuildLibraryChoices(source.Id); await SaveStateAsync(); await ReindexLibraryAsync(source);
    }

    public async Task<bool> AddFaLibraryAsync(string selectedPath)
    {
        var rootPath = FaLibraryDetector.ResolveRoot(selectedPath);
        if (rootPath is null)
        {
            StatusText = "That folder does not appear to be an FA _Assets folder. Choose _Assets or its parent folder.";
            return false;
        }

        var existing = _state.Libraries.FirstOrDefault(source =>
            source.Id.Equals(LibrarySourceIds.ForgottenAdventures, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            StatusText = PathsEqual(existing.RootPath, rootPath)
                ? "That Forgotten Adventures library is already registered."
                : $"Forgotten Adventures is already registered at {existing.RootPath}. Remove it before choosing a different copy.";
            return false;
        }

        if (_state.Libraries.Any(source => PathsEqual(source.RootPath, rootPath)))
        {
            StatusText = "That folder is already registered as another library. Remove it before adding it with the FA parser.";
            return false;
        }

        var source = new AssetLibrarySource
        {
            Id = LibrarySourceIds.ForgottenAdventures,
            Name = "Forgotten Adventures",
            RootPath = AssetIdentity.NormalizePath(rootPath),
            ParserProfile = LibraryParserProfiles.Fa
        };
        _state.Libraries.Add(source);
        RebuildLibraryChoices(source.Id);
        await SaveStateAsync();
        await ReindexLibraryAsync(source);
        return true;
    }

    public async Task RenameSelectedLibraryAsync(string name)
    {
        var source = SelectedSource; if (source is null) return;
        var renamed = source with { Name = NormalizeLibraryName(name, source.RootPath) };
        var index = _state.Libraries.FindIndex(item => item.Id.Equals(source.Id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) _state.Libraries[index] = renamed;
        RebuildLibraryChoices(renamed.Id); await SaveStateAsync(); StatusText = $"Library renamed to {renamed.Name}.";
    }

    public async Task RemoveSelectedLibraryAsync()
    {
        var source = SelectedSource; if (source is null) return;
        _state.Libraries.RemoveAll(item => item.Id.Equals(source.Id, StringComparison.OrdinalIgnoreCase));
        _assets.RemoveAll(asset => asset.SourceId.Equals(source.Id, StringComparison.OrdinalIgnoreCase));
        await _stateStore.DeleteSourceAssetsAsync(source.Id);
        await RebuildCatalogAsync();
        _tileCache.Clear(); _selection.Clear(); SelectedAsset = null;
        RebuildLibraryChoices(All); RebuildFilters(All, All, All, All, All, All, All);
        RefreshBuildSelectors(); await RefreshSectionsAsync(); if (IsBuildMode && _buildIsPopulated) await PopulateBuildAsync(saveState: false); await SaveStateAsync(); StatusText = $"Removed {source.Name} from the index. Source files remain unchanged.";
    }

    public Task ReindexSelectedLibraryAsync() => SelectedSource is { } source ? ReindexLibraryAsync(source) : ReindexAllLibrariesAsync();

    public async Task ReindexAllLibrariesAsync()
    {
        if (IsBusy) return;
        var before = _assets.Select(a => a.StableIdentity).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (_state.Libraries.Count == 0) { StatusText = "Add a library first."; return; }
        IsBusy = true;
        try
        {
            var combined = new List<AssetRecord>();
            foreach (var source in _state.Libraries)
            {
                if (!Directory.Exists(source.RootPath)) { combined.AddRange(_assets.Where(asset => asset.SourceId.Equals(source.Id, StringComparison.OrdinalIgnoreCase))); continue; }
                StatusText = $"Reindexing {source.Name}…";
                var parsed = await _indexer.ScanLibraryAsync(source, ProgressFor(source)); combined.AddRange(parsed);
            }
            _assets = SortAssets(combined); await _stateStore.ReplaceAssetsAsync(_assets); await RebuildCatalogAsync(); RecordNewAssets(before); RebuildSourceFacets(); _tileCache.Clear(); RebuildFilters(All, All, All, All, All, All, All); await Task.Yield(); ResetAllTaxonomyFilters();
            RefreshBuildSelectors(); await RefreshSectionsAsync(); if (IsBuildMode && _buildIsPopulated) await PopulateBuildAsync(saveState: false); await SaveStateAsync(); StatusText = $"Reindexed {_assets.Count:N0} assets across {_state.Libraries.Count:N0} libraries. Source files remain unchanged.";
        }
        catch (Exception ex) { StatusText = $"Reindex failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task ReindexLibraryAsync(AssetLibrarySource source)
    {
        if (IsBusy) return;
        var before = _assets.Select(a => a.StableIdentity).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(source.RootPath)) { StatusText = $"Library folder not found: {source.RootPath}"; return; }
        IsBusy = true;
        try
        {
            StatusText = $"Reindexing {source.Name}…";
            var parsed = await _indexer.ScanLibraryAsync(source, ProgressFor(source));
            _assets.RemoveAll(asset => asset.SourceId.Equals(source.Id, StringComparison.OrdinalIgnoreCase));
            _assets.AddRange(parsed); _assets = SortAssets(_assets); _tileCache.Clear();
            await _stateStore.ReplaceSourceAssetsAsync(source.Id, _assets.Where(asset => asset.SourceId.Equals(source.Id, StringComparison.OrdinalIgnoreCase)));
            await RebuildCatalogAsync();
            RecordNewAssets(before); RebuildSourceFacets();
            RebuildFilters(All, All, All, All, All, All, All); await Task.Yield(); ResetAllTaxonomyFilters();
            RefreshBuildSelectors(); await RefreshSectionsAsync(); if (IsBuildMode && _buildIsPopulated) await PopulateBuildAsync(saveState: false); await SaveStateAsync(); StatusText = $"Indexed {parsed.Count:N0} assets from {source.Name}. Source files remain unchanged.";
        }
        catch (Exception ex) { StatusText = $"Reindex failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    public void SelectAsset(AssetTileViewModel asset, bool control = false, bool shift = false, bool rightClick = false)
    {
        if (!rightClick || !_allResultsSelected) _allResultsSelected = false;
        var visibleTiles = ActiveVisibleTiles();
        _selection.Select(asset.Asset.StableIdentity, visibleTiles.Select(tile => tile.Asset.StableIdentity).ToList(), control, shift, rightClick);
        foreach (var tile in visibleTiles) tile.IsSelected = _selection.Selected.Contains(tile.Asset.StableIdentity, StringComparer.OrdinalIgnoreCase);
        SelectedAsset = asset.IsSelected ? asset : visibleTiles.LastOrDefault(tile => tile.IsSelected);
        OnPropertyChanged(nameof(SelectedAssetName)); OnPropertyChanged(nameof(SelectedAssets));
        OnPropertyChanged(nameof(SelectionSummary));
        LoadMapping();
    }

    public void RotateSelected(int delta)
    {
        if (SelectedAsset is null) return;
        SelectedAsset.Rotate(delta); OnPropertyChanged(nameof(SelectedRotation)); StatusText = $"{SelectedAsset.Asset.FileName} rotated to {SelectedAsset.RotationLabel}."; _ = WarmRotationCacheAsync(SelectedAsset);
    }

    public void FlipSelectedHorizontally()
    {
        if (SelectedAsset is null) return;
        SelectedAsset.FlipHorizontally(); OnPropertyChanged(nameof(SelectedRotation));
        StatusText = $"{SelectedAsset.Asset.FileName} horizontal flip {(SelectedAsset.IsFlippedHorizontally ? "on" : "off")}.";
        _ = WarmRotationCacheAsync(SelectedAsset);
    }

    private bool _randomRotationEnabled;
    public bool RandomRotationEnabled { get => _randomRotationEnabled; set => SetProperty(ref _randomRotationEnabled, value); }
    public ImageCacheService ImageCache => _imageCache;

    public async Task<string?> GetDragFileAsync(AssetTileViewModel asset)
    {
        SelectedAsset = asset;
        // Visual placement randomness is not a security-sensitive value.
#pragma warning disable CA5394
        var angle = RandomRotationEnabled ? Random.Shared.Next(8) * 45 : asset.RotationAngle;
#pragma warning restore CA5394
        try { StatusText = "Preparing drag image…"; var path = await _imageCache.GetDragFileAsync(asset.Asset.FilePath, angle, asset.IsFlippedHorizontally); StatusText = $"Dragging {Path.GetFileName(path)} at {angle}°. Source remains unchanged."; return path; }
        catch (Exception ex) { StatusText = $"Could not prepare drag file: {ex.Message}"; return null; }
    }

    public Task EnsureThumbnailAsync(AssetTileViewModel asset) => asset.EnsureThumbnailAsync(_imageCache);
    public static void ReleaseThumbnail(AssetTileViewModel asset) => asset.ReleaseThumbnail();

    public async Task SaveMappingAsync()
    {
        var set = CurrentBrowserWallSet();
        var key = set?.Id ?? CurrentFamilyKey();
        _state.RibbonMappings[key] = new RibbonMapping { FamilyKey = key, CspTool = CspTool.Trim(), CspToolGroup = CspToolGroup.Trim(), CspRibbonName = CspRibbonName.Trim(), IsManuallyConfirmed = true };
        await SaveStateAsync(); StatusText = set is null ? "CSP ribbon mapping saved." : "CSP ribbon mapping saved for this complete wall set.";
    }

    public Task SaveStateAsync()
    {
        if (!IsInitialized) return Task.CompletedTask;
        _state = _state with { SchemaVersion = 6, Assets = _assets,
            LastSourceId = SelectedSourceId, LastGroup = SelectedGroup, LastSubGroup = SelectedSubGroup, LastMaterial = SelectedMaterial,
            LastStyle = SelectedStyle, LastTheme = SelectedTheme, LastSourceSet = SelectedSourceSet,
            LastFilenameVariant = SelectedFilenameVariant, CacheRoot = _stateStore.CacheRoot,
            LastCategory = SelectedCategory, LastType = SelectedType, LastSubtype = SelectedSubtype, LastContext = SelectedContext,
            BuildRecipe = CurrentBuildRecipe(), BrowserFilter = SourceFilter() with { Identities = null }, ThumbnailSize = ThumbnailSize };
        return _stateStore.SaveMetadataAsync(_state with { UserTags = new(_state.UserTags, StringComparer.OrdinalIgnoreCase) });
    }

    private void RebuildLibraryChoices(string? preferredId)
    {
        _suppressRefresh = true; Libraries.Clear(); Libraries.Add(new LibraryChoice(All, "All Libraries"));
        foreach (var source in _state.Libraries.OrderBy(source => source.Name, StringComparer.OrdinalIgnoreCase)) Libraries.Add(new LibraryChoice(source.Id, source.Name));
        _selectedLibrary = Libraries.FirstOrDefault(choice => choice.Id.Equals(preferredId, StringComparison.OrdinalIgnoreCase)) ?? Libraries[0];
        OnPropertyChanged(nameof(SelectedLibrary)); OnPropertyChanged(nameof(SelectedSourceRoot)); OnPropertyChanged(nameof(CanManageSelectedLibrary)); OnPropertyChanged(nameof(HasRegisteredLibraries)); _suppressRefresh = false;
    }

    private void RebuildFilters(string? group, string? subGroup, string? material, string? style, string? theme, string? sourceSet, string? filenameVariant,
        string? category = null, string? type = null, string? context = null, string? subtype = null)
    {
        _suppressRefresh = true;
        _selectedGroup = NormalizeSelection(group); _selectedSubGroup = NormalizeSelection(subGroup); _selectedMaterial = NormalizeSelection(material);
        _selectedStyle = NormalizeSelection(style); _selectedTheme = NormalizeSelection(theme); _selectedSourceSet = NormalizeSelection(sourceSet); _selectedFilenameVariant = NormalizeSelection(filenameVariant);
        _selectedCategory = NormalizeSelection(category); _selectedType = NormalizeSelection(type); _selectedSubtype = NormalizeSelection(subtype); _selectedContext = NormalizeSelection(context);
        _suppressRefresh = false; LoadMapping();
    }

    private void RebuildFacets(AssetFacet? protectedFacet = null) { _suppressRefresh = true; RebuildFacetsCore(protectedFacet); _suppressRefresh = false; }

    private void RebuildFacetsCore(AssetFacet? protectedFacet)
    {
        var filter = CurrentFilter();
        var group = _selectedGroup; var subGroup = _selectedSubGroup; var material = _selectedMaterial; var style = _selectedStyle;
        var theme = _selectedTheme; var sourceSet = _selectedSourceSet; var filenameVariant = _selectedFilenameVariant;
        var category = _selectedCategory; var type = _selectedType; var subtype = _selectedSubtype; var context = _selectedContext;
        Replace(Groups, _catalog.ValuesForFacet(filter, AssetFacet.Group));
        Replace(SubGroups, _catalog.ValuesForFacet(filter, AssetFacet.SubGroup));
        Replace(Materials, _catalog.ValuesForFacet(filter, AssetFacet.MaterialFacet));
        Replace(Styles, _catalog.ValuesForFacet(filter, AssetFacet.AppearanceFacet));
        Replace(Themes, _catalog.ValuesForFacet(filter, AssetFacet.Theme));
        Replace(SourceSets, _catalog.ValuesForFacet(filter, AssetFacet.SourceSet));
        Replace(FilenameVariants, _catalog.ValuesForFacet(filter, AssetFacet.FilenameVariant));
        Replace(Categories, _catalog.ValuesForFacet(filter, AssetFacet.Category));
        Replace(Types, _catalog.ValuesForFacet(filter, AssetFacet.Type));
        Replace(Subtypes, _catalog.ValuesForFacet(filter, AssetFacet.Subtype));
        Replace(Contexts, _catalog.ValuesForFacet(filter, AssetFacet.Context));
        _selectedGroup = Pick(Groups, group); _selectedSubGroup = Pick(SubGroups, subGroup); _selectedMaterial = Pick(Materials, material);
        _selectedStyle = Pick(Styles, style); _selectedTheme = Pick(Themes, theme); _selectedSourceSet = Pick(SourceSets, sourceSet); _selectedFilenameVariant = Pick(FilenameVariants, filenameVariant);
        _selectedCategory = Pick(Categories, category); _selectedType = Pick(Types, type); _selectedSubtype = Pick(Subtypes, subtype); _selectedContext = Pick(Contexts, context);
        OnPropertyChanged(nameof(SelectedGroup)); OnPropertyChanged(nameof(SelectedSubGroup)); OnPropertyChanged(nameof(SelectedMaterial)); OnPropertyChanged(nameof(SelectedStyle));
        OnPropertyChanged(nameof(SelectedTheme)); OnPropertyChanged(nameof(SelectedSourceSet)); OnPropertyChanged(nameof(SelectedFilenameVariant));
        OnPropertyChanged(nameof(SelectedCategory)); OnPropertyChanged(nameof(SelectedType)); OnPropertyChanged(nameof(SelectedSubtype)); OnPropertyChanged(nameof(SelectedContext));
    }

    public void ResetFilters()
    {
        ResetSourceFilters();
        _searchDebounceCancellation?.Cancel(); _searchText = string.Empty; OnPropertyChanged(nameof(SearchText));
        ResetAllTaxonomyFilters(); Refresh();
    }

    public void SetBrowserViewportWidth(double width)
    {
        if (width <= 0) return;
        _viewportWidth = width;
        var columns = Math.Max(1, (int)Math.Floor((width - 16) / (TileWidth + 9)));
        if (columns == _browserColumnCount) return;
        _browserColumnCount = columns;
        RebuildBrowserRows();
    }

    public void ToggleBrowserSection(string sectionName)
    {
        var section = Sections.FirstOrDefault(s => s.Name == sectionName);
        var header = BrowserRows.FirstOrDefault(r => r.IsHeader && r.Name == sectionName);
        if (section is null || header is null) return;
        var expanded = !IsBrowserSectionExpanded(sectionName);
        _browserSectionExpansion[sectionName] = expanded;
        var index = BrowserRows.IndexOf(header) + 1;
        // Keep every existing header/container alive. A collection Reset loses the
        // virtualizing panel's height estimates and the clicked heading's scroll anchor.
        if (expanded)
        {
            foreach (var row in section.Assets.Chunk(_browserColumnCount))
                BrowserRows.Insert(index++, BrowserDisplayRowViewModel.AssetRow(row));
        }
        else
        {
            while (index < BrowserRows.Count && !BrowserRows[index].IsHeader) BrowserRows.RemoveAt(index);
        }
        header.SetCollapsed(!expanded);
        SynchronizeBrowserTiles();
    }

    public void SetAllBrowserSectionsExpanded(bool expanded)
    {
        if (!Sections.Any(s => IsBrowserSectionExpanded(s.Name) != expanded)) return;
        foreach (var section in Sections) _browserSectionExpansion[section.Name] = expanded;
        RebuildBrowserRows();
    }

    private bool IsBrowserSectionExpanded(string name) => _browserSectionExpansion.TryGetValue(name, out var expanded) ? expanded : Sections.Count == 1;

    private void ResetAllTaxonomyFilters()
    {
        _suppressRefresh = true; _selectedGroup = All; _selectedSubGroup = All; _selectedMaterial = All; _selectedStyle = All; _selectedTheme = All; _selectedSourceSet = All; _selectedFilenameVariant = All;
        _selectedCategory = All; _selectedType = All; _selectedSubtype = All; _selectedContext = All;
        OnPropertyChanged(nameof(SelectedGroup)); OnPropertyChanged(nameof(SelectedSubGroup)); OnPropertyChanged(nameof(SelectedMaterial)); OnPropertyChanged(nameof(SelectedStyle)); OnPropertyChanged(nameof(SelectedTheme)); OnPropertyChanged(nameof(SelectedSourceSet)); OnPropertyChanged(nameof(SelectedFilenameVariant));
        OnPropertyChanged(nameof(SelectedCategory)); OnPropertyChanged(nameof(SelectedType)); OnPropertyChanged(nameof(SelectedSubtype)); OnPropertyChanged(nameof(SelectedContext));
        _suppressRefresh = false; LoadMapping();
    }

    private void Refresh() { _searchDebounceCancellation?.Cancel(); _ = RefreshSectionsAsync(); LoadMapping(); }
    private void ScheduleSearchRefresh() { _searchDebounceCancellation?.Cancel(); var cancellation = new CancellationTokenSource(); _searchDebounceCancellation = cancellation; _ = DebouncedSearchRefreshAsync(cancellation); }
    private async Task DebouncedSearchRefreshAsync(CancellationTokenSource cancellation)
    {
        try { await Task.Delay(SearchDebounceDelay, cancellation.Token); if (!cancellation.IsCancellationRequested) { await RefreshSectionsAsync(); } }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally { if (ReferenceEquals(_searchDebounceCancellation, cancellation)) _searchDebounceCancellation = null; cancellation.Dispose(); }
    }

    private async Task RefreshSectionsAsync()
    {
        var cancellation = new CancellationTokenSource(); var previous = Interlocked.Exchange(ref _refreshCancellation, cancellation); previous?.Cancel(); previous?.Dispose();
        var token = cancellation.Token; var generation = ++_refreshGeneration; var filter = SourceFilter(); var browser = _sourceBrowser;
        try
        {
            var entries = await Task.Run(() => browser.Filter(filter, token), token); token.ThrowIfCancellationRequested();
            var matching = entries.Select(e => e.Asset).ToList();
            // Group the entire result set, never a truncated preview. Tile models are
            // created only when a section expands; image decoding remains viewport-lazy.
            var groups = await Task.Run(() => entries.GroupBy(e => e.Taxonomy.BrowserPath)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new RefreshGroup(g.Key.Length > 0 ? g.Key : "Library root", g.Select(e => e.Asset).ToList())).ToList(), token);
            var sections = groups.Select(group => new PartSectionViewModel(group.Name, group.Assets.Count,
                () => group.Assets.Select(CreateBrowserTile).ToList())).ToList();
            token.ThrowIfCancellationRequested(); if (generation != _refreshGeneration) return;
            _matchingAssets = matching; _allResultsSelected = false;
            OnPropertyChanged(nameof(MatchingAssetCount));
            OnPropertyChanged(nameof(SelectionSummary));
            Sections.Clear(); foreach (var section in sections) Sections.Add(section);
            RebuildBrowserRows();
            StatusText = $"{matching.Count:N0} matching assets across {sections.Count:N0} sections. Expand a section to browse; thumbnails load as you scroll.";
            OnPropertyChanged(nameof(SelectedAssetName));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally { if (ReferenceEquals(Interlocked.CompareExchange(ref _refreshCancellation, null, cancellation), cancellation)) cancellation.Dispose(); }
    }

    private static RefreshPlan BuildRefreshPlan(AssetCatalogIndex catalog, List<AssetRecord> assets, AssetFilter filter, CancellationToken token)
    {
        var matching = catalog.Filter(filter, token); token.ThrowIfCancellationRequested();
        // The landing view remains bounded, but once the user deliberately filters or searches,
        // virtualization—not result truncation—controls UI cost. Every match stays reachable.
        var primary = HasDeliberateFilter(filter) ? matching : matching.Take(UnfilteredPreviewLimit).ToList();
        var related = AssetQueries.RelatedBuildingPieces(assets, filter, token).Take(240).ToList(); token.ThrowIfCancellationRequested();
        var groups = BuildDisplayGroups(primary, filter).Select(group => new RefreshGroup(group.Name, group.Assets)).ToList(); if (related.Count > 0) groups.Add(new RefreshGroup("Related Building Pieces", related));
        return new RefreshPlan(matching.Count, primary.Count, groups);
    }

    private static bool HasDeliberateFilter(AssetFilter filter) =>
        !string.IsNullOrWhiteSpace(filter.FilenameQuery)
        || filter.Group != All
        || filter.SubGroup != All
        || filter.Material != All
        || filter.Style != All
        || filter.Theme != All
        || filter.Family != All
        || filter.Variant != All
        || filter.Category != All
        || filter.Type != All
        || filter.Subtype != All
        || filter.Context != All
        || filter.MaterialFacet != All
        || filter.AppearanceFacet != All;

    private static IEnumerable<(string Name, List<AssetRecord> Assets)> BuildDisplayGroups(List<AssetRecord> assets, AssetFilter filter)
    {
        var grouped = assets.GroupBy(asset => AssetTypeHierarchy.DisplayPath(
            AssetSemanticClassifier.Classify(asset), filter.Type, filter.Subtype));
        foreach (var group in grouped.OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)) yield return (group.Key, group.ToList());
    }

    private Task<AssetTileViewModel?> CreateTileAsync(AssetRecord asset, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var tile = _tileCache.GetOrAdd(asset.StableIdentity, _ => new AssetTileViewModel(asset));
        tile.UpdateAsset(asset);
        return Task.FromResult<AssetTileViewModel?>(tile);
    }

    private AssetTileViewModel CreateBrowserTile(AssetRecord asset)
    {
        var tile = _tileCache.GetOrAdd(asset.StableIdentity, _ => new AssetTileViewModel(asset));
        tile.UpdateAsset(asset);
        return tile;
    }

    private void PruneTileCache(IEnumerable<AssetTileViewModel> visibleTiles)
    {
        if (_tileCache.Count <= TileCacheSoftLimit) return;
        var visible = visibleTiles.Select(tile => tile.Asset.StableIdentity).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in _tileCache.Keys.Where(key => !visible.Contains(key)).Take(_tileCache.Count - TileCacheSoftLimit).ToList())
            _tileCache.TryRemove(key, out _);
    }

    private void RebuildBrowserRows()
    {
        var rows = new List<BrowserDisplayRowViewModel>();
        foreach (var section in Sections)
        {
            var collapsed = !IsBrowserSectionExpanded(section.Name);
            rows.Add(BrowserDisplayRowViewModel.Header(section.Name, collapsed, section.Count));
            if (collapsed) continue;
            foreach (var row in section.Assets.Chunk(_browserColumnCount))
                rows.Add(BrowserDisplayRowViewModel.AssetRow(row));
        }
        BrowserRows.ReplaceAll(rows);
        SynchronizeBrowserTiles();
    }

    private void SynchronizeBrowserTiles()
    {
        var tiles = Sections.Where(s => IsBrowserSectionExpanded(s.Name)).SelectMany(s => s.Assets).ToList();
        var retained = tiles.ToHashSet();
        foreach (var tile in _visibleTiles.Where(t => !retained.Contains(t))) { ReleaseThumbnail(tile); tile.IsSelected = false; }
        _visibleTiles = tiles;
        _selection.Retain(tiles.Select(tile => tile.Asset.StableIdentity));
        foreach (var tile in tiles) tile.IsSelected = _allResultsSelected || _selection.Selected.Contains(tile.Asset.StableIdentity, StringComparer.OrdinalIgnoreCase);
        SelectedAsset = tiles.LastOrDefault(tile => tile.IsSelected);
        PruneTileCache(tiles.Concat(_buildVisibleTiles));
        OnPropertyChanged(nameof(SelectedAssetName)); OnPropertyChanged(nameof(SelectedAssets)); OnPropertyChanged(nameof(SelectionSummary));
    }

    private IProgress<IndexProgress> ProgressFor(AssetLibrarySource source) => new Progress<IndexProgress>(progress => StatusText = progress.Indexed == 0 ? $"{source.Name}: discovered {progress.Discovered:N0} assets…" : $"{source.Name}: classified {progress.Indexed:N0} of {progress.Discovered:N0} assets…");
    private sealed record RefreshGroup(string Name, List<AssetRecord> Assets);
    private sealed record RefreshPlan(int MatchingCount, int PrimaryCount, List<RefreshGroup> Groups);
    private static AssetRecord NormalizeLoadedAsset(AssetRecord asset)
    {
        var relativePath = string.IsNullOrWhiteSpace(asset.RelativePath) ? Path.GetRelativePath(asset.SourceRoot, asset.FilePath) : asset.RelativePath;
        return relativePath == asset.RelativePath
            ? asset
            : asset with { RelativePath = relativePath };
    }
    private AssetFilter CurrentFilter() => new(Group: SelectedGroup, SubGroup: SelectedSubGroup, Theme: SelectedTheme,
        FilenameQuery: SearchText, FilenameCaseSensitive: FilenameCaseSensitive, SourceId: SelectedSourceId,
        Category: SelectedCategory, Type: SelectedType, Subtype: SelectedSubtype, Context: SelectedContext,
        MaterialFacet: SelectedMaterial, AppearanceFacet: SelectedStyle, SourceSet: SelectedSourceSet,
        FilenameVariant: SelectedFilenameVariant);
    private string CurrentFamilyKey()
    {
        if (SelectedAsset is not null) return SelectedAsset.Asset.FamilyKey;
        var matches = _matchingAssets;
        var family = UniqueValue(matches.Select(asset => asset.Family));
        var variant = UniqueValue(matches.Select(asset => asset.Variant));
        return AssetFamilyKey.Create(SelectedGroup, SelectedSubGroup, SelectedMaterial, SelectedStyle, SelectedTheme, family, variant);
    }
    private void LoadMapping()
    {
        _state.RibbonMappings.TryGetValue(CurrentFamilyKey(), out var mapping);
        var set = CurrentBrowserWallSet();
        if (set is not null)
        {
            if (_state.RibbonMappings.TryGetValue(set.Id, out var setMapping)) mapping = setMapping;
            else if (mapping is null && set.AutomaticRibbon.Status == WallRibbonResolutionStatus.Resolved) mapping = set.AutomaticRibbon.Mapping;
            CspMappingDescription = mapping is null
                ? "One wall set selected · ribbon association needs confirmation."
                : $"One wall set selected · {(mapping.IsManuallyConfirmed ? "manually confirmed" : "automatic")} association for {set.DisplayName}.";
        }
        else CspMappingDescription = "Select or filter to one wall set to inspect its association.";
        CspTool = mapping?.CspTool ?? string.Empty; CspToolGroup = mapping?.CspToolGroup ?? string.Empty; CspRibbonName = mapping?.CspRibbonName ?? string.Empty;
    }
    private WallConstructionSet? CurrentBrowserWallSet()
    {
        if (SelectedAsset?.Asset is { } selected && selected.Group.Equals("Building", StringComparison.OrdinalIgnoreCase)
            && selected.SubGroup.Equals("Walls", StringComparison.OrdinalIgnoreCase))
        {
            var selectedAssetSets = _wallCatalog.Sets
                .Where(set => set.WallPieces.Any(asset => asset.StableIdentity.Equals(selected.StableIdentity, StringComparison.OrdinalIgnoreCase)))
                .Take(2).ToList();
            return selectedAssetSets.Count == 1 ? selectedAssetSets[0] : null;
        }
        if (_matchingAssets.Count == 0 || _matchingAssets.Any(a => a.Group != "Building" || a.SubGroup != "Walls")) return null;
        var identities = _matchingAssets.Select(asset => asset.StableIdentity).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = _wallCatalog.Sets.Where(set => set.WallPieces.Any(asset => identities.Contains(asset.StableIdentity))).Take(2).ToList();
        return candidates.Count == 1 ? candidates[0] : null;
    }
    private async Task WarmRotationCacheAsync(AssetTileViewModel asset) { if (asset.RotationAngle == 0 && !asset.IsFlippedHorizontally) return; try { await _imageCache.GetDragFileAsync(asset.Asset.FilePath, asset.RotationAngle, asset.IsFlippedHorizontally); } catch { } }
    private static List<AssetRecord> SortAssets(IEnumerable<AssetRecord> assets) => assets.OrderBy(asset => asset.Group, StringComparer.OrdinalIgnoreCase).ThenBy(asset => asset.SubGroup, StringComparer.OrdinalIgnoreCase).ThenBy(asset => asset.Material, StringComparer.OrdinalIgnoreCase).ThenBy(asset => asset.Style, StringComparer.OrdinalIgnoreCase).ThenBy(asset => asset.Theme, StringComparer.OrdinalIgnoreCase).ThenBy(asset => asset.Family, StringComparer.OrdinalIgnoreCase).ThenBy(asset => asset.Variant, StringComparer.OrdinalIgnoreCase).ThenBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase).ToList();
    private static string Pick(IEnumerable<string> values, string? preferred) => values.FirstOrDefault(value => value.Equals(preferred, StringComparison.OrdinalIgnoreCase)) ?? All;
    private static string UniqueValue(IEnumerable<string> values)
    {
        var distinct = values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToList();
        return distinct.Count == 1 ? distinct[0] : All;
    }
    private static void Replace(ObservableCollection<string> target, IEnumerable<string> values) { target.Clear(); foreach (var value in values) target.Add(value); }
    private static string NormalizeSelection(string? value) => string.IsNullOrWhiteSpace(value) ? All : value;
    private static string NormalizeLibraryName(string? value, string root) => string.IsNullOrWhiteSpace(value) ? Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) : value.Trim();
    private static bool PathsEqual(string left, string right) => string.Equals(AssetIdentity.NormalizePath(left), AssetIdentity.NormalizePath(right), StringComparison.OrdinalIgnoreCase);
    private async Task RebuildCatalogAsync()
    {
        var restorePlanner = _plannerReady;
        if (restorePlanner) _state = _state with { BuildRecipe = CurrentBuildRecipe() };
        _plannerReady = false;
        var snapshot = _assets.ToArray();
        var wallTask = Task.Run(() => { var t = System.Diagnostics.Stopwatch.StartNew(); var v = WallConstructionCatalog.Build(snapshot); return (Value: v, Ms: t.Elapsed.TotalMilliseconds); });
        var sourceTask = Task.Run(() => { var t = System.Diagnostics.Stopwatch.StartNew(); var v = new SourceBrowserIndex(snapshot, _state.Libraries, _state.UserTags); return (Value: v, Ms: t.Elapsed.TotalMilliseconds); });
        await Task.WhenAll(wallTask, sourceTask);
        _wallCatalog = wallTask.Result.Value;
        _sourceBrowser = sourceTask.Result.Value;
        StartupTimingsMs["Wall catalog"] = wallTask.Result.Ms;
        StartupTimingsMs["Source projection"] = sourceTask.Result.Ms;
        StartupTimingsMs["Semantic catalog"] = 0;
        _catalog = AssetCatalogIndex.Empty;
        if (restorePlanner) await EnsurePlannerReadyAsync();
    }
}
