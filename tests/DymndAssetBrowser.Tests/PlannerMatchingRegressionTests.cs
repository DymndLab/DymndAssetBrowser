using System.Text.Json;
using System.Windows.Controls;
using System.Reflection;
using DymndAssetBrowser.App;
using DymndAssetBrowser.App.ViewModels;
using DymndAssetBrowser.App.Infrastructure;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using DymndAssetBrowser.Core.Models;

internal static class PlannerMatchingRegressionTests
{
    public static void Core(Func<string, AssetRecord> asset, Action<bool, string> check)
    {
        var sample = asset(@"Desert\Base_Desert_Settlement\Structures\Building\Walls\Wall_Adobe_Red_A_Corner_A1.png");
        check(SourceTaxonomy.Parse(sample).Materials.Single().Key == "Adobe: Red", "Adobe wall has transferable material and finish metadata");
        check(SourceTaxonomy.Parse(asset(@"Desert\Base_Desert_Settlement\Structures\Arches\Arch_Adobe_Red_Single_A1.png")).Materials.Single().Key == "Adobe: Red",
            "Adobe trim uses the same material vocabulary as walls");
        MaterialTag[][] palettes = [[new("Brick", "Earthy")], [new("Wood", "Ashen")], [new("Brick", "Earthy"), new("Wood", "Ashen")],
            [new("Brick", "Earthy"), new("Wood", "Light")], [new("Wood", "Ashen"), new("Metal")], [new("Wood")], []];
        var entries = palettes.Select((p, i) => new SourceBrowserIndex.Entry(sample with { FilePath = $"C:\\{i}.png" },
            new("Base", "Settlement", "Base", [], p, []), [])).ToArray();
        var index = new SourceBrowserIndex(entries);
        var filter = new SourceBrowserFilter { Materials = ["Brick: Earthy", "Wood: Ashen"], ExcludeAdditionalMaterials = true, ExactMaterialFinishes = true };
        check(index.Filter(filter).SequenceEqual(entries.Take(3)), "Wall Trim palette permits each matching subset and exact mixed combo, rejecting extra finishes/materials and unknowns");
        check(index.Filter(filter with { AllMaterials = true }).SequenceEqual(entries.Skip(2).Take(1)), "Trim sample combo requires both finishes, not single-material subsets");
        check(index.Filter(filter with { Materials = ["Wood"] }).SequenceEqual(new[] { entries[1], entries[5] }), "Explicit Any Wood finish remains broad in exact palettes");
        var vm = new PlannerFilterViewModel(BuildComponent.Trim, () => SourcePlannerCatalog.Empty, () => "All");
        check(vm.CanReceiveWallMaterials, "Fresh Trim accepts automatic wall materials");
        vm.SelectMaterials(filter.Materials, false, exactFinishes: true, followWalls: true);
        var saved = JsonSerializer.Deserialize<PlannerSourceSelection>(JsonSerializer.Serialize(vm.Selection))!;
        vm.Restore(saved);
        check(vm.CanReceiveWallMaterials && vm.Selection.Filter.ExactMaterialFinishes && vm.Selection.MaterialsFollowWalls,
            "Automatic Trim provenance and exact finish rules survive saving/restoring");
        vm.Tags = "custom";
        check(!vm.CanReceiveWallMaterials, "Manual Trim filtering takes ownership from automatic defaults");
        vm.Reset();
        vm.SetBiomes(["Unavailable biome"]); vm.CollectionFacet.Selected = "Unavailable settlement"; vm.VariantFacet.Selected = "Z99";
        vm.RefreshChoices();
        check(vm.Selection.Filter.Biomes.SequenceEqual(["Unavailable biome"]) && vm.CollectionFacet.Selected == "Unavailable settlement" && vm.VariantFacet.Selected == "Z99",
            "Planner keeps unavailable shared facets instead of broadening to All");
        check(vm.CollectionFacet.Options.Count(v => v == "All") == 1 && vm.VariantFacet.Options.Count(v => v == "All") == 1,
            "Retaining unavailable facets does not duplicate All choices");
    }

    public static async Task Run(MainViewModel vm, MainWindow window, Action<bool, string> check)
    {
        var states = new[] { vm.WallsPlannerFilter.Selection, vm.FloorPlannerFilter.Selection, vm.TrimPlannerFilter.Selection };
        try
        {
            await vm.ResetBuildAsync();
            var wall = vm.BuildWallSampleAssets.First(t => t.Asset.FileName.Contains("BrickWood_Earthy_Ashen"));
            var floorBefore = JsonSerializer.Serialize(vm.FloorPlannerFilter.Selection);
            var floorCount = vm.BuildFloorSampleAssets.Count;
            check(vm.SelectPlannerSample(wall), "Fresh planner wall click succeeds");
            var f = vm.TrimPlannerFilter.Selection.Filter;
            check(f.Materials.ToHashSet().SetEquals(["Brick: Earthy", "Wood: Ashen"]) && !f.AllMaterials && f.ExactMaterialFinishes
                && vm.TrimPlannerFilter.Selection.MaterialsFollowWalls, "Wall click automatically supplies the exact paired finish palette to untouched Trim");
            var trims = vm.BuildTrimSampleAssets.ToArray();
            check(trims.Length > 0 && trims.All(t => SourceTaxonomy.Parse(t.Asset).Materials.All(m => f.Materials.Contains(m.Key)))
                && trims.Any(t => SourceTaxonomy.Parse(t.Asset).Materials.Count == 1), "Real-library automatic Trim includes single-material subsets and excludes unrelated finishes");
            check(JsonSerializer.Serialize(vm.FloorPlannerFilter.Selection) == floorBefore && vm.BuildFloorSampleAssets.Count == floorCount,
                "Ordinary wall click leaves Floor filtering and count unchanged");
            vm.WallsPlannerFilter.Reset();
            var adobe = vm.BuildWallSampleAssets.First(t => t.Asset.FileName.Contains("Wall_Adobe_Red_"));
            vm.SelectPlannerSample(adobe);
            check(vm.TrimPlannerFilter.Selection.Filter.Materials.SequenceEqual(["Adobe: Red"]) && vm.BuildTrimSampleAssets.Count > 0,
                "Automatically managed Trim follows a later Adobe wall choice");
            var adobePiece = vm.BuildWallAssets.First();
            var menu = new Border { DataContext = adobePiece, ContextMenu = new() };
            typeof(MainWindow).GetMethod("Asset_ContextMenuOpening", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [menu, null]);
            check(menu.ContextMenu.Items.OfType<MenuItem>().Any(i => (string?)i.Header == "Use these materials for Trim" && i.IsEnabled),
                "Actual Adobe wall right-click exposes the enabled Trim transfer action");
            vm.TrimPlannerFilter.Tags = "manual-trim-choice";
            var manual = JsonSerializer.Serialize(vm.TrimPlannerFilter.Selection);
            vm.WallsPlannerFilter.Reset(); vm.SelectPlannerSample(wall);
            check(JsonSerializer.Serialize(vm.TrimPlannerFilter.Selection) == manual, "Wall selection preserves manually customized Trim");
            check(vm.UseWallMaterialsForTrim(vm.BuildWallAssets.First()) && vm.TrimPlannerFilter.Tags == "manual-trim-choice",
                "Explicit transfer replaces materials while preserving other manually chosen Trim filters");

            vm.TrimPlannerFilter.Reset();
            var trim = vm.BuildTrimSampleAssets.First(t => SourceTaxonomy.Parse(t.Asset).Materials.Count == 2);
            var keys = SourceTaxonomy.Parse(trim.Asset).Materials.Select(m => m.Key).ToHashSet();
            check(vm.SelectPlannerSample(trim) && vm.TrimPlannerFilter.MatchAllMaterials && vm.TrimPlannerFilter.Selection.Filter.ExactMaterialFinishes,
                "Preview Trim click chooses its exact material combination");
            check(vm.BuildTrimSampleAssets.Count > 0 && vm.BuildTrimSampleAssets.All(t => SourceTaxonomy.Parse(t.Asset).Materials.Select(m => m.Key).ToHashSet().SetEquals(keys)),
                "Trim sample results contain precisely the clicked material combo");
            vm.TrimPlannerFilter.Reset();
            // Let two-way WPF picker bindings settle after the rapid test edits.
            await Task.Delay(250);
            await vm.PopulateBuildAsync(saveState: false);
            check(vm.BuildDoorFrameAssets.Count + vm.BuildWindowSillAssets.Count + vm.BuildOtherOpeningAssets.Count > 0,
                "Populated Trim contains assets: " + vm.StatusText);
            trim = vm.BuildDoorFrameAssets.Concat(vm.BuildWindowSillAssets).Concat(vm.BuildOtherOpeningAssets)
                .First(t => SourceTaxonomy.Parse(t.Asset).Materials.Count > 0);
            check(vm.SelectPlannerSample(trim) && vm.TrimPlannerFilter.Selection.Filter.ExactMaterialFinishes,
                "Trim clicking also works after Populate Build");

            await vm.ResetBuildAsync(); vm.SelectPlannerSample(wall);
            var floor = vm.BuildFloorSampleAssets.First(); vm.SelectPlannerSample(floor);
            trim = vm.BuildTrimSampleAssets.First(t => SourceTaxonomy.Parse(t.Asset).Materials.Any(m => m.Key == "Wood: Ashen"));
            check(vm.ApplyPlannerFilter(trim, "Material:Wood: Ashen"), "Filter Planner to works from a Trim tile");
            check(new[] { vm.WallsPlannerFilter, vm.FloorPlannerFilter, vm.TrimPlannerFilter }.All(v => !v.HasPinnedSelection
                && v.Selection.Filter.Materials.SequenceEqual(["Wood: Ashen"])), "Shared material filter updates Walls, Floor and Trim and clears all exact pins");
            await vm.ResetBuildAsync();
            adobe = vm.BuildWallSampleAssets.First(t => t.Asset.FileName.Contains("Wall_Adobe_Red_"));
            check(vm.ApplyPlannerFilter(adobe, "Biome") && new[] { vm.WallsPlannerFilter, vm.FloorPlannerFilter, vm.TrimPlannerFilter }
                .All(v => v.SelectedBiomes.SequenceEqual(["Desert"])), "Wall right-click biome applies across all three sections");
            check(vm.ApplyPlannerFilter(adobe, "Collection") && new[] { vm.WallsPlannerFilter, vm.FloorPlannerFilter, vm.TrimPlannerFilter }
                .All(v => v.CollectionFacet.Selected == "Base"), "Wall right-click settlement applies across all three sections");
            var targets = new[] { new AssetFilterTarget("Walls"), new AssetFilterTarget("Floor"), new AssetFilterTarget("Trim") };
            var beforeWall = JsonSerializer.Serialize(vm.WallsPlannerFilter.Selection);
            var beforeFloor = JsonSerializer.Serialize(vm.FloorPlannerFilter.Selection);
            vm.TrimPlannerFilter.Tags = "obsolete-filter";
            var closed = false;
            var filterMenu = AssetFilterMenu.Create(vm.PlannerFilterOptions(adobe),
                keys => vm.ApplyPlannerFilters(adobe, keys, targets.Where(t => t.IsSelected).Select(t => Enum.Parse<BuildComponent>(t.Label))),
                () => closed = true, () => ModifierKeys.Control, targets);
            var row = (StackPanel)filterMenu.Items.OfType<MenuItem>().Single(i => i.Header is StackPanel).Header;
            foreach (var box in row.Children.OfType<CheckBox>().Where(b => (string)b.Content != "Trim"))
            { box.IsChecked = false; box.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); }
            foreach (var key in new[] { "Biome", "Material:Adobe: Red" })
                filterMenu.Items.OfType<MenuItem>().Single(i => (string?)i.Tag == key).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            check(!closed && vm.TrimPlannerFilter.Tags == "obsolete-filter", "Planner Ctrl-click queues characteristics without applying early");
            var apply = filterMenu.Items.OfType<MenuItem>().Single(i => i.Header is string s && s.StartsWith("Apply selected"));
            check(apply.IsEnabled && (string)apply.Header == "Apply selected (2)", "Shared filter menu displays its queued characteristic count");
            apply.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            check(closed && vm.TrimPlannerFilter.SelectedBiomes.SequenceEqual(["Desert"]) && vm.TrimPlannerFilter.Tags == ""
                && vm.TrimPlannerFilter.Selection.Filter.Materials.SequenceEqual(["Adobe: Red"]), "Apply selected replaces only Trim with both queued characteristics");
            check(JsonSerializer.Serialize(vm.WallsPlannerFilter.Selection) == beforeWall && JsonSerializer.Serialize(vm.FloorPlannerFilter.Selection) == beforeFloor,
                "Unchecked Walls and Floor retain their selections exactly");
            check(vm.ApplyPlannerFilters(adobe, ["Material:Adobe: Red"], [BuildComponent.Floor, BuildComponent.Trim])
                && vm.FloorPlannerFilter.Selection.Filter.Materials.SequenceEqual(["Adobe: Red"])
                && vm.TrimPlannerFilter.Selection.Filter.Materials.SequenceEqual(["Adobe: Red"])
                && JsonSerializer.Serialize(vm.WallsPlannerFilter.Selection) == beforeWall, "Floor plus Trim target selection leaves Walls untouched");
            var variant = vm.PlannerFilterOptions(adobe).First(o => o.Key == "Variant");
            var tag = vm.PlannerFilterOptions(adobe).First(o => o.IsTag);
            vm.ApplyPlannerFilters(adobe, [variant.Key, tag.Key], [BuildComponent.Floor]);
            check(vm.FloorPlannerFilter.VariantFacet.Selected == variant.Value && vm.FloorPlannerFilter.Selection.Filter.Tags.SequenceEqual([tag.Value])
                && vm.FloorPlannerFilter.Selection.Filter.Materials.Length == 0, "Planner shortcuts include variant and tags with Browser-style replacement semantics");
            var beforeNone = JsonSerializer.Serialize(vm.FloorPlannerFilter.Selection);
            check(!vm.ApplyPlannerFilters(adobe, ["Biome"], []) && JsonSerializer.Serialize(vm.FloorPlannerFilter.Selection) == beforeNone,
                "No selected target makes no changes");
        }
        finally
        {
            vm.WallsPlannerFilter.Restore(states[0]); vm.FloorPlannerFilter.Restore(states[1]); vm.TrimPlannerFilter.Restore(states[2]);
            // Refresh the restored palette without persisting into the user's live state.
            await Task.Delay(250);
            await vm.PopulateBuildAsync(saveState: false);
            vm.TrimPlannerFilter.SelectMaterials(states[2].Filter.Materials, states[2].Filter.AllMaterials,
                states[2].Filter.ExactMaterialFinishes, states[2].MaterialsFollowWalls);
        }
    }
}
