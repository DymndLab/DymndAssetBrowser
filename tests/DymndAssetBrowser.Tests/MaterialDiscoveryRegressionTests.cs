using DymndAssetBrowser.Core.Models;
using DymndAssetBrowser.App.ViewModels;

internal static class MaterialDiscoveryRegressionTests
{
    public static async Task Run(MainViewModel vm, Action<bool, string> check)
    {
        var states = new[] { vm.WallsPlannerFilter.Selection, vm.FloorPlannerFilter.Selection, vm.TrimPlannerFilter.Selection };
        try
        {
            await vm.ResetBuildAsync();
            vm.WallsPlannerFilter.SelectMaterials(["Wood"]);
            check(vm.PlannerWallSets.Count == 181 && vm.PlannerWallSets.All(s => s.WallSystem != "Adobe"), "Real-library Wood filter excludes all nine Adobe shelf-bearing sets");
            check(vm.PlannerWallSets.Any(s => s.WallSystem == "Brick Wood") && vm.PlannerWallSets.Any(s => s.WallSystem == "Stone Wood"), "Real mixed wall construction remains discoverable under Wood");
            vm.WallsPlannerFilter.Reset();
            vm.WallsPlannerFilter.SelectMaterials(["Adobe: Red"]);
            var sample = vm.BuildWallSampleAssets.First(t => t.Asset.FileName.Contains("Wide"));
            check(vm.SelectPlannerSample(sample), "Adobe Wide sample selects its complete set");
            var shelf = vm.BuildWallAssets.First(t => t.Asset.FileName.Contains("Shelf_Wood"));
            check(vm.BuildWallAssets.Any(t => t.Asset.FileName.Contains("Wide_Window_A1")) && vm.BuildWallAssets.Any(t => t.Asset.FileName.Contains("Wide_Window_A2")), "Both Adobe window segments remain available as wall pieces");
            check(vm.WallMaterialsForTrim(shelf).SequenceEqual(["Adobe: Red"]), "Clicking an Adobe shelf uses the wall construction palette for Trim");
            check(vm.TrimPlannerFilter.Selection.Filter.Materials.SequenceEqual(["Adobe: Red"]), "Automatic Trim defaults exclude incidental shelf wood");
            var options = vm.PlannerFilterOptions(shelf);
            check(options.Any(o => o.Key == "Material:Adobe: Red") && !options.Any(o => o.Key.StartsWith("Material:Wood")), "Planner shelf context menu uses consistent construction materials");
            vm.TrimPlannerFilter.Reset();
            check(vm.UseWallMaterialsForTrim(shelf) && vm.TrimPlannerFilter.Selection.Filter.Materials.SequenceEqual(["Adobe: Red"]), "Explicit shelf-to-Trim transfer also excludes incidental wood");
            check(vm.ApplyPlannerFilters(shelf, ["Material:Adobe: Red"], [BuildComponent.Floor, BuildComponent.Trim])
                && vm.FloorPlannerFilter.Selection.Filter.Materials.SequenceEqual(["Adobe: Red"]), "Planner right-click construction material applies to chosen targets");
            vm.FloorPlannerFilter.Reset();
            vm.FloorPlannerFilter.SetBiomes(["Desert"]);
            check(vm.FloorPlannerFilter.MaterialGroups.All(g => g.Name != "Tile"), "Desert floor picker no longer offers Tile as a construction material");
            vm.FloorPlannerFilter.SelectMaterials(["Stone: Slate"], exactFinishes: true);
            check(vm.BuildFloorSampleAssets.Count == 10 && vm.BuildFloorSampleAssets.All(t => SourceTaxonomy.Parse(t.Asset).Materials.Single().Key == "Stone: Slate"), "Desert Slate filters to nine stone tile textures plus the matching substrate");
            vm.FloorPlannerFilter.SelectMaterials(["Ceramic: Sandstone"], exactFinishes: true);
            check(vm.BuildFloorSampleAssets.Count > 0 && vm.BuildFloorSampleAssets.All(t => SourceTaxonomy.Parse(t.Asset).Materials.Single().Material == "Ceramic"), "Ceramic Sandstone remains distinct from stone flooring in Planner");
        }
        finally
        {
            vm.WallsPlannerFilter.Restore(states[0]); vm.FloorPlannerFilter.Restore(states[1]); vm.TrimPlannerFilter.Restore(states[2]);
            await Task.Delay(250);
            await vm.PopulateBuildAsync(saveState: false);
            vm.TrimPlannerFilter.SelectMaterials(states[2].Filter.Materials, states[2].Filter.AllMaterials,
                states[2].Filter.ExactMaterialFinishes, states[2].MaterialsFollowWalls);
        }
    }

    public static void Core(Func<string, AssetRecord> asset, Action<bool, string> check)
    {
        const string prefix = @"Desert\Base_Desert_Settlement\Structures\Building\Walls_and_Curbs\Wall_Adobe_Wide_A\";
        var path = asset(prefix + "Wall_Adobe_Red_Wide_A_Path.png");
        var corner = asset(prefix + "Wall_Adobe_Red_Wide_Corner_A1_1x1.png");
        var shelf = asset(prefix + "Wall_Adobe_Red_Wide_Shelf_Wood_Red_A1_2x1.png");
        var window = asset(prefix + "Wall_Adobe_Red_Wide_Window_A1_1x1.png");
        var window2 = asset(prefix + "Wall_Adobe_Red_Wide_Window_A2_1x1.png");
        var wood = asset(@"!Core_Settlements\Structures\Building\Walls_and_Curbs\Wall_Wood_A\Wall_Wood_Ashen_A_Path.png");
        var brickWood = asset(@"!Core_Settlements\Structures\Building\Walls_and_Curbs\Wall_BrickWood_A\Wall_BrickWood_Earthy_Ashen_A_Path.png");
        var stoneWood = asset(@"!Core_Settlements\Structures\Building\Walls_and_Curbs\Wall_StoneWood_A\Wall_StoneWood_Slate_Ashen_A_Path.png");
        var assets = new[] { path, corner, shelf, window, window2, wood, brickWood, stoneWood };
        var source = new SourceBrowserIndex(assets.Select(a => { var t = SourceTaxonomy.Parse(a); return new SourceBrowserIndex.Entry(a, t, t.Tags); }));
        var walls = WallConstructionCatalog.Build(assets);
        var catalog = new SourcePlannerCatalog(source, new(assets), walls);
        var adobe = walls.Sets.Single(s => s.WallSystem == "Adobe");
        IReadOnlyList<WallConstructionSet> Match(params string[] materials) => catalog.WallSets(new() { Filter = new() { Materials = materials } }, "All");
        check(Match("Wood").Count == 3 && Match("Wood").All(s => s.WallSystem != "Adobe"), "Wood discovers pure and mixed wood wall sets, not Adobe shelf accessories");
        check(Match("Wood: Red").Count == 0, "An accessory finish cannot make a wall set match");
        check(Match("Adobe: Red").Single().WallPieces.Count == 5, "Adobe selection retains corners, wooden shelves and both window segments");
        check(source.Filter(new() { Materials = ["Wood: Red"] }).Single().Asset == shelf, "Individual Browser shelf metadata retains its real wood component");
        check(catalog.ConstructionMaterials(adobe.Id).Single().Key == "Adobe: Red", "Trim-transfer construction palette excludes incidental wood");
        check(catalog.Scope(BuildComponent.Walls).Entries.Where(e => adobe.WallPieces.Contains(e.Asset)).All(e => e.Taxonomy.Materials.Single().Key == "Adobe: Red"),
            "All members expose the same wall construction facets to Planner");
        check(catalog.Scope(BuildComponent.Trim).Entries.All(e => e.Asset != window && e.Asset != window2), "Wall-matching window segments remain Walls, not Trim");
        check(catalog.WallSets(new() { Filter = new() { Materials = ["Wood"], ExcludeAdditionalMaterials = true } }, "All").Single().WallSystem == "Wood", "Wood-only excludes genuine mixed construction");
        check(catalog.WallSets(new() { Filter = new() { Materials = ["Stone: Slate", "Wood: Ashen"], AllMaterials = true } }, "All").Single().WallSystem == "Stone Wood",
            "Match-all preserves paired Stone and Wood finishes");
        check(catalog.WallSets(new() { Filter = new() { Materials = ["Adobe", "Wood"], AllMaterials = true } }, "All").Count == 0, "Match-all does not mistake Adobe shelving for a composite wall system");
        check(catalog.WallSets(new() { Filter = new() { Materials = ["Adobe"], Tags = ["Shelf"] } }, "All").Single().WallPieces.Count == 5,
            "Piece tags can still discover and return the entire construction set");
        var picker = new PlannerFilterViewModel(BuildComponent.Walls, () => catalog, () => "All");
        picker.RefreshChoices(); picker.SelectMaterials(["Adobe"]); picker.MatchAllMaterials = true;
        check(!picker.MaterialGroups.Single(g => g.Name == "Wood").Parent.HasMatches, "Planner disables wood when it cannot match the Adobe construction filter");

        var noAnchorAssets = new[] { shelf, corner, window };
        var noAnchorSource = new SourceBrowserIndex(source.Entries.Where(e => noAnchorAssets.Contains(e.Asset)));
        var noAnchorWalls = WallConstructionCatalog.Build(noAnchorAssets);
        var noAnchor = new SourcePlannerCatalog(noAnchorSource, new(noAnchorAssets), noAnchorWalls);
        check(noAnchor.ConstructionMaterials(noAnchorWalls.Sets.Single().Id).Single().Key == "Adobe: Red"
            && noAnchor.WallSets(new() { Filter = new() { Materials = ["Wood"] } }, "All").Count == 0,
            "Sets without a path use common materials rather than the first shelf member");

        // A shared member must not transfer a match between sets with different anchors.
        var sharedSets = new WallConstructionCatalog([
            adobe with { Id = "red", WallPieces = [path, shelf] },
            adobe with { Id = "wood", WallPieces = [wood, shelf] }
        ]);
        var sharedCatalog = new SourcePlannerCatalog(source, new(assets), sharedSets);
        check(sharedCatalog.WallSets(new() { Filter = new() { Materials = ["Wood"] } }, "All").Single().Id == "wood",
            "Shared asset identities cannot leak a material match into another wall set");

        foreach (var biome in new[] { "Desert", "Mountain", "Underdark" })
        foreach (var finish in new[] { "Sandstone", "Slate", "Terracotta", "White" })
        {
            var floor = asset($@"{biome}\Base_{biome}_Settlement\Textures\Stone_Tiles\Stone_Tile_Large_{finish}_Cracked_A.jpg");
            var taxonomy = SourceTaxonomy.Parse(floor);
            check(taxonomy.Materials.Single().Key == "Stone: " + finish, $"{biome} stone tile pairs {finish} without a Tile material");
            check(taxonomy.Tags.Contains("Tile") && taxonomy.Tags.Contains("Cracked"), "Tile shape and condition remain searchable tags: " + biome + " " + finish);
        }
        foreach (var finish in new[] { "Sandstone", "Slate", "PatternRed", "PatternB_Gold" })
        {
            var ceramic = asset($@"Desert\Base_Desert_Settlement\Textures\Ceramic_Tiles\Ceramic_Tile_Medium_{finish}_A.jpg");
            var materials = SourceTaxonomy.Parse(ceramic).Materials;
            check(materials.Count == 1 && materials[0].Material == "Ceramic" && materials[0].Finish.Length > 0,
                "Ceramic color is not converted into stone or metallic composition: " + finish);
        }
        var unknown = asset(@"Custom\Tile_Floor_White_A.png");
        foreach (var name in new[] { "Herringbone_Dirt_A.jpg", "Rectangular_Tiles_A_Dirt.jpg", "Cobblestone_A_Dirt1.jpg", "Flat_Stone_Floor_A_Dirt.jpg" })
        {
            var dirtyStone = SourceTaxonomy.Parse(asset(@"!Core_Settlements\Textures\Stone_Floors\" + name));
            check(dirtyStone.Materials.Single().Key == "Stone: Dirt" && dirtyStone.Tags.Contains("Dirt"), "Stone floor collection retains stone composition and dirty finish: " + name);
        }
        check(SourceTaxonomy.Parse(asset(@"Desert\Wilderness\Textures\Dirt\Dirt_Red_A.jpg")).Materials.Single().Key == "Dirt: Red", "Actual dirt terrain remains Dirt");
        var genericDirty = asset(@"Custom\Textures\Stone_Floors\Herringbone_Dirt_A.jpg") with { Group = "Building", SubGroup = "Floors" };
        check(SourceTaxonomy.Parse(genericDirty, false).Materials.Single().Material == "Dirt", "FA dirty-stone folder convention is not imposed on generic libraries");
        check(SourceTaxonomy.Parse(unknown, false).Materials.Single().Material == "Tile", "An unspecified tile is not guessed to be stone");
        var genericStone = asset(@"Custom\Stone_Tile_Slate_A.png");
        check(SourceTaxonomy.Parse(genericStone, false).Materials.Single().Key == "Stone: Slate", "Explicit stone composition works in non-FA filenames too");
        var mixedTile = asset(@"Custom\Stone_Tile_Slate_Wood_Ashen_A.png");
        check(SourceTaxonomy.Parse(mixedTile, false).Materials.Select(m => m.Key).ToHashSet().SetEquals(["Stone: Slate", "Wood: Ashen"]), "Removing Tile retains genuine additional materials");
    }
}
