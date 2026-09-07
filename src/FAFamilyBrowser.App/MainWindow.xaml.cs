using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FAFamilyBrowser.App.Services;
using FAFamilyBrowser.App.ViewModels;
using FAFamilyBrowser.Core.Models;
using Microsoft.Win32;

namespace FAFamilyBrowser.App;

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
        Title = string.IsNullOrWhiteSpace(version) ? "Dym&D Asset Companion" : $"Dym&D Asset Companion v{version}";
        DataContext = _viewModel;
        _thumbnailViewportTimer.Tick += ThumbnailViewportTimer_Tick;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel.SetBrowserViewportWidth(BrowserAssetList.ActualWidth);
        _viewModel.SetBuildViewportWidth(BuildAssetList.ActualWidth);
        await _viewModel.InitializeAsync();
        _viewModel.SetBrowserViewportWidth(BrowserAssetList.ActualWidth);
        _viewModel.SetBuildViewportWidth(BuildAssetList.ActualWidth);
        if (!_viewModel.HasRegisteredLibraries) await OfferFirstRunSetupAsync();
    }
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = Math.Max(1, e.NewSize.Width - 24);
        _viewModel.SetBrowserViewportWidth(width);
        _viewModel.SetBuildViewportWidth(width);
    }
    private async void Window_Closed(object? sender, EventArgs e)
    {
        _isClosing = true;
        _thumbnailViewportTimer.Stop();
        foreach (var element in _assetTileElements)
            if (element.DataContext is AssetTileViewModel tile) MainViewModel.ReleaseThumbnail(tile);
        _assetTileElements.Clear();
        try { await _viewModel.SaveStateAsync(); } catch { }
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
            "Welcome to Dym&D Asset Companion.\n\nChoose your Forgotten Adventures _Assets folder now? " +
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
    private void BrowserSectionHeader_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string sectionName)
            _viewModel.ToggleBrowserSection(sectionName);
    }
    private void RotateLeft_Click(object sender, RoutedEventArgs e) => _viewModel.RotateSelected(-90);
    private void RotateRight_Click(object sender, RoutedEventArgs e) => _viewModel.RotateSelected(90);
    private void FlipHorizontal_Click(object sender, RoutedEventArgs e) => _viewModel.FlipSelectedHorizontally();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is System.Windows.Controls.TextBox) return;
        if (e.Key == Key.Q) { _viewModel.RotateSelected(-90); e.Handled = true; }
        else if (e.Key == Key.E) { _viewModel.RotateSelected(90); e.Handled = true; }
        else if (e.Key == Key.F) { _viewModel.FlipSelectedHorizontally(); e.Handled = true; }
    }

    private void Asset_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not AssetTileViewModel asset) return;
        if (_viewModel.SelectPlannerSample(asset))
        {
            e.Handled = true;
            return;
        }
        FocusManager.SetFocusedElement(this, (IInputElement)sender);
        Keyboard.Focus((IInputElement)sender);
        _dragStart = e.GetPosition(this); _dragCandidate = asset;
        var modifiers = Keyboard.Modifiers;
        _viewModel.SelectAsset(asset, modifiers.HasFlag(ModifierKeys.Control), modifiers.HasFlag(ModifierKeys.Shift));
    }

    private async void Asset_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragInProgress || e.LeftButton != MouseButtonState.Pressed || _dragCandidate is null) return;
        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(current.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var candidate = _dragCandidate; _dragCandidate = null; _dragInProgress = true;
        try
        {
            var path = await _viewModel.GetDragFileAsync(candidate);
            if (path is null || !File.Exists(path)) return;
            var data = new DataObject(); data.SetFileDropList(new StringCollection { path });
            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy);
        }
        finally { _dragInProgress = false; }
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

        var plannerFilters = _viewModel.PlannerFilterOptions(asset);
        if (plannerFilters.Count > 0)
        {
            menu.Items.Add(new Separator());
            var filterMenu = new MenuItem { Header = "Filter Planner to" };
            foreach (var option in plannerFilters)
            {
                var filterItem = new MenuItem { Header = $"{option.Label}: {option.Value}" };
                filterItem.Click += (_, _) => _viewModel.ApplyPlannerFilter(asset, option.Key);
                filterMenu.Items.Add(filterItem);
            }
            menu.Items.Add(filterMenu);
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
