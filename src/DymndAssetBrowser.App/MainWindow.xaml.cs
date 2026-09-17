using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DymndAssetBrowser.App.Services;
using DymndAssetBrowser.App.ViewModels;
using DymndAssetBrowser.Core.Models;
using Microsoft.Win32;

namespace DymndAssetBrowser.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private Point _dragStart;
    private AssetTileViewModel? _dragCandidate;
    private bool _dragInProgress;
    private readonly HashSet<FrameworkElement> _assetTileElements = [];
    private readonly DispatcherTimer _thumbnailViewportTimer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(75)
    };
    private bool _isClosing;

    public MainWindow()
    {
        InitializeComponent();
        var version = typeof(MainWindow).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion.Split('+')[0];
        Title = string.IsNullOrWhiteSpace(version) ? "DYM&D Asset Browser" : $"DYM&D Asset Browser v{version}";
        DataContext = _viewModel;
        _thumbnailViewportTimer.Tick += ThumbnailViewportTimer_Tick;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel.SetBrowserViewportWidth(BrowserAssetList.ActualWidth);
        _viewModel.SetBuildViewportWidth(BuildPaletteColumns.ActualWidth);
        _viewModel.SetBuildViewportHeight(ActualHeight);
        await _viewModel.InitializeAsync();
        while (!_viewModel.IsInitialized)
        {
            var choice = MessageBox.Show(this,
                "Saved data could not be loaded. No settings have been overwritten.\n\n" + _viewModel.StartupError + "\n\nData folder: " + _viewModel.StateDirectory +
                (_viewModel.HasSettingsBackup ? "\n\nYes: restore the last saved settings backup.\nNo: start with fresh settings (the current file will be preserved).\nCancel: close without changes." :
                "\n\nYes: start with fresh settings (the current file will be preserved).\nNo or Cancel: close without changes."),
                "Recover saved settings", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
            if (choice == MessageBoxResult.Cancel || (!_viewModel.HasSettingsBackup && choice == MessageBoxResult.No)) { Close(); return; }
            try { await _viewModel.RecoverSettingsAsync(_viewModel.HasSettingsBackup && choice == MessageBoxResult.Yes); }
            catch (Exception ex) { MessageBox.Show(this, "Recovery failed.\n\n" + ex.Message, "Recover saved settings", MessageBoxButton.OK, MessageBoxImage.Error); Close(); return; }
            await _viewModel.InitializeAsync();
        }
        _viewModel.SetBrowserViewportWidth(BrowserAssetList.ActualWidth);
        _viewModel.SetBuildViewportWidth(BuildPaletteColumns.ActualWidth);
        if (!_viewModel.HasRegisteredLibraries) await OfferFirstRunSetupAsync();
    }
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = Math.Max(1, e.NewSize.Width - 24);
        _viewModel.SetBrowserViewportWidth(width);
        _viewModel.SetBuildViewportWidth(width);
        _viewModel.SetBuildViewportHeight(e.NewSize.Height);
    }
    private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isClosing) return;
        e.Cancel = true;
        try { await _viewModel.SaveStateAsync(); }
        catch (Exception ex)
        {
            if (MessageBox.Show(this, "Could not save settings.\n\n" + ex.Message + "\n\nClose without saving?", "DYM&D Asset Browser", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        }
        _isClosing = true; Close();
    }
    private void Window_Closed(object? sender, EventArgs e)
    {
        _isClosing = true;
        _thumbnailViewportTimer.Stop();
        foreach (var element in _assetTileElements)
            if (element.DataContext is AssetTileViewModel tile) MainViewModel.ReleaseThumbnail(tile);
        _assetTileElements.Clear();
    }

    private void AssetTile_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        _assetTileElements.Add(element);
        ScheduleThumbnailViewportRefresh();
    }

    private void AssetTile_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        _assetTileElements.Remove(element);
        if (element.DataContext is AssetTileViewModel tile) MainViewModel.ReleaseThumbnail(tile);
        // Recycling can unload an old container after its replacement has loaded.
        // Recheck surviving containers so a shared tile does not stay blank.
        ScheduleThumbnailViewportRefresh();
    }

    private void AssetScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) =>
        ScheduleThumbnailViewportRefresh();

    private void ScheduleThumbnailViewportRefresh()
    {
        if (_isClosing) return;
        _thumbnailViewportTimer.Stop();
        _thumbnailViewportTimer.Start();
    }

    private void ThumbnailViewportTimer_Tick(object? sender, EventArgs e)
    {
        _thumbnailViewportTimer.Stop();
        foreach (var element in _assetTileElements.ToArray())
        {
            if (!element.IsLoaded)
            {
                _assetTileElements.Remove(element);
                continue;
            }
            if (element.DataContext is not AssetTileViewModel tile) continue;
            var scrollViewer = FindOwningAssetScrollViewer(element);
            if (scrollViewer is not null && IsNearViewport(element, scrollViewer))
                _ = _viewModel.EnsureThumbnailAsync(tile);
            else
                MainViewModel.ReleaseThumbnail(tile);
        }
    }

    private ScrollViewer? FindOwningAssetScrollViewer(DependencyObject element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is ScrollViewer scrollViewer) return scrollViewer;
        return null;
    }

    private static bool IsNearViewport(FrameworkElement element, ScrollViewer scrollViewer)
    {
        try
        {
            var bounds = element.TransformToAncestor(scrollViewer)
                .TransformBounds(new Rect(new Point(), element.RenderSize));
            const double preloadMargin = 320;
            var viewport = new Rect(-preloadMargin, -preloadMargin,
                scrollViewer.ActualWidth + preloadMargin * 2,
                scrollViewer.ActualHeight + preloadMargin * 2);
            return bounds.IntersectsWith(viewport);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private async void AddLibrary_Click(object sender, RoutedEventArgs e)
    {
        var folder = new OpenFolderDialog { Title = "Add a read-only asset library", Multiselect = false };
        if (folder.ShowDialog(this) != true) return;
        var defaultName = Path.GetFileName(folder.FolderName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var name = new LibraryNameDialog(defaultName, actionLabel: "Add Library")
            { Owner = this, Title = "Name Asset Library" };
        if (name.ShowDialog() == true) await _viewModel.AddLibraryAsync(name.Value, folder.FolderName);
    }

    private async void AddFaLibrary_Click(object sender, RoutedEventArgs e) => await ChooseFaLibraryAsync();

    private async Task OfferFirstRunSetupAsync()
    {
        var answer = MessageBox.Show(this,
            "Welcome to DYM&D Asset Browser.\n\nChoose your Forgotten Adventures _Assets folder now? " +
            "The browser indexes files in place and never copies, moves, renames, modifies, or deletes source assets.",
            "Set Up Forgotten Adventures",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (answer == MessageBoxResult.Yes) await ChooseFaLibraryAsync();
    }

    private async Task ChooseFaLibraryAsync()
    {
        var folder = new OpenFolderDialog
        {
            Title = "Choose the Forgotten Adventures _Assets folder (or its parent)",
            Multiselect = false
        };
        if (folder.ShowDialog(this) != true) return;

        if (!await _viewModel.AddFaLibraryAsync(folder.FolderName))
        {
            MessageBox.Show(this,
                _viewModel.StatusText,
                "Forgotten Adventures Library",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void RenameLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSource is not { } source) return;
        var dialog = new LibraryNameDialog(source.Name, actionLabel: "Save")
            { Owner = this, Title = "Rename Asset Library" };
        if (dialog.ShowDialog() == true) await _viewModel.RenameSelectedLibraryAsync(dialog.Value);
    }

    private async void RemoveLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedSource is not { } source) return;
        var answer = MessageBox.Show(this, $"Remove '{source.Name}' from the index?\n\nNo source files will be deleted or modified.",
            "Remove Asset Library", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes) await _viewModel.RemoveSelectedLibraryAsync();
    }

    private async void ReindexSource_Click(object sender, RoutedEventArgs e) => await _viewModel.ReindexSelectedLibraryAsync();
    private async void ReindexAll_Click(object sender, RoutedEventArgs e) => await _viewModel.ReindexAllLibrariesAsync();
    private void ResetFilters_Click(object sender, RoutedEventArgs e) => _viewModel.ResetFilters();
    private void ClearFilenameSearch_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SearchText = string.Empty;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            FilenameSearchTextBox.Focus();
            Keyboard.Focus(FilenameSearchTextBox);
            FilenameSearchTextBox.CaretIndex = 0;
        });
    }
    private async void SaveMapping_Click(object sender, RoutedEventArgs e) => await _viewModel.SaveMappingAsync();
    private void ShowBrowser_Click(object sender, RoutedEventArgs e) => _viewModel.ShowBrowser();
    private void ShowBuild_Click(object sender, RoutedEventArgs e) => _viewModel.ShowBuild();
    private async void PopulateBuild_Click(object sender, RoutedEventArgs e)
    {
        BuildSetupExpander.IsExpanded = false;
        await _viewModel.PopulateBuildAsync();
    }
    private async void ResetBuild_Click(object sender, RoutedEventArgs e)
    {
        BuildSetupExpander.IsExpanded = true;
        await _viewModel.ResetBuildAsync();
    }
    private async void ResetBuildWalls_Click(object sender, RoutedEventArgs e)
    {
        BuildSetupExpander.IsExpanded = true;
        await _viewModel.ResetBuildComponentAsync(BuildComponent.Walls);
    }
    private async void ResetBuildFloor_Click(object sender, RoutedEventArgs e)
    {
        BuildSetupExpander.IsExpanded = true;
        await _viewModel.ResetBuildComponentAsync(BuildComponent.Floor);
    }
    private async void ResetBuildTrim_Click(object sender, RoutedEventArgs e)
    {
        BuildSetupExpander.IsExpanded = true;
        await _viewModel.ResetBuildComponentAsync(BuildComponent.Trim);
    }
    private async void SaveBuildMapping_Click(object sender, RoutedEventArgs e) => await _viewModel.SaveBuildWallMappingAsync();
    private void BuildSectionHeader_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is BuildPaletteSection section)
            _viewModel.ToggleBuildSection(section);
    }
    private int _browserAnchorGeneration;
    private async void BrowserSectionHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement button || button.Tag is not string sectionName) return;
        var scroll = FindOwningAssetScrollViewer(button);
        var header = button.DataContext as BrowserDisplayRowViewModel;
        var anchorY = scroll is null ? 0 : button.TranslatePoint(new Point(), scroll).Y;
        var generation = ++_browserAnchorGeneration;
        _viewModel.ToggleBrowserSection(sectionName);
        if (scroll is null || header is null) return;
        // Pixel-scrolling a mixed-height virtual list can still re-estimate its
        // extent after incremental edits. Anchor to the actual heading, not an
        // old absolute offset. Two layout passes allow those estimates to settle.
        for (int pass = 0; pass < 2; pass++)
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            if (_isClosing || generation != _browserAnchorGeneration || !button.IsLoaded
                || !ReferenceEquals(button.DataContext, header) || !_viewModel.BrowserRows.Contains(header)) return;
            scroll.UpdateLayout();
            var delta = button.TranslatePoint(new Point(), scroll).Y - anchorY;
            if (Math.Abs(delta) > 0.5)
            {
                scroll.ScrollToVerticalOffset(Math.Clamp(scroll.VerticalOffset + delta, 0, scroll.ScrollableHeight));
                scroll.UpdateLayout();
            }
        }
    }
    private void ExpandAllBrowser_Click(object sender, RoutedEventArgs e) => SetAllBrowserSections(true);
    private void CollapseAllBrowser_Click(object sender, RoutedEventArgs e) => SetAllBrowserSections(false);
    private void SetAllBrowserSections(bool expanded)
    {
        ++_browserAnchorGeneration;
        _viewModel.SetAllBrowserSectionsExpanded(expanded);
        if (_viewModel.BrowserRows.Count > 0) BrowserAssetList.ScrollIntoView(_viewModel.BrowserRows[0]);
    }
    private void Utilities_Click(object sender, RoutedEventArgs e)
    {
        if (_dragInProgress) return;
        new CacheUtilitiesWindow(_viewModel.ImageCache) { Owner = this }.ShowDialog();
    }
    private void RotateLeft_Click(object sender, RoutedEventArgs e) => _viewModel.RotateSelected(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -45 : -90);
    private void RotateRight_Click(object sender, RoutedEventArgs e) => _viewModel.RotateSelected(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 45 : 90);
    private void FlipHorizontal_Click(object sender, RoutedEventArgs e) => _viewModel.FlipSelectedHorizontally();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase or PasswordBox) return;
        if (SourceControls.IsKeyboardFocusWithin || Keyboard.FocusedElement is ComboBox or ComboBoxItem) return;
        if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && _viewModel.IsBrowserMode) { _viewModel.SelectAllBrowserResults(); e.Handled = true; return; }
        if (Infrastructure.TransformShortcuts.Apply(_viewModel, e.Key, Keyboard.Modifiers, e.IsRepeat)) e.Handled = true;
    }

    private void Asset_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not AssetTileViewModel asset) return;
        FocusManager.SetFocusedElement(this, (IInputElement)sender);
        Keyboard.Focus((IInputElement)sender);
        _dragStart = e.GetPosition(this); _dragCandidate = asset;
        var modifiers = Keyboard.Modifiers;
        _viewModel.SelectAsset(asset, modifiers.HasFlag(ModifierKeys.Control), modifiers.HasFlag(ModifierKeys.Shift));
    }

    private void Asset_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var candidate = _dragCandidate;
        _dragCandidate = null;
        if (_dragInProgress || candidate is null
            || !ReferenceEquals((sender as FrameworkElement)?.DataContext, candidate)) return;
        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _dragStart.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(current.Y - _dragStart.Y) >= SystemParameters.MinimumVerticalDragDistance) return;
        // A click chooses a Planner sample; a drag must never rebuild its source tile.
        if (_viewModel.SelectPlannerSample(candidate)) e.Handled = true;
    }

    private async void Asset_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragInProgress || e.LeftButton != MouseButtonState.Pressed || _dragCandidate is null) return;
        var candidate = TakeDragCandidate(e.GetPosition(this));
        if (candidate is null) return;
        _dragInProgress = true;
        try
        {
            var path = await _viewModel.GetDragFileAsync(candidate);
            if (path is null || !File.Exists(path)) return;
            var data = new DataObject(); data.SetFileDropList(new StringCollection { path });
            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy);
        }
        finally { _dragInProgress = false; }
    }

    private AssetTileViewModel? TakeDragCandidate(Point current)
    {
        if (_dragCandidate is null
            || (Math.Abs(current.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(current.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)) return null;
        var candidate = _dragCandidate;
        _dragCandidate = null;
        return candidate;
    }

    private void Asset_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not AssetTileViewModel asset || element.ContextMenu is null)
        {
            return;
        }

        _viewModel.SelectAsset(asset, rightClick: true);
        var menu = element.ContextMenu;
        menu.Items.Clear();
        var foundryPath = FoundryAssetPathService.Resolve(asset.Asset);
        var copyPath = new MenuItem
        {
            Header = "Copy filepath",
            IsEnabled = foundryPath is not null,
            ToolTip = foundryPath is null
                ? "No matching file was found under the configured Foundry Data folder."
                : foundryPath
        };
        if (foundryPath is not null)
        {
            copyPath.Click += (_, _) => CopyFoundryPath(foundryPath);
        }
        menu.Items.Add(copyPath);
        var copyFullPath = new MenuItem { Header = "Copy full filepath", ToolTip = asset.Asset.FilePath };
        copyFullPath.Click += (_, _) =>
        {
            try { Clipboard.SetText(System.IO.Path.GetFullPath(asset.Asset.FilePath)); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not copy full filepath", MessageBoxButton.OK, MessageBoxImage.Warning); }
        };
        menu.Items.Add(copyFullPath);
        var showInExplorer = new MenuItem { Header = "Show in File Explorer", ToolTip = asset.Asset.FilePath };
        showInExplorer.Click += (_, _) =>
        {
            try { ExplorerRevealService.Reveal(asset.Asset.FilePath); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not show source file", MessageBoxButton.OK, MessageBoxImage.Warning); }
        };
        menu.Items.Add(showInExplorer);
        var editTags = new MenuItem { Header = "Edit tags…" };
        editTags.Click += (_, _) => SourceBrowserControls.ShowTagEditor(this, _viewModel);
        menu.Items.Add(editTags);

        var browserFilters = _viewModel.BrowserFilterOptions(asset);
        if (browserFilters.Count > 0)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(Infrastructure.AssetFilterMenu.Create(browserFilters,
                keys => _viewModel.ApplyBrowserFilter(asset, keys), () => menu.IsOpen = false));
        }

        var plannerFilters = _viewModel.PlannerFilterOptions(asset);
        if (plannerFilters.Count > 0)
        {
            menu.Items.Add(new Separator());
            var targets = new[] { new Infrastructure.AssetFilterTarget("Walls"), new Infrastructure.AssetFilterTarget("Floor"), new Infrastructure.AssetFilterTarget("Trim") };
            var filterMenu = Infrastructure.AssetFilterMenu.Create(plannerFilters,
                keys => _viewModel.ApplyPlannerFilters(asset, keys, targets.Where(t => t.IsSelected).Select(t => Enum.Parse<BuildComponent>(t.Label))),
                () => menu.IsOpen = false, targets: targets);
            filterMenu.Header = "Apply filters to Planner";
            filterMenu.ToolTip = "Choose target sections. Ctrl/Shift-click to queue characteristics, then Apply selected. Replaces filters only in the checked sections.";
            menu.Items.Add(filterMenu);
        }
        var wallMaterials = _viewModel.WallMaterialsForTrim(asset);
        if (_viewModel.IsPlannerWall(asset))
        {
            var transferMaterials = new MenuItem
            {
                Header = "Use these materials for Trim",
                IsEnabled = wallMaterials.Count > 0,
                ToolTip = wallMaterials.Count == 0 ? "No material metadata is available for this wall set."
                    : string.Join(", ", wallMaterials) + ". Replaces Trim materials; allows matching single or mixed pieces, with no extra finishes. Other Trim filters stay unchanged."
            };
            transferMaterials.Click += (_, _) => _viewModel.UseWallMaterialsForTrim(asset);
            menu.Items.Add(transferMaterials);
        }
    }

    private static void CopyFoundryPath(string foundryPath)
    {
        try
        {
            Clipboard.SetText(foundryPath);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            MessageBox.Show("Windows could not access the clipboard. Please try Copy filepath again.",
                "Copy filepath", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

}
