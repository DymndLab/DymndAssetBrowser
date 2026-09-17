using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DymndAssetBrowser.App;
using DymndAssetBrowser.App.ViewModels;
using DymndAssetBrowser.Core.Models;

internal static class PlannerInteractionRegressionTests
{
    public static async Task Run(MainViewModel vm, MainWindow window, Action<bool, string> check)
    {
        var wallState = vm.WallsPlannerFilter.Selection;
        var floorState = vm.FloorPlannerFilter.Selection;
        var trimState = vm.TrimPlannerFilter.Selection;
        var floor = vm.BuildFloorSampleAssets.Single();
        var trim = vm.BuildTrimSampleAssets.First();
        await vm.EnsureThumbnailAsync(floor);
        await vm.EnsureThumbnailAsync(trim);
        var floorBitmap = floor.Thumbnail;
        var trimBitmap = trim.Thumbnail;
        check(floorBitmap is not null && trimBitmap is not null, "Planner regression starts with loaded floor and trim previews");
        var rightRows = vm.BuildRightPaletteRows.ToArray();
        vm.WallsPlannerFilter.ClearPinnedSelection();
        vm.WallsPlannerFilter.SelectIdentity(wallState.SelectedIdentity);
        check(vm.BuildRightPaletteRows.SequenceEqual(rightRows)
            && ReferenceEquals(floor.Thumbnail, floorBitmap) && ReferenceEquals(trim.Thumbnail, trimBitmap),
            "Changing wall selection retains untouched right-column rows and loaded floor/trim thumbnails");

        var refreshes = 0;
        EventHandler changed = (_, _) => refreshes++;
        vm.FloorPlannerFilter.SelectionChanged += changed;
        vm.SelectPlannerSample(floor);
        vm.SelectPlannerSample(floor);
        vm.FloorPlannerFilter.SelectionChanged -= changed;
        check(refreshes == 0 && ReferenceEquals(floor.Thumbnail, floorBitmap),
            "Repeated clicks on a pinned floor neither rebuild nor blank previews");

        vm.FloorPlannerFilter.ClearPinnedSelection();
        await Task.Delay(300);
        window.UpdateLayout();
        var source = Descendants<Border>(window).First(b => ReferenceEquals(b.DataContext, floor) && b.ContextMenu is not null);
        var down = typeof(MainWindow).GetMethod("Asset_PreviewMouseLeftButtonDown", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var up = typeof(MainWindow).GetMethod("Asset_PreviewMouseLeftButtonUp", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dragCandidate = typeof(MainWindow).GetField("_dragCandidate", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var downArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent };
        down.Invoke(window, [source, downArgs]);
        check(!vm.FloorPlannerFilter.HasPinnedSelection && ReferenceEquals(dragCandidate.GetValue(window), floor)
            && !downArgs.Handled, "Floor mouse-down arms drag without narrowing or rebuilding the palette");
        check(await vm.GetDragFileAsync(floor) == floor.Asset.FilePath && File.Exists(floor.Asset.FilePath),
            "Unpopulated floor drag resolves the original image for external import");
        up.Invoke(window, [source, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseUpEvent }]);
        check(vm.FloorPlannerFilter.Selection.SelectedIdentity == floor.Asset.StableIdentity
            && dragCandidate.GetValue(window) is null, "Floor mouse-up without movement chooses that texture and clears the drag candidate");
        vm.FloorPlannerFilter.ClearPinnedSelection();
        await Task.Delay(200);
        window.UpdateLayout();
        source = Descendants<Border>(window).First(b => ReferenceEquals(b.DataContext, floor) && b.ContextMenu is not null);
        down.Invoke(window, [source, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent }]);
        var takeDrag = typeof(MainWindow).GetMethod("TakeDragCandidate", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var start = (Point)typeof(MainWindow).GetField("_dragStart", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
        check(takeDrag.Invoke(window, [start]) is null && ReferenceEquals(dragCandidate.GetValue(window), floor),
            "Movement below drag threshold retains the pending floor click");
        var moved = new Point(start.X + SystemParameters.MinimumHorizontalDragDistance + 1, start.Y);
        check(ReferenceEquals(takeDrag.Invoke(window, [moved]), floor) && dragCandidate.GetValue(window) is null
            && !vm.FloorPlannerFilter.HasPinnedSelection, "Crossing drag threshold consumes the floor candidate without changing filters");
        up.Invoke(window, [source, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseUpEvent }]);
        check(!vm.FloorPlannerFilter.HasPinnedSelection, "Mouse-up with no click candidate does not choose a floor after a drag");
        vm.FloorPlannerFilter.SelectIdentity(floor.Asset.StableIdentity);

        var wall = vm.BuildWallAssets.First(t => vm.WallMaterialsForTrim(t).Count == 2);
        var wallBefore = JsonSerializer.Serialize(vm.WallsPlannerFilter.Selection);
        var floorBefore = JsonSerializer.Serialize(vm.FloorPlannerFilter.Selection);
        vm.TrimPlannerFilter.Reset();
        vm.TrimPlannerFilter.MatchAllMaterials = true;
        var menuSource = new Border { DataContext = wall, ContextMenu = new ContextMenu() };
        typeof(MainWindow).GetMethod("Asset_ContextMenuOpening", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, [menuSource, null]);
        var transferItem = menuSource.ContextMenu.Items.OfType<MenuItem>().SingleOrDefault(i => (string?)i.Header == "Use these materials for Trim");
        check(transferItem is not null && transferItem.IsEnabled, "Wall right-click menu exposes Use these materials for Trim");
        transferItem!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        check(vm.TrimPlannerFilter.Selection.Filter.Materials.ToHashSet().SetEquals(["Brick: Earthy", "Wood: Ashen"])
            && !vm.TrimPlannerFilter.MatchAllMaterials, "Mixed wall transfer retains both paired finishes and matches either material");
        check(vm.BuildTrimSampleAssets.Count > 0 && vm.BuildTrimSampleAssets.All(t => SourceTaxonomy.Parse(t.Asset).Materials
            .Any(m => m.Key is "Wood: Ashen" or "Brick: Earthy")), "Transferred Trim includes only matching material finishes");
        check(vm.BuildTrimSampleAssets.Any(t => SourceTaxonomy.Parse(t.Asset).Materials.Count == 1),
            "Mixed wall transfer allows single-material door/window trim");
        check(JsonSerializer.Serialize(vm.WallsPlannerFilter.Selection) == wallBefore
            && JsonSerializer.Serialize(vm.FloorPlannerFilter.Selection) == floorBefore,
            "Wall-to-Trim transfer leaves wall and floor choices unchanged");
        var selectedTrim = vm.TrimPlannerFilter.Selection;
        check(!vm.UseWallMaterialsForTrim(floor) && vm.TrimPlannerFilter.Selection.Filter.Materials.SequenceEqual(selectedTrim.Filter.Materials),
            "Floor assets cannot trigger the wall-to-Trim action");
        vm.TrimPlannerFilter.SetBiomes(["Astral"]);
        vm.TrimPlannerFilter.Tags = "nonmatching-transfer-regression-tag";
        vm.TrimPlannerFilter.MatchAllTags = true;
        vm.TrimPlannerFilter.ExcludeAdditionalMaterials = true;
        var constraints = vm.TrimPlannerFilter.Selection.Filter;
        vm.UseWallMaterialsForTrim(wall);
        vm.TrimPlannerFilter.RefreshChoices();
        var transferred = vm.TrimPlannerFilter.Selection.Filter;
        check(transferred.Materials.ToHashSet().SetEquals(["Brick: Earthy", "Wood: Ashen"])
            && vm.TrimPlannerFilter.MatchingCount == 0,
            "Unavailable transferred finishes remain selected across refresh and yield zero rather than All");
        check(transferred.Biomes.SequenceEqual(constraints.Biomes) && transferred.Collection == constraints.Collection
            && transferred.Tags.SequenceEqual(constraints.Tags) && transferred.AllTags == constraints.AllTags
            && transferred.Variant == constraints.Variant && transferred.ExcludeAdditionalMaterials,
            "Transfer preserves Trim biome, settlement, tags, variant, and exclusion setting");
        var restored = new PlannerFilterViewModel(BuildComponent.Trim, () => SourcePlannerCatalog.Empty, () => "All");
        restored.Restore(vm.TrimPlannerFilter.Selection);
        check(restored.Selection.Filter.Materials.ToHashSet().SetEquals(transferred.Materials),
            "Missing transferred materials survive saved-selection restoration");

        vm.WallsPlannerFilter.Restore(wallState);
        vm.FloorPlannerFilter.Restore(floorState);
        vm.TrimPlannerFilter.Restore(trimState);
        vm.TrimPlannerFilter.SelectMaterials(trimState.Filter.Materials, trimState.Filter.AllMaterials);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
