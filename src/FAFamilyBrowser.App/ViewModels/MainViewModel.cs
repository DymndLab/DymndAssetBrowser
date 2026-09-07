using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using FAFamilyBrowser.App.Infrastructure;
using FAFamilyBrowser.App.Services;
using FAFamilyBrowser.Core.Indexing;
using FAFamilyBrowser.Core.Models;
using FAFamilyBrowser.Core.Persistence;

namespace FAFamilyBrowser.App.ViewModels;

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
    private readonly HashSet<string> _collapsedBrowserSections = new(StringComparer.OrdinalIgnoreCase);
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
        _stateStore = stateStore ?? new ApplicationStateStore();
        _imageCache = new ImageCacheService(_stateStore.CacheRoot);
        WallsBuildSelector = new WallSetSelectorViewModel(() => _wallCatalog, () => SelectedSourceId);
        FloorBuildSelector = new BuildSelectorViewModel("Floor", BuildComponent.Floor, () => _catalog, () => SelectedSourceId);
        TrimBuildSelector = new BuildSelectorViewModel("Trim", BuildComponent.Trim, () => _catalog, () => SelectedSourceId);
        WallsBuildSelector.SelectionChanged += BuildSelector_SelectionChanged;
        FloorBuildSelector.SelectionChanged += BuildSelector_SelectionChanged;
        TrimBuildSelector.SelectionChanged += BuildSelector_SelectionChanged;
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
    public bool FilenameCaseSensitive { get => _filenameCaseSensitive; set { if (SetProperty(ref _filenameCaseSensitive, value)) { RebuildFacets(); Refresh(); } } }

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
    public string CspTool { get => _cspTool; set => SetProperty(ref _cspTool, value); }
    public string CspToolGroup { get => _cspToolGroup; set => SetProperty(ref _cspToolGroup, value); }
    public string CspRibbonName { get => _cspRibbonName; set => SetProperty(ref _cspRibbonName, value); }
    public string CspMappingDescription { get => _cspMappingDescription; private set => SetProperty(ref _cspMappingDescription, value); }
    public int IndexedAssetCount => _assets.Count;

    public async Task InitializeAsync()
    {
        IsBusy = true;
        StatusText = "Loading the asset index…";
        try
        {
            _state = await Task.Run(async () => await _stateStore.LoadAsync());
            _imageCache = new ImageCacheService(_state.CacheRoot ?? _stateStore.CacheRoot);
            // Reindexing owns path validation. Keeping an offline library indexed makes startup
            // deterministic and avoids 166k synchronous filesystem probes on every launch.
            _assets = _state.Assets.Select(NormalizeLoadedAsset).ToList();
            _state = _state with { Assets = [] };
            await RebuildCatalogAsync();
            RebuildLibraryChoices(_state.LastSourceId);
            RebuildFilters(All, All, _state.LastMaterial, _state.LastStyle, _state.LastTheme, _state.LastSourceSet, _state.LastFilenameVariant,
                _state.LastCategory, _state.LastType, _state.LastContext, _state.LastSubtype);
            RestoreBuildRecipe(_state.BuildRecipe);
            await Task.Yield();
            if (_assets.Count > 0 && !_catalog.Any(CurrentFilter())) ResetAllTaxonomyFilters();
            await RefreshSectionsAsync();
            StatusText = _assets.Count == 0
                ? "No index loaded. Add or reindex a library to discover image files."
                : $"Loaded {_assets.Count:N0} indexed assets from {_state.Libraries.Count:N0} libraries.";
        }
        finally { IsBusy = false; }
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
            _assets = SortAssets(combined); await _stateStore.ReplaceAssetsAsync(_assets); await RebuildCatalogAsync(); _tileCache.Clear(); RebuildFilters(All, All, All, All, All, All, All); await Task.Yield(); ResetAllTaxonomyFilters();
            RefreshBuildSelectors(); await RefreshSectionsAsync(); if (IsBuildMode && _buildIsPopulated) await PopulateBuildAsync(saveState: false); await SaveStateAsync(); StatusText = $"Reindexed {_assets.Count:N0} assets across {_state.Libraries.Count:N0} libraries. Source files remain unchanged.";
        }
        catch (Exception ex) { StatusText = $"Reindex failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task ReindexLibraryAsync(AssetLibrarySource source)
    {
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
            RebuildFilters(All, All, All, All, All, All, All); await Task.Yield(); ResetAllTaxonomyFilters();
            RefreshBuildSelectors(); await RefreshSectionsAsync(); if (IsBuildMode && _buildIsPopulated) await PopulateBuildAsync(saveState: false); await SaveStateAsync(); StatusText = $"Indexed {parsed.Count:N0} assets from {source.Name}. Source files remain unchanged.";
        }
        catch (Exception ex) { StatusText = $"Reindex failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    public void SelectAsset(AssetTileViewModel asset, bool control = false, bool shift = false, bool rightClick = false)
    {
        var visibleTiles = ActiveVisibleTiles();
        _selection.Select(asset.Asset.StableIdentity, visibleTiles.Select(tile => tile.Asset.StableIdentity).ToList(), control, shift, rightClick);
        foreach (var tile in visibleTiles) tile.IsSelected = _selection.Selected.Contains(tile.Asset.StableIdentity, StringComparer.OrdinalIgnoreCase);
        SelectedAsset = asset.IsSelected ? asset : visibleTiles.LastOrDefault(tile => tile.IsSelected);
        OnPropertyChanged(nameof(SelectedAssetName)); OnPropertyChanged(nameof(SelectedAssets));
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

    public async Task<string?> GetDragFileAsync(AssetTileViewModel asset)
    {
        SelectedAsset = asset;
        try { StatusText = asset.RotationAngle == 0 && !asset.IsFlippedHorizontally ? "Dragging original source image…" : "Preparing transformed PNG cache…"; var path = await _imageCache.GetDragFileAsync(asset.Asset.FilePath, asset.RotationAngle, asset.IsFlippedHorizontally); StatusText = $"Dragging {Path.GetFileName(path)}. Source remains unchanged."; return path; }
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
        _state = _state with { SchemaVersion = 6, Assets = _assets,
            LastSourceId = SelectedSourceId, LastGroup = SelectedGroup, LastSubGroup = SelectedSubGroup, LastMaterial = SelectedMaterial,
            LastStyle = SelectedStyle, LastTheme = SelectedTheme, LastSourceSet = SelectedSourceSet,
            LastFilenameVariant = SelectedFilenameVariant, CacheRoot = _stateStore.CacheRoot,
            LastCategory = SelectedCategory, LastType = SelectedType, LastSubtype = SelectedSubtype, LastContext = SelectedContext,
            BuildRecipe = CurrentBuildRecipe() };
        return _stateStore.SaveMetadataAsync(_state);
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
        RebuildFacetsCore(null); _suppressRefresh = false; LoadMapping();
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
        _searchDebounceCancellation?.Cancel(); _searchText = string.Empty; OnPropertyChanged(nameof(SearchText));
        ResetAllTaxonomyFilters(); RebuildFacets(); Refresh();
    }

    public void SetBrowserViewportWidth(double width)
    {
        if (width <= 0) return;
        var columns = Math.Max(1, (int)Math.Floor((width - 16) / 259));
        if (columns == _browserColumnCount) return;
        _browserColumnCount = columns;
        RebuildBrowserRows();
    }

    public void ToggleBrowserSection(string sectionName)
    {
        if (!_collapsedBrowserSections.Add(sectionName)) _collapsedBrowserSections.Remove(sectionName);
        RebuildBrowserRows();
    }

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
        try { await Task.Delay(SearchDebounceDelay, cancellation.Token); if (!cancellation.IsCancellationRequested) { RebuildFacets(); await RefreshSectionsAsync(); } }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally { if (ReferenceEquals(_searchDebounceCancellation, cancellation)) _searchDebounceCancellation = null; cancellation.Dispose(); }
    }

    private async Task RefreshSectionsAsync()
    {
        var cancellation = new CancellationTokenSource(); var previous = Interlocked.Exchange(ref _refreshCancellation, cancellation); previous?.Cancel(); previous?.Dispose();
        var token = cancellation.Token; var generation = ++_refreshGeneration; var filter = CurrentFilter(); var catalog = _catalog; var assets = _assets;
        try
        {
            var plan = await Task.Run(() => BuildRefreshPlan(catalog, assets, filter, token), token); token.ThrowIfCancellationRequested();
            var sections = new List<PartSectionViewModel>(); var tiles = new List<AssetTileViewModel>();
            foreach (var group in plan.Groups)
            {
                var groupTiles = new List<AssetTileViewModel>();
                foreach (var batch in group.Assets.Chunk(24)) { token.ThrowIfCancellationRequested(); var loaded = await Task.WhenAll(batch.Select(asset => CreateTileAsync(asset, token))); token.ThrowIfCancellationRequested(); groupTiles.AddRange(loaded.OfType<AssetTileViewModel>()); }
                tiles.AddRange(groupTiles); sections.Add(new PartSectionViewModel(group.Name, groupTiles));
            }
            token.ThrowIfCancellationRequested(); if (generation != _refreshGeneration) return;
            Sections.Clear(); foreach (var section in sections) Sections.Add(section);
            RebuildBrowserRows();
            _visibleTiles = tiles; _selection.Retain(tiles.Select(tile => tile.Asset.StableIdentity)); foreach (var tile in tiles) tile.IsSelected = _selection.Selected.Contains(tile.Asset.StableIdentity, StringComparer.OrdinalIgnoreCase);
            SelectedAsset = tiles.LastOrDefault(tile => tile.IsSelected); PruneTileCache(tiles);
            var suffix = plan.MatchingCount > plan.PrimaryCount ? $" Displaying the first {plan.PrimaryCount:N0}; narrow filters for more." : string.Empty;
            StatusText = $"{plan.MatchingCount:N0} matching assets.{suffix}"; OnPropertyChanged(nameof(SelectedAssetName));
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
            var collapsed = _collapsedBrowserSections.Contains(section.Name);
            rows.Add(BrowserDisplayRowViewModel.Header(section.Name, collapsed));
            if (collapsed) continue;
            foreach (var row in section.Assets.Chunk(_browserColumnCount))
                rows.Add(BrowserDisplayRowViewModel.AssetRow(row));
        }
        BrowserRows.ReplaceAll(rows);
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
        var matches = _catalog.Filter(CurrentFilter());
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
        if (!SelectedCategory.Equals("Construction", StringComparison.OrdinalIgnoreCase)
            || !SelectedType.Equals("Walls", StringComparison.OrdinalIgnoreCase)
            || !(SelectedSubtype.Equals(All, StringComparison.OrdinalIgnoreCase)
                || SelectedSubtype.Equals("Wall Pieces", StringComparison.OrdinalIgnoreCase))) return null;
        var identities = _catalog.Filter(CurrentFilter()).Select(asset => asset.StableIdentity).ToHashSet(StringComparer.OrdinalIgnoreCase);
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
        var rebuilt = await Task.Run(() =>
            (Assets: new AssetCatalogIndex(_assets), Walls: WallConstructionCatalog.Build(_assets)));
        _catalog = rebuilt.Assets;
        _wallCatalog = rebuilt.Walls;
    }
}
