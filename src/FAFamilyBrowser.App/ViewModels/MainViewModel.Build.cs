using System.Collections.ObjectModel;
using System.Windows;
using FAFamilyBrowser.Core.Models;

namespace FAFamilyBrowser.App.ViewModels;

public sealed record PlannerFilterOption(string Key, string Label, string Value);

public sealed partial class MainViewModel
{
    private const int BuildSampleLimit = 240;
    private readonly List<AssetTileViewModel> _buildVisibleTiles = [];
    private CancellationTokenSource? _buildRefreshCancellation;
    private int _buildRefreshGeneration;
    private bool _isBuildMode;
    private bool _buildIsPopulated;
    private string _buildWallRibbonText = "WALL RIBBON BRUSH = Populate a wall set to resolve its ribbon";
    private string _buildCspTool = string.Empty;
    private string _buildCspToolGroup = string.Empty;
    private string _buildCspRibbonName = string.Empty;
    private int _buildColumnsPerSection = 2;
    private readonly HashSet<BuildPaletteSection> _collapsedBuildSections = [];
    private int _floorPreviewMatchingCount;
    private int _trimPreviewMatchingCount;

    public WallSetSelectorViewModel WallsBuildSelector { get; }
    public BuildSelectorViewModel FloorBuildSelector { get; }
    public BuildSelectorViewModel TrimBuildSelector { get; }
    public BulkObservableCollection<AssetTileViewModel> BuildWallSampleAssets { get; } = [];
    public BulkObservableCollection<AssetTileViewModel> BuildFloorSampleAssets { get; } = [];
    public BulkObservableCollection<AssetTileViewModel> BuildTrimSampleAssets { get; } = [];
    public BulkObservableCollection<AssetTileViewModel> BuildWallAssets { get; } = [];
    public BulkObservableCollection<AssetTileViewModel> BuildWallComponentAssets { get; } = [];
    public BulkObservableCollection<AssetTileViewModel> BuildWallDetailAssets { get; } = [];
    public BulkObservableCollection<AssetTileViewModel> BuildFloorAssets { get; } = [];
    public BulkObservableCollection<AssetTileViewModel> BuildDoorFrameAssets { get; } = [];
    public BulkObservableCollection<AssetTileViewModel> BuildWindowSillAssets { get; } = [];
    public BulkObservableCollection<AssetTileViewModel> BuildOtherOpeningAssets { get; } = [];
    public BulkObservableCollection<BuildPaletteDisplayRowViewModel> BuildPaletteRows { get; } = [];

    public bool IsBuildMode
    {
        get => _isBuildMode;
        private set
        {
            if (!SetProperty(ref _isBuildMode, value)) return;
            OnPropertyChanged(nameof(IsBrowserMode));
            OnPropertyChanged(nameof(SelectedAssets));
            _selection.Clear();
            foreach (var tile in _visibleTiles.Concat(_buildVisibleTiles)) tile.IsSelected = false;
            SelectedAsset = null;
        }
    }

    public bool IsBrowserMode => !IsBuildMode;
    public bool BuildHasPalette => BuildWallSampleAssets.Count + BuildFloorSampleAssets.Count + BuildTrimSampleAssets.Count
        + BuildWallAssets.Count + BuildWallComponentAssets.Count + BuildWallDetailAssets.Count + BuildFloorAssets.Count + BuildDoorFrameAssets.Count
        + BuildWindowSillAssets.Count + BuildOtherOpeningAssets.Count > 0;
    public string BuildSummary => $"Walls: {BuildWallsSummary}    |    Floor: {BuildFloorSummary}    |    Trim: {BuildTrimSummary}";
    public string BuildWallsSummary => WallsBuildSelector.Summary;
    public string BuildFloorSummary => FloorBuildSelector.Summary;
    public string BuildTrimSummary => TrimBuildSelector.Summary;
    public string BuildWallRibbonText { get => _buildWallRibbonText; private set => SetProperty(ref _buildWallRibbonText, value); }
    public string BuildCspTool { get => _buildCspTool; set => SetProperty(ref _buildCspTool, value); }
    public string BuildCspToolGroup { get => _buildCspToolGroup; set => SetProperty(ref _buildCspToolGroup, value); }
    public string BuildCspRibbonName { get => _buildCspRibbonName; set => SetProperty(ref _buildCspRibbonName, value); }

    public void ShowBrowser() => IsBuildMode = false;

    public void ShowBuild()
    {
        IsBuildMode = true;
        RefreshBuildSelectors();
        if (_buildIsPopulated && !BuildHasPalette)
        {
            StatusText = "Restoring the saved building palette…";
            _ = PopulateBuildAsync(saveState: false);
        }
        else
        {
            if (!_buildIsPopulated) RefreshWallSetPreview();
            StatusText = _buildIsPopulated
                ? $"Build palette ready: {BuildWallAssets.Count:N0} walls, {BuildFloorAssets.Count:N0} floors, {BuildDoorFrameAssets.Count + BuildWindowSillAssets.Count + BuildOtherOpeningAssets.Count:N0} openings."
                : WallPreviewStatus();
        }
    }

    public async Task PopulateBuildAsync(bool saveState = true)
    {
        var cancellation = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _buildRefreshCancellation, cancellation);
        previous?.Cancel();
        previous?.Dispose();
        var token = cancellation.Token;
        var generation = ++_buildRefreshGeneration;
        var recipe = CurrentBuildRecipe(isPopulated: true);
        var catalog = _catalog;
        var sourceId = SelectedSourceId;

        IsBusy = true;
        StatusText = "Generating building palette…";
        try
        {
            var wallCatalog = _wallCatalog;
            var palette = await Task.Run(() => BuildModeService.Populate(catalog, wallCatalog, recipe, _state.RibbonMappings, sourceId), token);
            token.ThrowIfCancellationRequested();

            var walls = await CreateTilesAsync(palette.Walls, token);
            var wallComponents = await CreateTilesAsync(palette.WallComponents, token);
            var details = await CreateTilesAsync(palette.WallDetails, token);
            var floors = await CreateTilesAsync(palette.Floors, token);
            var doors = await CreateTilesAsync(palette.DoorFrames, token);
            var windows = await CreateTilesAsync(palette.WindowSills, token);
            var other = await CreateTilesAsync(palette.OtherOpenings, token);
            token.ThrowIfCancellationRequested();
            if (generation != _buildRefreshGeneration) return;

            ReleaseTiles(BuildWallSampleAssets);
            ReleaseTiles(BuildFloorSampleAssets);
            ReleaseTiles(BuildTrimSampleAssets);
            BuildWallSampleAssets.Clear();
            BuildFloorSampleAssets.Clear();
            BuildTrimSampleAssets.Clear();
            BuildWallAssets.ReplaceAll(walls);
            BuildWallComponentAssets.ReplaceAll(wallComponents);
            BuildWallDetailAssets.ReplaceAll(details);
            BuildFloorAssets.ReplaceAll(floors);
            BuildDoorFrameAssets.ReplaceAll(doors);
            BuildWindowSillAssets.ReplaceAll(windows);
            BuildOtherOpeningAssets.ReplaceAll(other);
            _buildVisibleTiles.Clear();
            _buildVisibleTiles.AddRange(walls.Concat(wallComponents).Concat(details).Concat(floors).Concat(doors).Concat(windows).Concat(other));
            _buildIsPopulated = true;
            RebuildBuildPaletteRows();
            ApplyWallRibbonResolution(palette.WallRibbon);
            OnPropertyChanged(nameof(BuildHasPalette));
            OnPropertyChanged(nameof(BuildSummary));
            NotifyBuildSummaries();
            if (IsBuildMode)
            {
                _selection.Retain(_buildVisibleTiles.Select(tile => tile.Asset.StableIdentity));
                foreach (var tile in _buildVisibleTiles) tile.IsSelected = _selection.Selected.Contains(tile.Asset.StableIdentity, StringComparer.OrdinalIgnoreCase);
            }

            PruneTileCache(_visibleTiles.Concat(_buildVisibleTiles));
            if (saveState) await SaveStateAsync();
            var trimCount = doors.Count + windows.Count + other.Count;
            StatusText = walls.Count == 0 && WallsBuildSelector.CandidateCount != 1
                ? $"Choose one wall set before building. {WallsBuildSelector.CandidateCount:N0} sets currently match."
                : $"Build palette populated: {walls.Count:N0} wall pieces, {wallComponents.Count:N0} wall additions, {details.Count:N0} detailing assets, {floors.Count:N0} floor assets, and {trimCount:N0} opening assets.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(Interlocked.CompareExchange(ref _buildRefreshCancellation, null, cancellation), cancellation)) cancellation.Dispose();
            if (generation == _buildRefreshGeneration) IsBusy = false;
        }
    }

    public async Task SaveBuildWallMappingAsync()
    {
        var set = WallsBuildSelector.SelectedSet;
        if (set is null)
        {
            StatusText = "Narrow Walls until exactly one wall set remains before saving a ribbon mapping.";
            return;
        }
        var key = BuildModeService.WallMappingKey(set);
        _state.RibbonMappings[key] = new RibbonMapping
        {
            FamilyKey = key,
            CspTool = BuildCspTool.Trim(),
            CspToolGroup = BuildCspToolGroup.Trim(),
            CspRibbonName = BuildCspRibbonName.Trim(),
            IsManuallyConfirmed = true
        };
        await PopulateBuildAsync(saveState: false);
        await SaveStateAsync();
        StatusText = "Wall ribbon mapping saved once for this complete wall set.";
    }

    public async Task ResetBuildAsync()
    {
        CancelBuildRefresh();
        WallsBuildSelector.Reset();
        FloorBuildSelector.Reset();
        TrimBuildSelector.Reset();
        ClearBuildResults(BuildComponent.Walls);
        ClearBuildResults(BuildComponent.Floor);
        ClearBuildResults(BuildComponent.Trim);
        FinishBuildReset("Planner reset. Choose any Walls, Floor, and Trim details, then Populate.");
        await SaveStateAsync();
    }

    public void SetBuildViewportWidth(double width)
    {
        if (width <= 0) return;
        var columns = Math.Max(1, (int)Math.Floor(((width - 28) / 2) / 213));
        if (columns == _buildColumnsPerSection) return;
        _buildColumnsPerSection = columns;
        RebuildBuildPaletteRows();
    }

    public void ToggleBuildSection(BuildPaletteSection section)
    {
        if (!_collapsedBuildSections.Add(section)) _collapsedBuildSections.Remove(section);
        RebuildBuildPaletteRows();
    }

    public async Task ResetBuildComponentAsync(BuildComponent component)
    {
        CancelBuildRefresh();
        var title = component switch
        {
            BuildComponent.Walls => WallsBuildSelector.Title,
            BuildComponent.Floor => FloorBuildSelector.Title,
            _ => TrimBuildSelector.Title
        };
        switch (component)
        {
            case BuildComponent.Walls: WallsBuildSelector.Reset(); break;
            case BuildComponent.Floor: FloorBuildSelector.Reset(); break;
            default: TrimBuildSelector.Reset(); break;
        }
        ClearBuildResults(component);
        FinishBuildReset($"{title} reset. Choose new details, then Populate.");
        await SaveStateAsync();
    }

    private void RestoreBuildRecipe(BuildRecipe? recipe)
    {
        recipe ??= new BuildRecipe();
        WallsBuildSelector.Restore(recipe.WallSet);
        FloorBuildSelector.Restore(BuildModeService.Canonicalize(recipe.Floor, BuildComponent.Floor));
        TrimBuildSelector.Restore(BuildModeService.Canonicalize(recipe.Trim, BuildComponent.Trim));
        _buildIsPopulated = recipe.IsPopulated;
        OnPropertyChanged(nameof(BuildSummary));
        NotifyBuildSummaries();
    }

    private BuildRecipe CurrentBuildRecipe(bool? isPopulated = null) => new()
    {
        WallSet = WallsBuildSelector.PersistedSelection,
        Floor = FloorBuildSelector.Selection,
        Trim = TrimBuildSelector.Selection,
        IsPopulated = isPopulated ?? _buildIsPopulated
    };

    private void RefreshBuildSelectors()
    {
        WallsBuildSelector.RefreshChoices();
        FloorBuildSelector.RefreshChoices();
        TrimBuildSelector.RefreshChoices();
        OnPropertyChanged(nameof(BuildSummary));
        NotifyBuildSummaries();
    }

    private void BuildSelector_SelectionChanged(object? sender, EventArgs e)
    {
        CancelBuildRefresh();
        _buildIsPopulated = false;
        if (ReferenceEquals(sender, WallsBuildSelector)) SeedUnfilteredComponentsFromWalls();
        RefreshWallSetPreview();
        OnPropertyChanged(nameof(BuildSummary));
        NotifyBuildSummaries();
        StatusText = WallPreviewStatus();
    }

    private void NotifyBuildSummaries()
    {
        OnPropertyChanged(nameof(BuildWallsSummary));
        OnPropertyChanged(nameof(BuildFloorSummary));
        OnPropertyChanged(nameof(BuildTrimSummary));
    }

    private void CancelBuildRefresh()
    {
        var cancellation = Interlocked.Exchange(ref _buildRefreshCancellation, null);
        cancellation?.Cancel();
        cancellation?.Dispose();
        _buildRefreshGeneration++;
        IsBusy = false;
    }

    private void ClearBuildResults(BuildComponent component)
    {
        switch (component)
        {
            case BuildComponent.Walls:
                ReleaseTiles(BuildWallSampleAssets);
                BuildWallSampleAssets.Clear();
                BuildWallAssets.Clear();
                BuildWallComponentAssets.Clear();
                BuildWallDetailAssets.Clear();
                BuildCspTool = string.Empty;
                BuildCspToolGroup = string.Empty;
                BuildCspRibbonName = string.Empty;
                BuildWallRibbonText = "WALL RIBBON BRUSH = Populate a wall set to resolve its ribbon";
                break;
            case BuildComponent.Floor:
                ReleaseTiles(BuildFloorSampleAssets);
                BuildFloorSampleAssets.Clear();
                BuildFloorAssets.Clear();
                break;
            case BuildComponent.Trim:
                ReleaseTiles(BuildTrimSampleAssets);
                BuildTrimSampleAssets.Clear();
                BuildDoorFrameAssets.Clear();
                BuildWindowSillAssets.Clear();
                BuildOtherOpeningAssets.Clear();
                break;
        }
    }

    private void FinishBuildReset(string status)
    {
        _buildIsPopulated = false;
        RefreshWallSetPreview();
        OnPropertyChanged(nameof(BuildSummary));
        NotifyBuildSummaries();
        StatusText = status;
    }

    public bool SelectWallSetSample(AssetTileViewModel tile)
    {
        if (!IsBuildMode || string.IsNullOrWhiteSpace(tile.WallSetId)) return false;
        return WallsBuildSelector.SelectSet(tile.WallSetId);
    }

    public bool SelectPlannerSample(AssetTileViewModel tile)
    {
        if (SelectWallSetSample(tile)) return true;
        if (!IsBuildMode || _buildIsPopulated) return false;
        if (BuildFloorSampleAssets.Contains(tile)) return FloorBuildSelector.SelectAsset(tile.Asset);
        if (BuildTrimSampleAssets.Contains(tile)) return TrimBuildSelector.SelectMaterialAppearance(tile.Asset);
        return false;
    }

    private void SeedUnfilteredComponentsFromWalls()
    {
        var choices = WallsBuildSelector.SelectedComponentAppearances
            .Where(choice => Meaningful(choice.Component))
            .DistinctBy(choice => choice.Component.Replace('_', ' '), StringComparer.OrdinalIgnoreCase)
            .ToList();
        SeedUnfilteredComponent(FloorBuildSelector, choices);
        SeedUnfilteredComponent(TrimBuildSelector, choices);
    }

    private static void SeedUnfilteredComponent(BuildSelectorViewModel selector,
        IReadOnlyList<WallComponentAppearance> wallChoices)
    {
        if (!selector.CanReceiveWallDefaults) return;
        var compatible = wallChoices
            .Where(choice => selector.Materials.Any(material =>
                material.Replace('_', ' ').Equals(choice.Component.Replace('_', ' '), StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (compatible.Count == 1)
        {
            selector.TrySeedMaterialAppearance(compatible[0].Component, compatible[0].Appearance);
            return;
        }
        selector.ClearAutomaticWallDefaults();
    }

    public IReadOnlyList<PlannerFilterOption> PlannerFilterOptions(AssetTileViewModel tile)
    {
        if (!IsBuildMode) return [];
        if (WallSetFor(tile) is { } set)
        {
            var options = new List<PlannerFilterOption>
            {
                new("WallSystem", "Wall System", set.WallSystem),
                new("PrimaryAppearance", set.PrimaryComponent, set.PrimaryAppearance),
                new("Theme", "Theme", set.Theme),
                new("Profile", "Profile", set.Profile),
                new("SetVariant", "Set Variant", set.Variant)
            };
            if (Meaningful(set.SecondaryComponent) && Meaningful(set.SecondaryAppearance))
                options.Insert(2, new("SecondaryAppearance", set.SecondaryComponent, set.SecondaryAppearance));
            return options.Where(option => Meaningful(option.Value)).ToList();
        }

        var component = PlannerComponentFor(tile);
        if (component is not (BuildComponent.Floor or BuildComponent.Trim)) return [];
        return new[]
        {
            new PlannerFilterOption("Material", "Material", tile.Asset.Material),
            new PlannerFilterOption("Appearance", "Appearance", tile.Asset.Style),
            new PlannerFilterOption("Theme", "Theme", tile.Asset.Theme),
            new PlannerFilterOption("Family", "Family", tile.Asset.Family),
            new PlannerFilterOption("Variant", "Variant", tile.Asset.Variant)
        }.Where(option => Meaningful(option.Value)).ToList();
    }

    public bool ApplyPlannerFilter(AssetTileViewModel tile, string key)
    {
        if (WallSetFor(tile) is { } set)
        {
            var facet = key switch
            {
                "WallSystem" => WallSetFacet.WallSystem,
                "PrimaryAppearance" => WallSetFacet.PrimaryAppearance,
                "SecondaryAppearance" => WallSetFacet.SecondaryAppearance,
                "Theme" => WallSetFacet.Theme,
                "Profile" => WallSetFacet.Profile,
                "SetVariant" => WallSetFacet.Variant,
                _ => (WallSetFacet?)null
            };
            var value = key switch
            {
                "WallSystem" => set.WallSystem,
                "PrimaryAppearance" => set.PrimaryAppearance,
                "SecondaryAppearance" => set.SecondaryAppearance,
                "Theme" => set.Theme,
                "Profile" => set.Profile,
                "SetVariant" => set.Variant,
                _ => string.Empty
            };
            if (facet is null || !Meaningful(value)) return false;
            WallsBuildSelector.FilterTo(facet.Value, value);
            return true;
        }

        var selector = PlannerComponentFor(tile) switch
        {
            BuildComponent.Floor => FloorBuildSelector,
            BuildComponent.Trim => TrimBuildSelector,
            _ => null
        };
        var assetFacet = key switch
        {
            "Material" => AssetFacet.Material,
            "Appearance" => AssetFacet.Style,
            "Theme" => AssetFacet.Theme,
            "Family" => AssetFacet.Family,
            "Variant" => AssetFacet.Variant,
            _ => (AssetFacet?)null
        };
        var valueFromAsset = key switch
        {
            "Material" => tile.Asset.Material,
            "Appearance" => tile.Asset.Style,
            "Theme" => tile.Asset.Theme,
            "Family" => tile.Asset.Family,
            "Variant" => tile.Asset.Variant,
            _ => string.Empty
        };
        if (selector is null || assetFacet is null || !Meaningful(valueFromAsset)) return false;
        selector.FilterTo(assetFacet.Value, valueFromAsset);
        return true;
    }

    private WallConstructionSet? WallSetFor(AssetTileViewModel tile)
    {
        if (!string.IsNullOrWhiteSpace(tile.WallSetId))
            return _wallCatalog.Sets.FirstOrDefault(set => set.Id.Equals(tile.WallSetId, StringComparison.OrdinalIgnoreCase));
        return In(tile, BuildWallAssets) || In(tile, BuildWallComponentAssets) || In(tile, BuildWallDetailAssets)
            ? WallsBuildSelector.SelectedSet
            : null;
    }

    private BuildComponent? PlannerComponentFor(AssetTileViewModel tile)
    {
        if (In(tile, BuildFloorSampleAssets) || In(tile, BuildFloorAssets)) return BuildComponent.Floor;
        if (In(tile, BuildTrimSampleAssets) || In(tile, BuildDoorFrameAssets)
            || In(tile, BuildWindowSillAssets) || In(tile, BuildOtherOpeningAssets)) return BuildComponent.Trim;
        if (!string.IsNullOrWhiteSpace(tile.WallSetId) || In(tile, BuildWallAssets)
            || In(tile, BuildWallComponentAssets) || In(tile, BuildWallDetailAssets)) return BuildComponent.Walls;
        return null;
    }

    private static bool In(AssetTileViewModel tile, IEnumerable<AssetTileViewModel> candidates) =>
        candidates.Any(candidate => candidate.Asset.StableIdentity.Equals(tile.Asset.StableIdentity, StringComparison.OrdinalIgnoreCase));

    private static bool Meaningful(string? value) => !string.IsNullOrWhiteSpace(value)
        && !value.Equals(AssetQueries.All, StringComparison.OrdinalIgnoreCase)
        && !value.Equals("Unspecified", StringComparison.OrdinalIgnoreCase)
        && !value.Equals("N/A", StringComparison.OrdinalIgnoreCase);

    private void RefreshWallSetPreview()
    {
        ReleaseTiles(BuildWallSampleAssets);
        ReleaseTiles(BuildFloorSampleAssets);
        ReleaseTiles(BuildTrimSampleAssets);
        BuildWallSampleAssets.Clear();
        BuildFloorSampleAssets.Clear();
        BuildTrimSampleAssets.Clear();
        BuildWallAssets.Clear();
        BuildWallComponentAssets.Clear();
        BuildWallDetailAssets.Clear();
        BuildFloorAssets.Clear();
        BuildDoorFrameAssets.Clear();
        BuildWindowSillAssets.Clear();
        BuildOtherOpeningAssets.Clear();

        var sets = WallsBuildSelector.CandidateSets;
        if (sets.Count > 1)
        {
            BuildWallSampleAssets.ReplaceAll(sets.Select(set =>
            {
                var representative = RepresentativeAsset(set);
                var ribbon = set.AutomaticRibbon.Status switch
                {
                    WallRibbonResolutionStatus.Resolved => "ribbon resolved",
                    WallRibbonResolutionStatus.Ambiguous => "ribbon ambiguous",
                    _ => "ribbon needs mapping"
                };
                return new AssetTileViewModel(representative, set.DisplayName,
                    $"{set.WallPieces.Count:N0} pieces  ·  {set.Details.Count:N0} detailing  ·  {ribbon}", set.Id);
            }));
            BuildCspTool = string.Empty;
            BuildCspToolGroup = string.Empty;
            BuildCspRibbonName = string.Empty;
            BuildWallRibbonText = $"WALL RIBBON BRUSH = {sets.Count:N0} WALL SETS MATCH — click a sample or narrow filters";
        }
        else if (sets.Count == 1)
        {
            var palette = BuildModeService.PreviewWallSet(_catalog, _wallCatalog, WallsBuildSelector.Selection,
                _state.RibbonMappings, TrimBuildSelector.Selection, SelectedSourceId);
            BuildWallAssets.ReplaceAll(palette.Walls.Select(CachedTile));
            BuildWallComponentAssets.ReplaceAll(palette.WallComponents.Select(CachedTile));
            BuildWallDetailAssets.ReplaceAll(palette.WallDetails.Select(CachedTile));
            ApplyWallRibbonResolution(palette.WallRibbon);
        }
        else
        {
            BuildCspTool = string.Empty;
            BuildCspToolGroup = string.Empty;
            BuildCspRibbonName = string.Empty;
            BuildWallRibbonText = "WALL RIBBON BRUSH = No matching wall set";
        }

        if (sets.Count != 1)
            BuildWallDetailAssets.ReplaceAll(BuildModeService.MatchThemeDetails(
                _catalog.Assets, WallsBuildSelector.Selection, SelectedSourceId).Select(CachedTile));

        var floorMatches = BuildModeService.MatchSelection(_catalog, BuildComponent.Floor,
            FloorBuildSelector.Selection, SelectedSourceId);
        var trimMatches = BuildModeService.MatchSelection(_catalog, BuildComponent.Trim,
            TrimBuildSelector.Selection, SelectedSourceId);
        _floorPreviewMatchingCount = floorMatches.Count;
        _trimPreviewMatchingCount = trimMatches.Count;
        BuildFloorSampleAssets.ReplaceAll(floorMatches.Take(BuildSampleLimit).Select(CachedTile));
        BuildTrimSampleAssets.ReplaceAll(trimMatches.Take(BuildSampleLimit).Select(CachedTile));

        _buildVisibleTiles.Clear();
        _buildVisibleTiles.AddRange(BuildWallSampleAssets.Concat(BuildWallAssets).Concat(BuildWallComponentAssets)
            .Concat(BuildWallDetailAssets).Concat(BuildFloorSampleAssets).Concat(BuildTrimSampleAssets));
        _selection.Retain(_buildVisibleTiles.Select(tile => tile.Asset.StableIdentity));
        if (SelectedAsset is not null && !_buildVisibleTiles.Contains(SelectedAsset)) SelectedAsset = null;
        foreach (var tile in _buildVisibleTiles)
            tile.IsSelected = _selection.Selected.Contains(tile.Asset.StableIdentity, StringComparer.OrdinalIgnoreCase);
        PruneTileCache(_visibleTiles.Concat(_buildVisibleTiles));
        RebuildBuildPaletteRows();
        OnPropertyChanged(nameof(BuildHasPalette));
    }

    private string WallPreviewStatus()
    {
        var walls = WallsBuildSelector.CandidateCount switch
        {
            0 => "no wall sets",
            1 => $"1 wall set ({WallsBuildSelector.SelectedSet!.DisplayName})",
            var count => $"{count:N0} wall sets"
        };
        return $"Window shopping: {walls}, {_floorPreviewMatchingCount:N0} floors, {_trimPreviewMatchingCount:N0} trim. "
            + "Click a wall set, exact floor, or trim material; right-click to filter by one property.";
    }

    private AssetTileViewModel CachedTile(AssetRecord asset)
    {
        var tile = _tileCache.GetOrAdd(asset.StableIdentity, _ => new AssetTileViewModel(asset));
        tile.UpdateAsset(asset);
        return tile;
    }

    private static AssetRecord RepresentativeAsset(WallConstructionSet set) => set.WallPieces
        .OrderBy(RepresentativeRank)
        .ThenBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase)
        .First();

    private static int RepresentativeRank(AssetRecord asset)
    {
        var name = asset.FileName;
        if (name.Contains("Corner", StringComparison.OrdinalIgnoreCase)) return 0;
        if (name.Contains("Straight", StringComparison.OrdinalIgnoreCase)) return 1;
        if (name.Contains("Connector", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Joint", StringComparison.OrdinalIgnoreCase)) return 2;
        if (!asset.PartType.Equals("Path", StringComparison.OrdinalIgnoreCase)) return 3;
        return 4;
    }

    private static void ReleaseTiles(IEnumerable<AssetTileViewModel> tiles)
    {
        foreach (var tile in tiles) tile.ReleaseThumbnail();
    }

    private async Task<List<AssetTileViewModel>> CreateTilesAsync(IEnumerable<AssetRecord> assets, CancellationToken token)
    {
        var tiles = new List<AssetTileViewModel>();
        foreach (var batch in assets.Chunk(24))
        {
            token.ThrowIfCancellationRequested();
            var loaded = await Task.WhenAll(batch.Select(asset => CreateTileAsync(asset, token)));
            tiles.AddRange(loaded.OfType<AssetTileViewModel>());
        }
        return tiles;
    }

    private void ApplyWallRibbonResolution(WallRibbonResolution resolution)
    {
        var mapping = resolution.Mapping;
        BuildCspTool = mapping?.CspTool ?? string.Empty;
        BuildCspToolGroup = mapping?.CspToolGroup ?? string.Empty;
        BuildCspRibbonName = mapping?.CspRibbonName ?? string.Empty;
        BuildWallRibbonText = resolution.Status switch
        {
            WallRibbonResolutionStatus.Resolved => $"WALL RIBBON BRUSH = {RibbonPath(mapping!)}",
            WallRibbonResolutionStatus.Ambiguous => $"WALL RIBBON BRUSH = AMBIGUOUS ({resolution.CandidateCount} mappings) — correct once below",
            _ => "WALL RIBBON BRUSH = MISSING — assign once below for this wall set"
        };
    }

    private static string RibbonPath(RibbonMapping mapping)
    {
        var parts = new[] { mapping.CspTool, mapping.CspToolGroup, mapping.CspRibbonName }
            .Where(value => !string.IsNullOrWhiteSpace(value));
        return string.Join(" > ", parts);
    }

    private List<AssetTileViewModel> ActiveVisibleTiles() => IsBuildMode ? _buildVisibleTiles : _visibleTiles;
    private void RebuildBuildPaletteRows()
    {
        var rows = new List<BuildPaletteDisplayRowViewModel>();
        var wallSamples = BuildWallSampleAssets.Count > 0;
        IReadOnlyList<AssetTileViewModel> walls = wallSamples ? BuildWallSampleAssets : BuildWallAssets;
        IReadOnlyList<AssetTileViewModel> floors = _buildIsPopulated ? BuildFloorAssets : BuildFloorSampleAssets;
        IReadOnlyList<AssetTileViewModel> detailing = BuildWallComponentAssets.Concat(BuildWallDetailAssets).ToList();
        IReadOnlyList<AssetTileViewModel> trim = _buildIsPopulated
            ? BuildDoorFrameAssets.Concat(BuildWindowSillAssets).Concat(BuildOtherOpeningAssets).ToList()
            : BuildTrimSampleAssets;

        AddPairedBuildSection(rows,
            wallSamples ? "MATCHING WALL SETS" : "WALL PIECES",
            wallSamples ? BuildPaletteSection.WallSetSamples : BuildPaletteSection.Walls, walls,
            "FLOOR TEXTURES", _buildIsPopulated ? BuildPaletteSection.Floors : BuildPaletteSection.FloorSamples, floors,
            rightCount: _buildIsPopulated ? floors.Count : _floorPreviewMatchingCount);
        AddPairedBuildSection(rows,
            "DETAILING", BuildPaletteSection.Detailing, detailing,
            "TRIM SET", _buildIsPopulated ? BuildPaletteSection.OtherOpenings : BuildPaletteSection.TrimSamples, trim,
            rightCount: _buildIsPopulated ? trim.Count : _trimPreviewMatchingCount);
        BuildPaletteRows.ReplaceAll(rows);
    }

    private void AddPairedBuildSection(List<BuildPaletteDisplayRowViewModel> rows,
        string leftTitle, BuildPaletteSection leftSection,
        IReadOnlyList<AssetTileViewModel> leftAssets, string rightTitle, BuildPaletteSection rightSection,
        IReadOnlyList<AssetTileViewModel> rightAssets, int? leftCount = null, int? rightCount = null)
    {
        var leftExpanded = !_collapsedBuildSections.Contains(leftSection);
        var rightExpanded = !_collapsedBuildSections.Contains(rightSection);
        rows.Add(new BuildPaletteDisplayRowViewModel
        {
            IsHeader = true,
            LeftHeader = HeaderText(leftTitle, leftCount ?? leftAssets.Count, leftExpanded),
            RightHeader = HeaderText(rightTitle, rightCount ?? rightAssets.Count, rightExpanded),
            LeftSection = leftSection,
            RightSection = rightSection,
            LeftHeaderHasBody = leftExpanded && leftAssets.Count > 0,
            RightHeaderHasBody = rightExpanded && rightAssets.Count > 0,
            HeaderMargin = new Thickness(0, rows.Count == 0 ? 0 : 8, 0, 0)
        });
        var leftRows = leftExpanded ? (int)Math.Ceiling((double)leftAssets.Count / _buildColumnsPerSection) : 0;
        var rightRows = rightExpanded ? (int)Math.Ceiling((double)rightAssets.Count / _buildColumnsPerSection) : 0;
        for (var row = 0; row < Math.Max(leftRows, rightRows); row++)
            rows.Add(new BuildPaletteDisplayRowViewModel
            {
                LeftAssets = leftExpanded ? leftAssets.Skip(row * _buildColumnsPerSection).Take(_buildColumnsPerSection).ToArray() : [],
                RightAssets = rightExpanded ? rightAssets.Skip(row * _buildColumnsPerSection).Take(_buildColumnsPerSection).ToArray() : [],
                IsLeftLastBodyRow = leftExpanded && row == leftRows - 1,
                IsRightLastBodyRow = rightExpanded && row == rightRows - 1
            });
    }

    private static string HeaderText(string title, int count, bool expanded) =>
        $"{(expanded ? "▼" : "▶")} {title}  ({count:N0})";
}
