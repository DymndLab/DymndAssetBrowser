using FAFamilyBrowser.App;
using FAFamilyBrowser.App.Services;
using FAFamilyBrowser.App.ViewModels;
using FAFamilyBrowser.Core.Indexing;
using FAFamilyBrowser.Core.Models;
using FAFamilyBrowser.Core.Parsing;
using FAFamilyBrowser.Core.Persistence;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

var failures = new List<string>();
var faParser = new FaAssetFilenameParser();
var root = @"C:\FA";
AssetRecord ParseFa(string relative) => faParser.Parse(root, Path.Combine(root, relative)) with { SourceId = LibrarySourceIds.ForgottenAdventures };

var brick = ParseFa(@"!Core_Settlements\Structures\Building\Walls_and_Curbs\Wall_Brick_Earthy_A_Corner_A_1x1.png");
Check(brick, "Building", "Walls", "Brick", "Earthy", "Unspecified", "Unspecified", "A");
var compound = ParseFa(@"Desert\Town\Structures\Building\Walls_and_Curbs\Wall_Brick_Plaster_Earthy_C1_Straight_F_4x1.png");
Check(compound, "Building", "Walls", "Brick_Plaster", "Earthy", "Desert", "Unspecified", "C1");
var compactCompound = ParseFa(@"Structures\Building\Walls_and_Curbs\Wall_StoneWood_Earthy_A1_Straight_A_1x1.png");
Expect(compactCompound.Material == "Stone_Wood", "Compact compound material token was not normalized.");
var stoneWoodDualFinish = ParseFa(@"Structures\Building\Walls_and_Curbs\Wall_StoneWood_Sandstone_Light_A_Straight_A_1x1.png");
Expect(stoneWoodDualFinish.Material == "Stone_Wood" && stoneWoodDualFinish.Style == "Sandstone"
    && stoneWoodDualFinish.Family == "Light" && stoneWoodDualFinish.Variant == "A",
    "Stone+Wood did not preserve its first finish as Style and second finish as Family.");
var brickWoodDualFinish = ParseFa(@"Structures\Building\Walls_and_Curbs\Wall_BrickWood_Earthy_Ashen_C_Connector_A_1x1.png");
Expect(brickWoodDualFinish.Material == "Brick_Wood" && brickWoodDualFinish.Style == "Earthy"
    && brickWoodDualFinish.Family == "Ashen" && brickWoodDualFinish.Variant == "C",
    "Brick+Wood did not preserve its first finish as Style and second finish as Family.");
var stoneMetalDualFinish = ParseFa(@"Structures\Building\Walls_and_Curbs\Wall_StoneMetal_Earthy_Gray_A1_Connector_A_1x1.png");
Expect(stoneMetalDualFinish.Material == "Stone_Metal" && stoneMetalDualFinish.Style == "Earthy"
    && stoneMetalDualFinish.Family == "Gray" && stoneMetalDualFinish.Variant == "A1",
    "Stone+Metal did not preserve its first finish as Style and second finish as Family.");
var patternVariant = ParseFa(@"Structures\Shelters\Tarps\Tarp_Cloth_Red_Pattern_A_A36_3x5.png");
Expect(patternVariant.Variant == "A36", "Pattern label was mistaken for the asset Variant.");
var extendedMaterial = ParseFa(@"Clutter\Clothing\Headwear\Hat_Fur_Gray_A1.png");
Expect(extendedMaterial.Material == "Fur", "Authoritative FA material vocabulary omitted Fur.");
var unknown = ParseFa(@"Horror\Town\Structures\Building\Walls_and_Curbs\Wall_Mystic_Alloy_Redrock_B_Connector_DIAG_A_1x1.png");
Check(unknown, "Building", "Walls", "Unspecified", "Redrock", "Horror", "Unspecified", "B");
Expect(unknown.PartType == "Diagonal", "Wall connector part type was not retained.");

var taxonomyCases = new (string Path, string Group, string SubGroup, string Family)[]
{
    (@"Aquatic\Vehicles\Canoes\Canoe_Wood_Ashen_A1.png", "Transport", "Marine", "Canoes"),
    (@"Aquatic\Vehicles\Boats\Boat_Wood_Earthy_A1.png", "Transport", "Marine", "Boats"),
    (@"Aquatic\Vehicles\Ships\Ship_Wood_Walnut_B2.png", "Transport", "Marine", "Ships"),
    (@"Aquatic\Vehicles\Sails_&_Rigging\Sail_Cloth_Ashen_A1.png", "Transport", "Marine", "Sails & Rigging"),
    (@"Aquatic\Vehicles\Docks\Dock_Wood_Earthy_A.png", "Transport", "Marine", "Marine Infrastructure"),
    (@"!Core_Settlements\Vehicles\Carts_&_Wagons\Cart_Wood_Ashen_A.png", "Transport", "Land", "Carts & Wagons"),
    (@"Arctic\Vehicles\Sleds_&_Sleighs\Sled_Wood_Frosty_A.png", "Transport", "Land", "Sleds & Sleighs"),
    (@"!Core_Settlements\Workplace_Equipment\Mining\Mine_Carts\Minecart_Metal_Rusty_A.png", "Transport", "Mining", "Mine Carts"),
    (@"!Core_Settlements\Workplace_Equipment\Mining\Mine_Tracks\Track_Wood_Ashen_Path.png", "Transport", "Mining", "Mine Tracks"),
    (@"!Core_Settlements\Workplace_Equipment\Mining\Drillcarts\Dwarven_Drillcart_Metal_Rusty_A1.png", "Transport", "Mining", "Drill Carts"),
    (@"!Core_Settlements\Workplace_Equipment\Mining\Mine_Carts\Mine_Cart_Fills\Ore\Ore_Minecart_Fill_Gold_A1.png", "Transport", "Mining", "Mining Fill"),
    (@"!Core_Settlements\Workplace_Equipment\Mining\Mine_Carts\Mine_Cart_Fills\Coal\Coal_Spill_01_A.png", "Transport", "Mining", "Mining Fill"),
    (@"Furniture\Seating\Chair_Wood_Walnut_A1.png", "Props", "Furniture", "Seating"),
    (@"Decor\Rugs\Rug_Cloth_Red_A.png", "Props", "Decor", "Unspecified"),
    (@"Workplace_Equipment\Smithing\Anvil_Metal_Rusty_A.png", "Props", "Workplace", "Unspecified"),
    (@"Structures\Shelters\Tent_Cloth_Ashen_A.png", "Building", "Shelters", "Unspecified"),
    (@"Structures\Rubble\Rubble_Stone_Earthy_A.png", "Building", "Ruins & Rubble", "Unspecified"),
    (@"Structures\Water_Structures\Fountain_Stone_Earthy_A.png", "Building", "Water Features", "Unspecified"),
    (@"Structures\Mechanical_Parts\Cog_Metal_Rusty_A.png", "Building", "Infrastructure", "Unspecified"),
    (@"Structures\Building\Pillars\Pillar_Stone_Earthy_A.png", "Building", "Pillars & Supports", "Unspecified"),
    (@"Structures\Building\Stairs_and_Ladders\Stairs_Wood_Ashen_A.png", "Building", "Stairs", "Unspecified")
    ,(@"Burial_and_Graves\Sarcophagi\Sarcophagus_Stone_Earthy_A.png", "Props", "Decor", "Burial & Graves")
    ,(@"Horror\!Wilderness\Paths\Intestines\Flesh_Pale_Intestines_Path_B1.png", "Nature", "Terrain", "Paths")
    ,(@"Horror\!Wilderness\Pillars\Flesh_Purple_Brain_Pillar_A1.png", "Building", "Pillars & Supports", "Unspecified")
    ,(@"Astral\Ceremorph_Settlement\Structures\Aesthetics\Ceremorph_Face_Ornament_Gray_A1.png", "Props", "Decor", "Unspecified")
    ,(@"Astral\Ceremorph_Settlement\Structures\Tentacles\Giant_Tentacles\Ceremorph_Tentacle_Green_B4.png", "Props", "Decor", "Unspecified")
    ,(@"Structures\Statues\Statue_Stone_Sandstone_A.png", "Props", "Decor", "Statues")
    ,(@"Natural_Decor\_Old_Crops\Corn\Dead_Corn_Row_A2.png", "Nature", "Flora", "Plants")
    ,(@"Structures\Building\Palisades\Wall_Palisade_Ashen_A.png", "Building", "Defenses & Barricades", "Palisades")
    ,(@"Aquatic\!Wilderness\Textures\Water\Water_Opaque_A.jpg", "Nature", "Water", "Unspecified")
    ,(@"Vehicles\Carts_and_Wagons\Cart_Wood_Ashen_A.png", "Transport", "Land", "Carts & Wagons")
    ,(@"Structures\Building\Grates\Floor_Grate_Metal_Rusty_A.png", "Building", "Floors", "Floor Grates")
    ,(@"Workplace_Equipment\Farming\Crops\Pumpkins\Crop_Pumpkin_A.png", "Nature", "Flora", "Plants")
};
foreach (var item in taxonomyCases)
{
    var asset = ParseFa(item.Path);
    Expect(asset.Group == item.Group && asset.SubGroup == item.SubGroup && asset.Family == item.Family,
        $"FA path taxonomy failed for {item.Path}: {asset.Group}/{asset.SubGroup}/{asset.Family}.");
}

var coreFloorTextureCases = new (string Path, string Family, string Material, string Style, string Variant)[]
{
    (@"!Core_Settlements\Textures\Marble\Marble_A_White.jpg", "Marble", "Marble", "White", "A"),
    (@"!Core_Settlements\Textures\Marble\Marble_Tiles_A_Green.jpg", "Marble Tiles", "Marble", "Green", "A"),
    (@"!Core_Settlements\Textures\Marble\Marble_Tiles_Cracked_A3_Black.jpg", "Cracked Marble Tiles", "Marble", "Black", "A3"),
    (@"!Core_Settlements\Textures\Brick\Brick_Dirt_A.jpg", "Brick Floors", "Brick", "Dirt", "A"),
    (@"!Core_Settlements\Textures\Grates\Grate_Wood_D_Ashen.png", "Floor Grates", "Wood", "Ashen", "D"),
    (@"!Core_Settlements\Textures\Hay\Hay_A_01.jpg", "Hay", "Hay", "Unspecified", "A"),
    (@"!Core_Settlements\Textures\Rug_and_Carpets\Carpet_DesignA_BlackGold_A1.jpg", "Carpet Design A", "Fabric", "Black & Gold", "A1"),
    (@"!Core_Settlements\Textures\Rug_and_Carpets\Rug_Uneven_Overlay_A3.png", "Rug Overlays", "Fabric", "Uneven", "A3"),
    (@"!Core_Settlements\Textures\Stone_Diagonal_Tiles\Stone_Diagonal_Tiles_05_B3.jpg", "Diagonal Tiles", "Stone", "Unspecified", "B3"),
    (@"!Core_Settlements\Textures\Stone_Floors\Herringbone_Dirt_A.jpg", "Herringbone", "Stone", "Dirt", "A"),
    (@"!Core_Settlements\Textures\Stone_Floors\Rock_Tiles_A_Mossy_03.jpg", "Rock Tiles", "Stone", "Mossy", "A"),
    (@"!Core_Settlements\Textures\Stone_Hexagonal_Tiles\Stone_Hexagonal_Tiles_03_A2.jpg", "Hexagonal Tiles", "Stone", "Unspecified", "A2"),
    (@"!Core_Settlements\Textures\Stone_Patterned_Tiles\Stone_Patterned_Tiles_02_L.jpg", "Patterned Tiles", "Stone", "Unspecified", "L"),
    (@"!Core_Settlements\Textures\Stone_Square_Tiles\Stone_Tiles_Cracked_C2_07.jpg", "Cracked Stone Tiles", "Stone", "Unspecified", "C2"),
    (@"!Core_Settlements\Textures\Wood\Wood_Texture_A_Ashen.jpg", "Wood Texture", "Wood", "Ashen", "A"),
    (@"!Core_Settlements\Textures\Wooden_Floors\Wooden_Flooring_X_Walnut.jpg", "Wooden Flooring", "Wood", "Walnut", "X"),
    (@"!Core_Settlements\Textures\Wooden_Floors\Wood_Damage_Overlay_AG1.png", "Wood Damage Overlays", "Wood", "Unspecified", "AG1")
};
foreach (var item in coreFloorTextureCases)
{
    var asset = ParseFa(item.Path);
    Expect(asset.Group == "Building" && asset.SubGroup == "Floors" && asset.Family == item.Family
        && asset.Material == item.Material && asset.Style == item.Style && asset.Variant == item.Variant,
        $"Core floor texture failed for {item.Path}: {asset.Group}/{asset.SubGroup}/{asset.Family}; {asset.Material}/{asset.Style}/{asset.Variant}.");
}

var coreGlassSurface = ParseFa(@"!Core_Settlements\Textures\Glass\Glass_Blue.png");
Expect(coreGlassSurface.Group == "Building" && coreGlassSurface.SubGroup == "Textures"
    && coreGlassSurface.Family == "Glass Surfaces"
    && AssetSemanticClassifier.Classify(coreGlassSurface) is { Type: "Surface Materials", Subtype: "Glass" },
    "Core Glass surface texture was still classified as a physical opening/trim piece.");

var postOfficeSign = ParseFa(@"!Core_Settlements\Decor\Signs_and_Boards\Modular_Signs\Overlays\Post_Office_Sign_B1_Overlay_1x1.png");
Expect(AssetSemanticClassifier.Classify(postOfficeSign) is { Category: "Furnishings & Objects", Type: "Signs & Notices" },
    "Post-office sign overlays were mistaken for office equipment.");

var corrugatedPanel = ParseFa(@"Arctic\Base_Arctic_Settlement\Structures\Building\Corrugated_Panels\Corrugated_Panel_Metal_Frosty_A1_1x3.png");
var corrugatedSemantic = AssetSemanticClassifier.Classify(corrugatedPanel);
Expect(corrugatedPanel is { Group: "Building", SubGroup: "Roofs", Family: "Corrugated Panels" }
    && corrugatedSemantic.Paths.Any(path => path is { Type: "Roofs", Subtype: "Corrugated Panels" })
    && corrugatedSemantic.Paths.Any(path => path is { Type: "Construction Materials", Subtype: "Panels & Sheet Material" })
    && corrugatedSemantic.Contexts.Contains("Build: Roof Surface"),
    "Corrugated panels were not available as both roofing and construction sheet material.");

var coreFirewood = ParseFa(@"!Core_Settlements\Lightsources\Coals_and_Firewood\Firewood\Firewood_Pile_Wood_Ashen_A1_2x1.png");
var arcticFirewood = ParseFa(@"Arctic\Base_Arctic_Settlement\Lightsources\Coals_and_Firewood\Firewood\Firewood_Log_Pile_Wood_Snowy_A1_3x1.png");
Expect(new[] { coreFirewood, arcticFirewood }.All(asset => asset is { Group: "Props", SubGroup: "Heating & Fuel", Family: "Firewood" }
        && AssetSemanticClassifier.Classify(asset) is { Category: "Furnishings & Objects", Type: "Heating & Fuel", Subtype: "Firewood" }),
    "Firewood remained classified as lighting instead of household heating fuel.");

var rockySnow = ParseFa(@"Arctic\!Wilderness\Textures\Snow\Rocky_Snow_A2_01.jpg");
Expect(AssetSemanticClassifier.Classify(rockySnow) is { Category: "Nature", Type: "Terrain", Subtype: "Ground Surfaces" },
    "A Rocky Snow texture was classified as a placeable rock instead of a ground surface.");

var arcticBlood = ParseFa(@"Arctic\!Wilderness\Decor\Snow\Snow_Blood\Arctic_Blood_A1_2x2.png");
Expect(arcticBlood is { Group: "Props", SubGroup: "Combat", Family: "Gore" }
    && AssetSemanticClassifier.Classify(arcticBlood) is { Category: "Combat & Hazards", Type: "Remains & Gore", Subtype: "Blood" },
    "Arctic blood remained under Ice & Snow instead of gore.");

var snowEdge = ParseFa(@"Arctic\!Wilderness\Decor\Snow\Arctic_Snow_Edge_A1_1x1.png");
var snowPath = ParseFa(@"Arctic\!Wilderness\Decor\Snow\Snow_Paths\Arctic_Snow_Soft_Path_A1.png");
var snowEdgeSemantic = AssetSemanticClassifier.Classify(snowEdge);
var snowPathSemantic = AssetSemanticClassifier.Classify(snowPath);
Expect(snowEdgeSemantic is { Category: "Nature", Type: "Terrain", Subtype: "Snow Cover & Edges" }
    && snowPathSemantic is { Category: "Nature", Type: "Terrain", Subtype: "Snow Paths & Ribbons" },
    $"Snow edge/detail assets remained under shorelines instead of snowy terrain detailing: edge={snowEdge.Group}/{snowEdge.SubGroup}->{snowEdgeSemantic.Type}/{snowEdgeSemantic.Subtype}, path={snowPath.Group}/{snowPath.SubGroup}->{snowPathSemantic.Type}/{snowPathSemantic.Subtype}.");

var crackedIce = ParseFa(@"Arctic\!Wilderness\Decor\Snow\Arctic_Cracked_Ice_Clear_Path_A1.png");
Expect(AssetSemanticClassifier.Classify(crackedIce) is { Category: "Nature", Type: "Water & Liquids", Subtype: "Ice & Snow" },
    "Cracked-ice paths stopped resolving as ice terrain.");

var frostOverlay = ParseFa(@"Arctic\!Wilderness\Decor\Frost_Overlays\Arctic_Frost_Overlay_A1_1x1.png");
var snowTextureOverlay = ParseFa(@"Arctic\!Wilderness\Textures\Overlays\Snow_Overlay_A1.png");
Expect(AssetSemanticClassifier.Classify(snowTextureOverlay) is { Type: "Terrain", Subtype: "Frost & Snow Overlays" },
    "An Arctic snow texture overlay remained in the generic Ground Overlays bucket.");
var arcticThemeDetails = BuildModeService.MatchThemeDetails(
    [snowEdge, snowPath, crackedIce, arcticBlood, frostOverlay, snowTextureOverlay],
    new WallSetSelection { Theme = "Arctic" }, LibrarySourceIds.ForgottenAdventures);
Expect(arcticThemeDetails.Count == 3 && arcticThemeDetails.Contains(snowEdge)
    && arcticThemeDetails.Contains(frostOverlay) && arcticThemeDetails.Contains(snowTextureOverlay)
    && !arcticThemeDetails.Contains(snowPath) && !arcticThemeDetails.Contains(crackedIce) && !arcticThemeDetails.Contains(arcticBlood),
    "Arctic Planner detailing did not include snow cover/overlays while excluding terrain ribbons, ice, and gore.");

var bridgeCases = new (string Path, string Family, string Material, string Style, string Variant)[]
{
    (@"!Core_Settlements\Structures\Bridges\Draw_Bridges\Draw_Bridge_Wood_Ashen_A1_Rusty_3x4.png", "Drawbridges", "Wood", "Ashen", "A1"),
    (@"!Core_Settlements\Structures\Bridges\Log_Bridges\Log_Bridge_Wood_Walnut_Straight_B2_2x4.png", "Log Bridges", "Wood", "Walnut", "B2"),
    (@"!Core_Settlements\Structures\Bridges\Plank_Bridges\Plank_Bridge_Red_C_3x1.png", "Plank Bridges", "Wood", "Red", "C"),
    (@"!Core_Settlements\Structures\Bridges\Rope_Brides\Rope_Bridge_03_A1_1x4.png", "Rope Bridges", "Rope", "Unspecified", "A1"),
    (@"!Core_Settlements\Structures\Bridges\Train_Bridges\Bridge_End_Light_A2_2x1.png", "Train Bridges", "Wood", "Light", "A2"),
    (@"!Core_Settlements\Structures\Bridges\Train_Bridges\Track_Bridge_Support_Metal_Rusty_A4_3x1.png", "Train Bridges", "Metal", "Rusty", "A4")
};
foreach (var item in bridgeCases)
{
    var asset = ParseFa(item.Path);
    Expect(asset.Group == "Building" && asset.SubGroup == "Bridges" && asset.Family == item.Family
        && asset.Material == item.Material && asset.Style == item.Style && asset.Variant == item.Variant,
        $"Bridge classification failed for {item.Path}: {asset.Group}/{asset.SubGroup}/{asset.Family}; {asset.Material}/{asset.Style}/{asset.Variant}.");
}

var woodTrack = ParseFa(@"!Core_Settlements\Workplace_Equipment\Mining\Mine_Tracks\Track_Ashen_Corner_A1_2x2.png");
Expect(woodTrack.Material == "Wood" && woodTrack.Style == "Ashen", "Wood-backed mine track was not filterable as Wood with its finish retained as Style.");
var explicitWoodTrack = ParseFa(@"!Core_Settlements\Workplace_Equipment\Mining\Mine_Tracks\Paths\Track_Wood_Walnut_Path.png");
Expect(explicitWoodTrack.Material == "Wood" && explicitWoodTrack.Style == "Walnut", "Explicit Wood mine-track path parsing regressed.");
var soloTrack = ParseFa(@"!Core_Settlements\Workplace_Equipment\Mining\Mine_Tracks\Track_Solo_Broken_Straight_A1_1x2.png");
Expect(soloTrack.Material == "Metal" && soloTrack.Family == "Mine Tracks", "Track_Solo was not exposed as Material = Metal.");
var metalSoloPath = ParseFa(@"!Core_Settlements\Workplace_Equipment\Mining\Mine_Tracks\Paths\Track_Metal_Solo_Path.png");
Expect(metalSoloPath.Material == "Metal", "Track_Metal_Solo_Path was incorrectly folded into the Track_Solo material rule.");
var miningTool = ParseFa(@"!Core_Settlements\Workplace_Equipment\Mining\Mining_Tools\Pickaxe_Metal_Rusty_A.png");
Expect(miningTool.Group == "Props" && miningTool.SubGroup == "Workplace", "Unrelated mining workplace tools leaked into Transport > Mining.");

var frame = ParseFa(@"Arctic\Base\Structures\Building\Doors\Door_Frames\Door_Frame_Wood_Frosty_A1_1x2.png");
var sill = ParseFa(@"Woodlands\Base\Structures\Building\Windows\Window_Sills\Window_Sill_Wood_Walnut_A1_2x1.png");
var arch = ParseFa(@"Woodlands\Base\Structures\Building\Arches\Arch_Wood_Walnut_A1_1x2.png");
Expect(frame.Family == "Door Frames" && sill.Family == "Window Sills" && arch.Family == "Arches", "Opening semantic families failed.");
Expect(frame.Variant == "A1" && sill.Variant == "A1", "Opening design code was not retained as Variant.");

var floorPlate = ParseFa(@"Astral\Ceremorph\Structures\Building\Floor_Plates\Organic\Floor_Plate_Organic_Purple_A7_1x1.png");
Expect(floorPlate.Family == "Floor Plates" && floorPlate.Variant == "A7" && floorPlate.Theme == "Astral", "Floor family/variant/theme parsing failed.");
var meal = ParseFa(@"!Core_Settlements\Clutter\Food\Prepared_Meals\Soup_Bowl_Wood_Ashen_A1_1x1.png");
Expect(meal.Group == "Props" && meal.SubGroup == "Prepared Meals" && meal.Material == "N/A" && meal.Variant == "A1", "Prepared meal taxonomy guessed or discarded metadata.");

var sourceA = brick with { SourceId = "source-a", Material = "Adobe", Style = "Earthy", Variant = "A" };
var sourceB = brick with { SourceId = "source-b", Material = "Adobe", Style = "Sandstone", Variant = "B" };
var wood = brick with { SourceId = "source-a", Material = "Wood", Style = "Ashen", Variant = "A" };
var filterAssets = new[] { sourceA, sourceB, wood };
var styles = AssetQueries.ValuesForFacet(filterAssets, new AssetFilter(Group: "Building", SubGroup: "Walls", Material: "Adobe", SourceId: "source-a"), AssetFacet.Style);
Expect(styles.SequenceEqual(new[] { "All", "Earthy" }), "Source/group/material context offered an empty Style value.");
var variantMatches = AssetQueries.Filter(filterAssets, new AssetFilter(SourceId: "source-b", Variant: "B")).ToList();
Expect(variantMatches.Count == 1 && ReferenceEquals(variantMatches[0], sourceB), "Source and Variant filters were not independent.");
var materialAcrossGroups = new[] { brick with { Group = "Building", Material = "Wood" }, meal with { Group = "Props", Material = "Wood" } };
Expect(AssetQueries.Filter(materialAcrossGroups, new AssetFilter(Material: "Wood")).Count() == 2, "Material filtering did not work across Groups.");
var filenameSearch = AssetQueries.Filter(new[] { floorPlate, brick }, new AssetFilter(FilenameQuery: "floor_plate_organic")).ToList();
Expect(filenameSearch.Count == 1 && filenameSearch[0] == floorPlate, "Case-insensitive filename search failed.");
Expect(!AssetQueries.Filter(new[] { floorPlate }, new AssetFilter(FilenameQuery: "floor_plate", FilenameCaseSensitive: true)).Any(), "Case-sensitive filename search ignored case.");
var searchAsset = floorPlate with { FileName = "Wall_Corner_Stone_Ashen_A1.png" };
Expect(AssetQueries.Filter(new[] { searchAsset }, new AssetFilter(FilenameQuery: "Corner Ashen")).Single() == searchAsset,
    "Unquoted filename words were not matched independently with AND behavior.");
Expect(AssetQueries.Filter(new[] { searchAsset }, new AssetFilter(FilenameQuery: "Ashen Corner")).Single() == searchAsset,
    "Unquoted filename search remained order-dependent.");
Expect(!AssetQueries.Filter(new[] { searchAsset }, new AssetFilter(FilenameQuery: "Corner Walnut")).Any(),
    "Unquoted filename search did not require every word to match.");
Expect(AssetQueries.Filter(new[] { searchAsset }, new AssetFilter(FilenameQuery: "\"Corner_Stone\"")).Single() == searchAsset,
    "Quoted filename phrase did not match verbatim.");
Expect(!AssetQueries.Filter(new[] { searchAsset }, new AssetFilter(FilenameQuery: "\"Corner Ashen\"")).Any(),
    "Quoted filename phrase incorrectly ignored separators or word adjacency.");
Expect(AssetQueries.Filter(new[] { searchAsset }, new AssetFilter(FilenameQuery: "Wall \"Stone_Ashen\"")).Single() == searchAsset,
    "Mixed word and quoted-phrase filename search failed.");
var astralBuilding = floorPlate with { Group = "Building", SubGroup = "Floors", Theme = "Astral", Family = "Floor Plates" };
var astralFlora = floorPlate with { Group = "Nature", SubGroup = "Flora", Theme = "Astral", Family = "Vines", FileName = "Tendril_Vine_Space_A1.png" };
var desertProps = floorPlate with { Group = "Props", SubGroup = "Decor", Theme = "Desert", Family = "Statues" };
var crossFacetAssets = new[] { astralBuilding, astralFlora, desertProps };
var astralGroups = AssetQueries.ValuesForFacet(crossFacetAssets, new AssetFilter(Theme: "Astral"), AssetFacet.Group);
Expect(astralGroups.SequenceEqual(new[] { "All", "Building", "Nature" }), "Theme did not constrain Group choices.");
var astralBuildingSubGroups = AssetQueries.ValuesForFacet(crossFacetAssets, new AssetFilter(Group: "Building", Theme: "Astral"), AssetFacet.SubGroup);
Expect(astralBuildingSubGroups.SequenceEqual(new[] { "All", "Floors" }), "Group and Theme did not jointly constrain SubGroup choices.");
var searchFamilies = AssetQueries.ValuesForFacet(crossFacetAssets, new AssetFilter(FilenameQuery: "Tendril_Vine"), AssetFacet.Family);
Expect(searchFamilies.SequenceEqual(new[] { "All", "Vines" }), "Filename search did not constrain taxonomy choices.");
var crossFacetIndex = new AssetCatalogIndex(crossFacetAssets);
foreach (var probe in new[]
{
    new AssetFilter(Theme: "Astral"),
    new AssetFilter(Group: "Building", Theme: "Astral"),
    new AssetFilter(FilenameQuery: "Tendril_Vine"),
    new AssetFilter(FilenameQuery: "Vine Tendril"),
    new AssetFilter(FilenameQuery: "\"Tendril_Vine\""),
    new AssetFilter(Theme: "astral", FilenameQuery: "FLOOR", FilenameCaseSensitive: false)
})
{
    var expectedPaths = AssetQueries.Filter(crossFacetAssets, probe).Select(asset => asset.FilePath);
    var indexedPaths = crossFacetIndex.Filter(probe).Select(asset => asset.FilePath);
    Expect(expectedPaths.SequenceEqual(indexedPaths), "Indexed filtering diverged from existing filter behavior.");
    foreach (var facet in Enum.GetValues<AssetFacet>())
        Expect(AssetQueries.ValuesForFacet(crossFacetAssets, probe, facet)
                .SequenceEqual(crossFacetIndex.ValuesForFacet(probe, facet)),
            $"Indexed {facet} choices diverged from existing cross-filter behavior.");
}

var semanticCrack = ParseFa(@"Desert\Base_Desert_Settlement\Structures\Building\Walls_and_Curbs\Wall_Adobe_Cracks\Wall_Adobe_Sandstone_Cracking_A1_1x1.png");
var crackTaxonomy = AssetSemanticClassifier.Classify(semanticCrack);
Expect(crackTaxonomy.Category == "Construction" && crackTaxonomy.Type == "Walls" && crackTaxonomy.Subtype == "Cracks & Damage",
    "Adobe crack overlays were not exposed as wall detailing in the semantic taxonomy.");
var semanticAdobePiece = ParseFa(@"Desert\Base_Desert_Settlement\Structures\Building\Walls_and_Curbs\Wall_Adobe_Wide_A\Wall_Adobe_Sandstone_Wide_A1_Path.png");
Expect(AssetSourceMetadata.SourceSet(semanticCrack) == "Wall Adobe Cracks"
       && AssetSourceMetadata.SourceSet(semanticAdobePiece) == "Wall Adobe Wide A",
    "Advanced Source Folder / Set metadata did not preserve the objective source collections.");
Expect(AssetSourceMetadata.FilenameVariant(semanticCrack) == "A1"
       && AssetSourceMetadata.FilenameVariant(semanticAdobePiece) == "A1",
    "Advanced Filename Variant metadata did not use the final filename design code.");
var advancedAssets = new[] { semanticCrack, semanticAdobePiece };
var advancedFilter = new AssetFilter(Category: "Construction", Type: "Walls", SourceSet: "Wall Adobe Wide A");
Expect(AssetQueries.Filter(advancedAssets, advancedFilter).SequenceEqual(new[] { semanticAdobePiece }),
    "Advanced Source Folder / Set filtering leaked Adobe cracks into a wall construction set.");
Expect(AssetQueries.ValuesForFacet(advancedAssets, new AssetFilter(Category: "Construction", Type: "Walls"), AssetFacet.SourceSet)
        .SequenceEqual(new[] { "All", "Wall Adobe Cracks", "Wall Adobe Wide A" }),
    "Advanced Source Folder / Set choices were not constrained by the active semantic filters.");
var advancedIndex = new AssetCatalogIndex(advancedAssets);
Expect(advancedIndex.Filter(advancedFilter).SequenceEqual(new[] { semanticAdobePiece })
       && advancedIndex.ValuesForFacet(new AssetFilter(Category: "Construction", Type: "Walls"), AssetFacet.SourceSet)
           .SequenceEqual(new[] { "All", "Wall Adobe Cracks", "Wall Adobe Wide A" }),
    "Indexed advanced filtering diverged from direct advanced filtering.");
var semanticPlate = ParseFa(@"!Core_Settlements\Clutter\Kitchenware\Plates\Plate_Ceramic_White_A1_1x1.png");
var plateTaxonomy = AssetSemanticClassifier.Classify(semanticPlate);
Expect(plateTaxonomy.Category == "Food & Dining" && plateTaxonomy.Type == "Kitchen & Dining Clutter" && plateTaxonomy.Subtype == "Tableware"
    && plateTaxonomy.Contexts.Contains("Kitchen & Food Preparation") && plateTaxonomy.Contexts.Contains("Tavern Kitchen"),
    "Tableware was not available to cross-category kitchen retrieval.");
var semanticIndex = new AssetCatalogIndex(new[] { semanticCrack, semanticPlate });
Expect(semanticIndex.Filter(new AssetFilter(Category: "Construction", Type: "Walls", Subtype: "Cracks & Damage")).Single() == semanticCrack,
    "Semantic Category, Type, and Subtype facets did not retrieve the Adobe crack overlay.");
Expect(semanticIndex.Filter(new AssetFilter(Context: "Kitchen & Food Preparation")).Single() == semanticPlate,
    "Multi-valued scene Context filtering did not retrieve kitchen tableware.");
Expect(semanticIndex.Filter(new AssetFilter(Context: "Tavern Kitchen")).Single() == semanticPlate,
    "Named Tavern Kitchen retrieval did not include general-purpose tableware.");
var hierarchyArrowSlit = ParseFa(@"!Core_Settlements\Structures\Building\Windows\Arrowslits\Arrowslit_Stone_Earthy_A1_1x1.png");
var hierarchyAssets = new[] { brick, semanticCrack, semanticPlate, hierarchyArrowSlit };
var hierarchyTypes = AssetQueries.ValuesForFacet(hierarchyAssets, new AssetFilter(), AssetFacet.Type);
var hierarchySubtypes = AssetQueries.ValuesForFacet(hierarchyAssets, new AssetFilter(), AssetFacet.Subtype);
Expect(hierarchyTypes.Contains("Walls") && hierarchyTypes.Contains("Openings") && hierarchyTypes.Contains("Kitchen & Dining Clutter")
    && hierarchySubtypes.Contains("Wall Pieces") && hierarchySubtypes.Contains("Cracks & Damage")
    && hierarchySubtypes.Contains("Arrow Slits") && hierarchySubtypes.Contains("Tableware"),
    "Cascading Type and Subtype choices did not include precise and cross-listed classifications.");
var parentWallMatches = AssetQueries.Filter(hierarchyAssets, new AssetFilter(Type: "Walls")).ToList();
var parentDetailMatches = AssetQueries.Filter(hierarchyAssets, new AssetFilter(Type: "Walls", Subtype: "Cracks & Damage")).ToList();
var parentOpeningMatches = AssetQueries.Filter(hierarchyAssets, new AssetFilter(Type: "Openings")).ToList();
Expect(parentWallMatches.Count == 3 && parentWallMatches.Contains(brick) && parentWallMatches.Contains(semanticCrack)
    && parentWallMatches.Contains(hierarchyArrowSlit)
    && parentDetailMatches.SequenceEqual(new[] { semanticCrack }),
    "The Walls Type did not include its wall pieces, details, and cross-listed additions.");
Expect(parentOpeningMatches.SequenceEqual(new[] { hierarchyArrowSlit }),
    "A cross-listed wall addition lost its precise Openings classification.");
var hierarchyIndex = new AssetCatalogIndex(hierarchyAssets);
Expect(hierarchyIndex.Filter(new AssetFilter(Type: "Walls")).SequenceEqual(parentWallMatches)
    && hierarchyIndex.ValuesForFacet(new AssetFilter(), AssetFacet.Type).SequenceEqual(hierarchyTypes)
    && hierarchyIndex.ValuesForFacet(new AssetFilter(), AssetFacet.Subtype).SequenceEqual(hierarchySubtypes),
    "Indexed Type/Subtype filtering diverged from direct taxonomy filtering.");

var adobePath = ParseFa(@"Desert\Base_Desert_Settlement\Structures\Building\Walls_and_Curbs\Wall_Adobe_Wide_A\Paths\Wall_Adobe_Sandstone_Wide_A1_Path.png");
var adobeCorner = ParseFa(@"Desert\Base_Desert_Settlement\Structures\Building\Walls_and_Curbs\Wall_Adobe_Wide_A\Wall_Adobe_Sandstone_Wide_Corner_A1_1x1.png");
var adobeCracks = Enumerable.Range(1, 15).Select(index => ParseFa(
    $@"Desert\Base_Desert_Settlement\Structures\Building\Walls_and_Curbs\Wall_Adobe_Cracks\Wall_Adobe_Sandstone_Cracking_A{index}_1x1.png")).ToList();
var exactWallAssets = new[] { adobePath, adobeCorner }.Concat(adobeCracks).ToList();
var exactWallCatalog = WallConstructionCatalog.Build(exactWallAssets);
var exactWallSelection = new WallSetSelection
{
    WallSystem = "Adobe", PrimaryAppearance = "Sandstone", Theme = "Desert", Profile = "Wide", Variant = "A"
};
var exactSets = exactWallCatalog.Filter(exactWallSelection, LibrarySourceIds.ForgottenAdventures);
Expect(exactSets.Count == 1 && exactSets[0].WallPieces.Count == 2 && exactSets[0].Details.Count == 15,
    "Exact Adobe construction set did not include all structural pieces and all compatible cracks.");
Expect(exactSets[0].AutomaticRibbon.Mapping?.CspRibbonName == "Desert_Wall_Adobe_Sandstone_Wide_A",
    "Path-anchored Adobe construction set did not resolve exactly one ribbon.");
var exactPalette = BuildModeService.Populate(new AssetCatalogIndex(exactWallAssets), exactWallCatalog,
    new BuildRecipe { WallSet = exactWallSelection }, new Dictionary<string, RibbonMapping>(),
    LibrarySourceIds.ForgottenAdventures);
Expect(exactPalette.Walls.Count == 2 && exactPalette.WallDetails.Count == 15
    && exactPalette.WallRibbon.Status == WallRibbonResolutionStatus.Resolved,
    "New Planner population did not separate wall pieces from compatible detailing.");
var exactWallSelector = new WallSetSelectorViewModel(() => exactWallCatalog, () => LibrarySourceIds.ForgottenAdventures);
exactWallSelector.Restore(exactWallSelection);
Expect(exactWallSelector.CandidateCount == 1 && exactWallSelector.CandidateSummary.Contains("15 detailing")
    && exactWallSelector.PrimaryComponentLabel == "Adobe" && !exactWallSelector.HasSecondaryComponent,
    "Dynamic wall selector did not resolve the exact Adobe set cleanly.");
Expect(exactWallSelector.PersistedSelection.SetId == exactSets[0].Id,
    "An exact Planner wall choice did not persist its stable construction-set identity.");
var previewPalette = BuildModeService.PreviewWallSet(new AssetCatalogIndex(exactWallAssets), exactWallCatalog,
    exactWallSelection, new Dictionary<string, RibbonMapping>(), new BuildSelection(), LibrarySourceIds.ForgottenAdventures);
Expect(previewPalette.Walls.Count == 2 && previewPalette.WallDetails.Count == 15
    && previewPalette.Floors.Count == 0 && previewPalette.DoorFrames.Count == 0,
    "Live wall preview did not return the complete exact wall kit without populating unrelated build sections.");
exactWallSelector.Reset();
Expect(exactWallSelector.SelectSet(exactSets[0].Id) && exactWallSelector.CandidateCount == 1
    && exactWallSelector.PersistedSelection.SetId == exactSets[0].Id,
    "Click-to-select wall sample could not restore one exact construction set.");

var mudbrickThin = ParseFa(@"Desert\Base_Desert_Settlement\Structures\Building\Walls_and_Curbs\Wall_Mudbrick_Light_Thin_A\Paths\Wall_Mudbrick_Light_Thin_A1_Path.png");
var mudbrickWide = ParseFa(@"Desert\Base_Desert_Settlement\Structures\Building\Walls_and_Curbs\Wall_Mudbrick_Red_Wide_A\Paths\Wall_Mudbrick_Red_Wide_A1_Path.png");
Expect(mudbrickThin.Material == "Adobe" && mudbrickThin.Style == "Mudbrick Light"
    && mudbrickThin.Family == "Thin" && mudbrickThin.Variant == "A"
    && mudbrickWide.Material == "Adobe" && mudbrickWide.Style == "Mudbrick Red"
    && mudbrickWide.Family == "Wide" && mudbrickWide.Variant == "A",
    "Mudbrick was not remodeled as Adobe with a content-specific color appearance and Thin/Wide profile.");
var adobeShelf = ParseFa(@"Desert\Base_Desert_Settlement\Structures\Building\Walls_and_Curbs\Wall_Adobe_Wide_A\Wall_Adobe_Sandstone_Wide_Shelf_Wood_Ashen_A1_2x1.png");
var adobeWallTaxonomy = AssetSemanticClassifier.Classify(adobeShelf);
Expect(adobeWallTaxonomy.Appearances.SequenceEqual(new[] { "Sandstone" })
    && !adobeWallTaxonomy.Appearances.Contains("Ashen"),
    "A wood insert inside a single-material Adobe wall polluted the Browser's wall Appearance choices.");
var compositeWall = ParseFa(@"!Core_Settlements\Structures\Building\Walls_and_Curbs\Wall_StoneWood_A\Paths\Wall_StoneWood_Sandstone_Ashen_A_Straight_Path.png");
var compositeSet = WallConstructionCatalog.Build([compositeWall]).Sets.Single();
Expect(compositeSet.WallSystem == "Stone Wood" && compositeSet.PrimaryComponent == "Stone"
    && compositeSet.PrimaryAppearance == "Sandstone" && compositeSet.SecondaryComponent == "Wood"
    && compositeSet.SecondaryAppearance == "Ashen",
    "Compound wall construction did not preserve component-specific appearances.");
var compositeTaxonomy = AssetSemanticClassifier.Classify(compositeWall);
Expect(compositeTaxonomy.Materials.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(new[] { "Stone", "Wood" })
    && compositeTaxonomy.Appearances.Contains("Sandstone") && compositeTaxonomy.Appearances.Contains("Ashen"),
    "Compound assets were not exposed through multi-valued Browser material and appearance facets.");
var compositeIndex = new AssetCatalogIndex([compositeWall]);
Expect(compositeIndex.Filter(new AssetFilter(MaterialFacet: "Wood", AppearanceFacet: "Ashen")).Single() == compositeWall
    && compositeIndex.Filter(new AssetFilter(MaterialFacet: "Stone", AppearanceFacet: "Sandstone")).Single() == compositeWall,
    "Compound wall could not be retrieved through either component's material and appearance.");
var tilePath = ParseFa(@"Desert\Base_Desert_Settlement\Structures\Building\Walls_and_Curbs\Wall_Tile_A\Wall_Tile_A_PatternA_A1_Path.png");
var tileRubble = Enumerable.Range(1, 3).Select(index => ParseFa(
    $@"Desert\Base_Desert_Settlement\Structures\Building\Walls_and_Curbs\Wall_Tile_Rubble\Wall_Tile_Broken_Piece_PatternA_A{index}_1x1.png")).ToList();
var tileSet = WallConstructionCatalog.Build(new[] { tilePath }.Concat(tileRubble)).Sets.Single();
Expect(tileSet.Details.Count == 3 && tileSet.Details.All(detail => AssetSemanticClassifier.Classify(detail) is { Type: "Walls", Subtype: "Loose Pieces & Rubble" }),
    "Compatible wall detailing remained hard-coded to Adobe cracks instead of general wall-detail collections.");
var stonePathForDetails = adobePath with
{
    FilePath = @"C:\FA\Wall_Stone_A\Paths\Wall_Stone_Earthy_A_Path.png",
    RelativePath = @"!Core_Settlements\Structures\Building\Walls_and_Curbs\Wall_Stone_A\Paths\Wall_Stone_Earthy_A_Path.png",
    FileName = "Wall_Stone_Earthy_A_Path.png",
    Material = "Stone", Style = "Earthy", Theme = "Core", Family = "Imperfect", Variant = "A", PartType = "Path"
};
var stoneDamage = stonePathForDetails with
{
    FilePath = @"C:\FA\Rubble\Wall_Damage_Overlay_Stone_Earthy_G1_1x2.png",
    RelativePath = @"!Core_Settlements\Structures\Rubble\Rubble_Piles\Stone\Wall_Damage_Overlay_Stone_Earthy_G1_1x2.png",
    FileName = "Wall_Damage_Overlay_Stone_Earthy_G1_1x2.png",
    SubGroup = "Ruins & Rubble", Family = "Wall Damage", Variant = "G1", PartType = "Overlay"
};
var stoneDecoration = stonePathForDetails with
{
    FilePath = @"C:\FA\Aesthetics\Wall_Decoration_Stone_Earthy_Small_A1_1x1.png",
    RelativePath = @"!Core_Settlements\Structures\Aesthetics\Wall_Decorations\Wall_Decoration_Stone_Earthy_Small_A1_1x1.png",
    FileName = "Wall_Decoration_Stone_Earthy_Small_A1_1x1.png",
    Group = "Props", SubGroup = "Decor", Family = "Wall Decorations", Variant = "A1", PartType = "Decoration"
};
var generalizedDetailSet = WallConstructionCatalog.Build([stonePathForDetails, stoneDamage, stoneDecoration]).Sets.Single();
Expect(generalizedDetailSet.Details.Count == 2
    && AssetSemanticClassifier.Classify(stoneDamage) is { Type: "Walls", Subtype: "Cracks & Damage" }
    && AssetSemanticClassifier.Classify(stoneDecoration) is { Type: "Walls", Subtype: "Decorations" },
    "Filename- and aesthetics-based wall details were not associated outside the Adobe folder model.");
var brickWoodPath = ParseFa(@"!Core_Settlements\Structures\Building\Walls_and_Curbs\Wall_BrickWood_A\Wall_BrickWood_Earthy_Ashen_A_Straight_Path.png");
var arrowslitPlain = ParseFa(@"!Core_Settlements\Structures\Building\Windows\Arrowslits\Arrowslit_Stone_Earthy_A1_1x1.png");
var arrowslitAshen = ParseFa(@"!Core_Settlements\Structures\Building\Windows\Arrowslits\Arrowslit_Stone_Earthy_A1_Ashen_1x1.png");
var arrowslitDark = ParseFa(@"!Core_Settlements\Structures\Building\Windows\Arrowslits\Arrowslit_Stone_Earthy_A1_Dark_1x1.png");
var archedWallEarthy = ParseFa(@"!Core_Settlements\Structures\Building\Arches\Arched_Wall_Stone_Earthy_Small_A1_2x1.png");
var archedWallSlate = ParseFa(@"!Core_Settlements\Structures\Building\Arches\Arched_Wall_Stone_Slate_Small_A1_2x1.png");
var wallAdditionAssets = new[] { brickWoodPath, arrowslitPlain, arrowslitAshen, arrowslitDark, archedWallEarthy, archedWallSlate };
var wallAdditionCatalog = WallConstructionCatalog.Build(wallAdditionAssets);
var brickWoodSet = wallAdditionCatalog.Sets.Single();
var wallAdditionPalette = BuildModeService.Populate(new AssetCatalogIndex(wallAdditionAssets), wallAdditionCatalog,
    new BuildRecipe
    {
        WallSet = new WallSetSelection { SetId = brickWoodSet.Id },
        Trim = new BuildSelection { Group = "Building", SubGroup = "Openings", Material = "Wood", Style = "Ashen" }
    }, new Dictionary<string, RibbonMapping>(), LibrarySourceIds.ForgottenAdventures);
Expect(wallAdditionPalette.WallComponents.Count == 3
    && wallAdditionPalette.WallComponents.Contains(arrowslitPlain)
    && wallAdditionPalette.WallComponents.Contains(arrowslitAshen)
    && wallAdditionPalette.WallComponents.Contains(archedWallEarthy)
    && !wallAdditionPalette.WallComponents.Contains(arrowslitDark)
    && !wallAdditionPalette.WallComponents.Contains(archedWallSlate)
    && wallAdditionPalette.OtherOpenings.Count == 0,
    "Wall additions did not match the selected masonry and wood appearances or leaked into Other Openings.");
Expect(AssetSemanticClassifier.Classify(arrowslitAshen) is { Type: "Openings", Subtype: "Arrow Slits" }
    && AssetSemanticClassifier.Classify(archedWallEarthy) is { Type: "Openings", Subtype: "Arched Wall Inserts" },
    "Arrow slits and arched wall inserts remained hidden under generic window categories.");

var wallStraight = brick with { SourceId = "source-a", FilePath = @"C:\FA\Wall_Wood_Ashen_A_Straight.png", FileName = "Wall_Wood_Ashen_A_Straight.png", Material = "Wood", Style = "Ashen", Family = "Unspecified", Variant = "A", PartType = "Straight" };
var wallCorner = wallStraight with { FilePath = @"C:\FA\Wall_Wood_Ashen_A_Corner.png", FileName = "Wall_Wood_Ashen_A_Corner.png", Variant = "A", PartType = "Corner" };
var wallJoint = wallStraight with { FilePath = @"C:\FA\Wall_Wood_Ashen_A_Joint.png", FileName = "Wall_Wood_Ashen_A_Joint.png", Variant = "A", PartType = "Joint" };
var unrelatedProp = wallStraight with { FilePath = @"C:\FA\Chair_Wood_Ashen.png", FileName = "Chair_Wood_Ashen.png", Group = "Props", SubGroup = "Furniture" };
var buildFloor = wallStraight with
{
    FilePath = @"C:\FA\!Core_Settlements\Textures\Wood\Floor_Wood_Light_Shack.png",
    RelativePath = @"!Core_Settlements\Textures\Wood\Floor_Wood_Light_Shack.png",
    FileName = "Floor_Wood_Light_Shack.png", SubGroup = "Floors", Style = "Light", Theme = "Shack",
    Family = "Floor Textures", Variant = "Unspecified"
};
var buildDoor = wallStraight with { FilePath = @"C:\FA\Door_Frame_Wood_Light_A1.png", FileName = "Door_Frame_Wood_Light_A1.png", SubGroup = "Openings", Style = "Light", Family = "Door Frames", Variant = "A1" };
var buildSill = buildDoor with { FilePath = @"C:\FA\Window_Sill_Wood_Light_A1.png", FileName = "Window_Sill_Wood_Light_A1.png", Family = "Window Sills" };
var buildArch = buildDoor with { FilePath = @"C:\FA\Arch_Wood_Light_A1.png", FileName = "Arch_Wood_Light_A1.png", Family = "Arches" };
var staleGlassOpening = buildDoor with
{
    FilePath = @"C:\FA\!Core_Settlements\Textures\Glass\Glass_Blue.png",
    FileName = "Glass_Blue.png",
    RelativePath = @"!Core_Settlements\Textures\Glass\Glass_Blue.png",
    Material = "Glass",
    Style = "Blue",
    Family = "Glass Surfaces"
};
var buildAssets = new[] { wallStraight, wallCorner, wallJoint, unrelatedProp, buildFloor, buildDoor, buildSill, buildArch, staleGlassOpening };
var buildIndex = new AssetCatalogIndex(buildAssets);
var buildWallCatalog = WallConstructionCatalog.Build(buildAssets);
var buildWallSet = buildWallCatalog.Sets.Single();
var broadTrim = BuildModeService.MatchSelection(buildIndex, BuildComponent.Trim,
    BuildModeService.Canonicalize(new BuildSelection(), BuildComponent.Trim), "source-a");
Expect(broadTrim.Count == 3 && !broadTrim.Contains(staleGlassOpening),
    "Planner Trim included a surface texture from a stale Openings classification.");
Expect(!BuildModeService.ValuesForFacet(buildIndex, BuildComponent.Trim,
        BuildModeService.Canonicalize(new BuildSelection(), BuildComponent.Trim), AssetFacet.Material, "source-a").Contains("Glass")
    && !BuildModeService.ValuesForFacet(buildIndex, BuildComponent.Trim,
        BuildModeService.Canonicalize(new BuildSelection(), BuildComponent.Trim), AssetFacet.Family, "source-a").Contains("Glass Surfaces"),
    "Stale Glass surface metadata leaked into Planner Trim filter choices.");

var trimSelector = new BuildSelectorViewModel("Trim", BuildComponent.Trim, () => buildIndex, () => "source-a");
trimSelector.Reset();
Expect(trimSelector.SelectMaterialAppearance(buildArch)
    && string.IsNullOrEmpty(trimSelector.Selection.AssetIdentity)
    && trimSelector.SelectedMaterial == "Wood" && trimSelector.SelectedStyle == "Light"
    && BuildModeService.MatchSelection(buildIndex, BuildComponent.Trim, trimSelector.Selection, "source-a").Count == 3,
    "Clicking a Trim sample did not retain the full matching material/appearance set.");
var seededFloorSelector = new BuildSelectorViewModel("Floor", BuildComponent.Floor, () => buildIndex, () => "source-a");
seededFloorSelector.Reset();
Expect(seededFloorSelector.TrySeedMaterialAppearance("Wood", "Light")
    && seededFloorSelector.SelectedMaterial == "Wood" && seededFloorSelector.SelectedStyle == "Light"
    && seededFloorSelector.CanReceiveWallDefaults,
    "A reset Floor selector did not accept compatible wall material/appearance defaults.");
seededFloorSelector.ClearAutomaticWallDefaults();
Expect(seededFloorSelector.IsUnfiltered,
    "An automatically seeded Floor selector could not return to reset state when the wall material became ambiguous.");
seededFloorSelector.SelectedMaterial = "Wood";
Expect(!seededFloorSelector.CanReceiveWallDefaults,
    "A user-edited Floor selector was still eligible for automatic wall defaults.");

var alternateFloor = buildFloor with
{
    FilePath = @"C:\FA\!Core_Settlements\Textures\Wood\Floor_Wood_Light_Shack_B.png",
    RelativePath = @"!Core_Settlements\Textures\Wood\Floor_Wood_Light_Shack_B.png",
    FileName = "Floor_Wood_Light_Shack_B.png",
    Variant = "B"
};
var exactFloorIndex = new AssetCatalogIndex([buildFloor, alternateFloor]);
var exactFloorSelector = new BuildSelectorViewModel("Floor", BuildComponent.Floor, () => exactFloorIndex, () => "source-a");
exactFloorSelector.Reset();
Expect(BuildModeService.MatchSelection(exactFloorIndex, BuildComponent.Floor, exactFloorSelector.Selection, "source-a").Count == 2,
    "A broad Floor window-shopping selection did not expose every matching asset.");
Expect(exactFloorSelector.SelectAsset(alternateFloor)
    && exactFloorSelector.Selection.AssetIdentity == alternateFloor.StableIdentity
    && BuildModeService.MatchSelection(exactFloorIndex, BuildComponent.Floor, exactFloorSelector.Selection, "source-a").SequenceEqual(new[] { alternateFloor }),
    "Clicking a Floor sample did not pin the exact asset.");
exactFloorSelector.FilterTo(AssetFacet.Material, alternateFloor.Material);
Expect(string.IsNullOrEmpty(exactFloorSelector.Selection.AssetIdentity)
    && BuildModeService.MatchSelection(exactFloorIndex, BuildComponent.Floor, exactFloorSelector.Selection, "source-a").Count == 2,
    "Filtering an exact Floor sample by one property did not broaden it back to matching assets.");

var resetFloorSelector = new BuildSelectorViewModel("Floor", BuildComponent.Floor, () => buildIndex, () => "source-a");
resetFloorSelector.Restore(new BuildSelection { Group = "Building", SubGroup = "Floors", Material = "Wood", Style = "Light", Theme = "Shack" });
Expect(resetFloorSelector.SelectedMaterial == "Wood" && resetFloorSelector.SelectedStyle == "Light" && resetFloorSelector.SelectedTheme == "Shack",
    "Restoring a Floor recipe did not preserve its independent material, appearance, and theme.");

var buildRecipe = new BuildRecipe
{
    WallSet = new WallSetSelection { SetId = buildWallSet.Id },
    Floor = new BuildSelection { Group = "Building", SubGroup = "Floors", Material = "Wood", Style = "Light", Theme = "Shack" },
    Trim = new BuildSelection { Group = "Building", SubGroup = "Openings", Material = "Wood", Style = "Light" },
    IsPopulated = true
};
var wallSetKey = BuildModeService.WallMappingKey(buildWallSet);
var buildMappings = new Dictionary<string, RibbonMapping>(StringComparer.OrdinalIgnoreCase)
{
    [wallSetKey] = new RibbonMapping { FamilyKey = wallSetKey, CspTool = "Woodland & Farm", CspToolGroup = "Wood Walls", CspRibbonName = "Wood Ashen Wall" }
};
var palette = BuildModeService.Populate(buildAssets, buildRecipe, buildMappings, "source-a");
Expect(palette.Walls.Count == 3 && palette.Walls.Select(asset => asset.PartType).ToHashSet().SetEquals(new[] { "Straight", "Corner", "Joint" }), "Build population did not include the complete matching wall-piece set.");
Expect(palette.Walls.All(asset => asset.Group == "Building" && asset.SubGroup == "Walls") && !palette.Walls.Contains(unrelatedProp), "Build wall membership ignored Group/SubGroup source-of-truth boundaries.");
Expect(palette.Floors.SequenceEqual(new[] { buildFloor }), "Build floor selection did not populate independently.");
Expect(palette.DoorFrames.SequenceEqual(new[] { buildDoor }) && palette.WindowSills.SequenceEqual(new[] { buildSill }) && palette.OtherOpenings.SequenceEqual(new[] { buildArch }), "Build trim population dropped or mispartitioned opening assets.");
Expect(palette.WallRibbon.Status == WallRibbonResolutionStatus.Resolved && ReferenceEquals(palette.WallRibbon.Mapping, buildMappings[wallSetKey]), "Build wall set did not resolve one shared ribbon association.");
var optionalPalette = BuildModeService.Populate(buildAssets, new BuildRecipe(), new Dictionary<string, RibbonMapping>(), "source-a");
Expect(optionalPalette.Walls.Count == 3 && optionalPalette.Floors.Count == 1 && optionalPalette.DoorFrames.Count == 1, "Optional All metadata levels did not produce a usable scoped Build palette.");

var stoneGeometryMatrix = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["A"] = "A", ["B1"] = "B1", ["B2"] = "B2", ["B3"] = "B3", ["C"] = "C",
    ["D"] = "D", ["E"] = "E", ["F"] = "F", ["G"] = "G", ["H"] = "H",
    ["I1"] = "I", ["I2"] = "J", ["I3"] = "J2", ["J1"] = "K", ["J2"] = "K2",
    ["K1"] = "L", ["K2"] = "M"
};
var stoneFinishMatrix = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["Slate"] = "01", ["Earthy"] = "02", ["Sandstone"] = "03", ["Redrock"] = "04", ["Volcanic"] = "05"
};
foreach (var (assetGeometry, brushGeometry) in stoneGeometryMatrix)
foreach (var (style, finish) in stoneFinishMatrix)
{
    var fileName = $"Wall_Stone_{style}_{assetGeometry}_Straight_{assetGeometry}_1x1.png";
    var stoneWall = wallStraight with
    {
        SourceId = LibrarySourceIds.ForgottenAdventures,
        FilePath = $@"C:\FA\!Core_Settlements\Structures\Building\Walls_and_Curbs\Wall_Stone_{assetGeometry[0]}\{fileName}",
        RelativePath = $@"!Core_Settlements\Structures\Building\Walls_and_Curbs\Wall_Stone_{assetGeometry[0]}\{fileName}",
        FileName = fileName,
        Material = "Stone",
        Style = style,
        Variant = assetGeometry
    };
    var suggested = FaStoneWallRibbonCatalog.TryResolve([stoneWall], stoneWall.FamilyKey);
    Expect(suggested?.CspTool == "Buildings" && suggested.CspToolGroup == "Stone Walls"
        && suggested.CspRibbonName == $"Wall_Stone_{brushGeometry}_{finish}" && !suggested.IsManuallyConfirmed,
        $"Stone wall ribbon matrix failed for {assetGeometry}/{style}.");
}
var drowCFile = "Drow_Wall_Stone_Redrock_Straight_C1_1x1.png";
var drowCWall = wallStraight with
{
    SourceId = LibrarySourceIds.ForgottenAdventures,
    FilePath = $@"C:\FA\Underdark\Drow_Settlement\Structures\Building\Walls_and_Curbs\Drow_Wall_C\{drowCFile}",
    RelativePath = $@"Underdark\Drow_Settlement\Structures\Building\Walls_and_Curbs\Drow_Wall_C\{drowCFile}",
    FileName = drowCFile,
    Material = "Stone",
    Style = "Redrock",
    Theme = "Underdark",
    Variant = "C1"
};
var drowSuggestion = FaStoneWallRibbonCatalog.TryResolve([drowCWall], drowCWall.FamilyKey);
Expect(drowSuggestion is null,
    "Drow walls must not receive an ordinary Stone Wall ribbon without a real Drow brush.");
var drowSetPath = drowCWall with
{
    FilePath = @"C:\FA\Underdark\Drow_Wall_C\Paths\Drow_Wall_Stone_Redrock_C1_Path.png",
    RelativePath = @"Underdark\Drow_Settlement\Structures\Building\Walls_and_Curbs\Drow_Wall_C\Paths\Drow_Wall_Stone_Redrock_C1_Path.png",
    FileName = "Drow_Wall_Stone_Redrock_C1_Path.png",
    Family = "Drow Wall C", PartType = "Path"
};
var drowCatalog = WallConstructionCatalog.Build([drowCWall, drowSetPath]);
var drowSet = drowCatalog.Sets.Single();
var drowRecipe = new BuildRecipe { WallSet = new WallSetSelection { SetId = drowSet.Id } };
var drowPalette = BuildModeService.Populate(new AssetCatalogIndex([drowCWall, drowSetPath]), drowCatalog,
    drowRecipe, new Dictionary<string, RibbonMapping>(), LibrarySourceIds.ForgottenAdventures);
Expect(drowPalette.WallRibbon.Status == WallRibbonResolutionStatus.Missing,
    "Planner must identify the absent Drow ribbon instead of suggesting an ordinary Stone Wall brush.");
var manualDrowKey = BuildModeService.WallMappingKey(drowSet);
var manualDrowMapping = new RibbonMapping { FamilyKey = manualDrowKey, CspTool = "Custom", CspToolGroup = "Walls", CspRibbonName = "My Drow Wall" };
var manualDrowPalette = BuildModeService.Populate(new AssetCatalogIndex([drowCWall, drowSetPath]), drowCatalog,
    drowRecipe, new Dictionary<string, RibbonMapping> { [manualDrowKey] = manualDrowMapping },
    LibrarySourceIds.ForgottenAdventures);
Expect(ReferenceEquals(manualDrowPalette.WallRibbon.Mapping, manualDrowMapping),
    "A known FA ribbon suggestion overrode a manually confirmed wall mapping.");
var drowAWall = drowCWall with
{
    FilePath = drowCWall.FilePath.Replace("Drow_Wall_C", "Drow_Wall_A"),
    RelativePath = drowCWall.RelativePath.Replace("Drow_Wall_C", "Drow_Wall_A")
};
Expect(FaStoneWallRibbonCatalog.TryResolve([drowAWall], drowAWall.FamilyKey) is null,
    "Unproven Drow wall families must not receive invented Stone Wall ribbon associations.");
var drowUniversalOverlay = drowCWall with
{
    FilePath = @"C:\FA\Underdark\Drow_Wall_Overlay_Metal_Gold_Universal_Endpiece_A1_1x1.png",
    RelativePath = @"Underdark\Drow_Settlement\Structures\Building\Walls_and_Curbs\Drow_Wall_Overlay_Metal_Gold_Universal_Endpiece_A1_1x1.png",
    FileName = "Drow_Wall_Overlay_Metal_Gold_Universal_Endpiece_A1_1x1.png",
    Material = "Metal", Style = "Gold", Family = "Drow Wall Overlay", Variant = "A1", PartType = "Overlay"
};
var drowDetailSet = WallConstructionCatalog.Build([drowSetPath, drowUniversalOverlay]).Sets.Single();
Expect(drowDetailSet.Details.Single() == drowUniversalOverlay,
    "Universal Drow overlay end pieces were not offered to every compatible Drow wall set.");

AssetRecord MixedWall(string fileName) => wallStraight with
{
    SourceId = LibrarySourceIds.ForgottenAdventures,
    FilePath = $@"C:\FA\!Core_Settlements\Structures\Building\Walls_and_Curbs\{fileName}",
    RelativePath = $@"!Core_Settlements\Structures\Building\Walls_and_Curbs\{fileName}",
    FileName = fileName
};
var stoneWoodFinishes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
{
    ["Slate"] = ["Ashen", "Dark", "Light", "Red", "Walnut"],
    ["Earthy"] = ["Ashen", "Dark", "Light", "Red", "Walnut"],
    ["Sandstone"] = ["Ashen", "Dark", "Light"],
    ["Redrock"] = ["Dark", "Red"],
    ["Volcanic"] = ["Red", "Walnut"]
};
foreach (var geometry in new[] { "A", "B", "C", "D" })
foreach (var (style, woodColors) in stoneWoodFinishes)
foreach (var woodColor in woodColors)
{
    var asset = MixedWall($"Wall_StoneWood_{style}_{woodColor}_{geometry}_Straight_A_1x1.png");
    var suggestion = FaMixedWallRibbonCatalog.TryResolve([asset], asset.FamilyKey);
    Expect(suggestion?.CspToolGroup == "Stone Wood Walls"
        && suggestion.CspRibbonName == $"Wall_StoneWood_{geometry}_{stoneFinishMatrix[style]}_{woodColor}",
        $"Stone+Wood ribbon mapping failed for {geometry}/{style}/{woodColor}.");
}
var unavailableStoneWood = MixedWall("Wall_StoneWood_Sandstone_Red_A_Straight_A_1x1.png");
Expect(FaMixedWallRibbonCatalog.TryResolve([unavailableStoneWood], unavailableStoneWood.FamilyKey) is null,
    "A nonexistent Stone+Wood color/finish brush combination was invented.");

foreach (var geometry in new[] { "A1", "A2", "B", "C1", "C2", "D" })
foreach (var woodColor in new[] { "Ashen", "Dark", "Light", "Red", "Walnut" })
{
    var asset = MixedWall($"Wall_PlasterWood_{woodColor}_{geometry}_Straight_A_1x1.png");
    var suggestion = FaMixedWallRibbonCatalog.TryResolve([asset], asset.FamilyKey);
    Expect(suggestion?.CspToolGroup == "Plaster Wood Walls"
        && suggestion.CspRibbonName == $"Wall_PlasterWood_{geometry}_{woodColor}",
        $"Plaster+Wood ribbon mapping failed for {geometry}/{woodColor}.");
}

var woodGeometryMatrix = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["A"] = "A", ["B"] = "B", ["C"] = "C", ["D"] = "D",
    ["E"] = "E1", ["F"] = "E2", ["G"] = "E3"
};
foreach (var (assetGeometry, brushGeometry) in woodGeometryMatrix)
foreach (var woodColor in new[] { "Ashen", "Dark", "Light", "Red", "Walnut" })
{
    var asset = MixedWall($"Wall_Wood_{woodColor}_{assetGeometry}_Straight_Path.png");
    var suggestion = FaMixedWallRibbonCatalog.TryResolve([asset], asset.FamilyKey);
    var brushExists = brushGeometry is not ("E2" or "E3") || woodColor is not ("Red" or "Walnut");
    Expect(brushExists
            ? suggestion?.CspToolGroup == "Wood Walls" && suggestion.CspRibbonName == $"Wall_Wood_{brushGeometry}_{woodColor}"
            : suggestion is null,
        $"Wood ribbon availability mapping failed for source {assetGeometry}/{woodColor}.");
}
var mixedBuildAsset = MixedWall("Wall_StoneWood_Redrock_Red_C_Straight_Path.png");
var mixedPalette = BuildModeService.Populate([mixedBuildAsset], new BuildRecipe(),
    new Dictionary<string, RibbonMapping>(), LibrarySourceIds.ForgottenAdventures);
Expect(mixedPalette.WallRibbon.Status == WallRibbonResolutionStatus.Resolved
    && mixedPalette.WallRibbon.Mapping?.CspRibbonName == "Wall_StoneWood_C_04_Red",
    "Planner did not use the confirmed Stone+Wood ribbon association.");

foreach (var geometry in new[] { "A", "B" })
foreach (var (style, finish) in stoneFinishMatrix)
{
    var brickWallCandidate = MixedWall($"Wall_Brick_{style}_{geometry}_Straight_Path.png");
    var suggestion = FaAdditionalWallRibbonCatalog.TryResolve([brickWallCandidate], brickWallCandidate.FamilyKey);
    Expect(suggestion?.CspToolGroup == "Brick Walls" && suggestion.CspRibbonName == $"Wall_Brick_{geometry}_{finish}",
        $"Brick ribbon mapping failed for {geometry}/{style}.");
}
foreach (var (style, finish) in stoneFinishMatrix)
foreach (var geometry in new[] { "A1", "A2" })
foreach (var metal in new[] { "Gray", "Rusty" })
{
    var stoneMetal = MixedWall($"Wall_StoneMetal_{style}_{metal}_{geometry}_Straight_Path.png");
    var suggestion = FaAdditionalWallRibbonCatalog.TryResolve([stoneMetal], stoneMetal.FamilyKey);
    Expect(suggestion?.CspToolGroup == "Stone Metal Walls"
           && suggestion.CspRibbonName == $"Wall_StoneMetal_{geometry}_{finish}_{metal}",
        $"Stone+Metal ribbon mapping failed for {geometry}/{style}/{metal}.");
}
foreach (var geometry in new[] { "A", "B", "C" })
foreach (var (style, colors) in stoneWoodFinishes)
foreach (var woodColor in colors)
{
    var brickWood = MixedWall($"Wall_BrickWood_{style}_{woodColor}_{geometry}_Straight_Path.png");
    var suggestion = FaAdditionalWallRibbonCatalog.TryResolve([brickWood], brickWood.FamilyKey);
    Expect(suggestion?.CspToolGroup == "Brick Wood Walls"
           && suggestion.CspRibbonName == $"Wall_BrickWood_{geometry}_{stoneFinishMatrix[style]}_{woodColor}",
        $"Brick+Wood ribbon mapping failed for {geometry}/{style}/{woodColor}.");
}
var plasterWall = MixedWall("Wall_Plaster_White_A_Straight_Path.png");
Expect(FaAdditionalWallRibbonCatalog.TryResolve([plasterWall], plasterWall.FamilyKey)?.CspRibbonName == "Wall_Plaster_A_01",
    "Plaster wall ribbon mapping failed.");
foreach (var geometry in new[] { "A", "B" })
foreach (var (style, finish) in stoneFinishMatrix)
{
    var curb = MixedWall($"Curb_Stone_{style}_{geometry}_Straight_Path.png");
    var suggestion = FaAdditionalWallRibbonCatalog.TryResolve([curb], curb.FamilyKey);
    Expect(suggestion?.CspToolGroup == "Curbs" && suggestion.CspRibbonName == $"Curb_Stone_{geometry}_{finish}",
        $"Curb ribbon mapping failed for {geometry}/{style}.");
}
var unsupportedBrickC = MixedWall("Wall_Brick_Slate_C_Straight_Path.png");
Expect(FaAdditionalWallRibbonCatalog.TryResolve([unsupportedBrickC], unsupportedBrickC.FamilyKey) is null,
    "A legacy generic Brick C brush was incorrectly applied to a modern finish-specific set.");

AssetRecord SpecialtyWall(string fileName, string theme = "All") => MixedWall(fileName) with { Theme = theme };
foreach (var color in new[] { "Blue", "Gray", "Green", "Purple" })
foreach (var geometry in new[] { "A", "B", "C", "D", "E", "F", "G" })
{
    var asset = SpecialtyWall($"Ceremorph_Wall_{color}_Corner_{geometry}1_1x1.png", "Astral");
    var suggestion = FaSpecialtyWallRibbonCatalog.TryResolve([asset], asset.FamilyKey);
    Expect(suggestion?.CspToolGroup == "Ceremorph Walls"
           && suggestion.CspRibbonName == $"Ceremorph_Wall_{color}_{geometry}",
        $"Ceremorph ribbon mapping failed for {color}/{geometry}.");
}

foreach (var color in new[] { "Red", "Sandstone", "White" })
foreach (var shape in new[] { "Sloped", "Thin", "Wide" })
foreach (var geometry in shape == "Sloped" ? new[] { "A", "B", "C", "D" } : new[] { "A", "B", "C" })
{
    var asset = SpecialtyWall($"Wall_Adobe_{color}_{shape}_{geometry}1_Path.png", "Desert");
    var brushShape = shape == "Sloped" ? "Angled" : shape;
    var suggestion = FaSpecialtyWallRibbonCatalog.TryResolve([asset], asset.FamilyKey);
    Expect(suggestion?.CspRibbonName == $"Desert_Wall_Adobe_{color}_{brushShape}_{geometry}",
        $"Adobe ribbon mapping failed for {color}/{shape}/{geometry}.");
}

foreach (var geometry in new[] { "A", "B", "C" })
foreach (var style in new[] { "PatternA", "PatternB", "PatternC", "PatternD", "Sandstone", "Slate", "Terracotta", "White" })
{
    var asset = SpecialtyWall($"Wall_Tile_{geometry}_{style}_A1_Path.png", "Desert");
    Expect(FaSpecialtyWallRibbonCatalog.TryResolve([asset], asset.FamilyKey)?.CspRibbonName == $"Tile_Wall_{geometry}_{style}",
        $"Tile-wall ribbon mapping failed for {geometry}/{style}.");
}

var metalGeometryMatrix = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["A"] = "A", ["B"] = "B", ["C"] = "C", ["D1"] = "D",
    ["D2"] = "E", ["E1"] = "F1", ["E2"] = "F2", ["F"] = "G"
};
foreach (var color in new[] { "Brass", "Gray", "Polished", "Rusty", "Soot" })
foreach (var (assetGeometry, brushGeometry) in metalGeometryMatrix)
{
    var asset = SpecialtyWall($"Wall_Metal_{color}_{assetGeometry}_Straight_A_3x1.png", "Industrial");
    Expect(FaSpecialtyWallRibbonCatalog.TryResolve([asset], asset.FamilyKey)?.CspRibbonName == $"Wall_Metal_{brushGeometry}_{color}",
        $"Metal-wall ribbon mapping failed for {color}/{assetGeometry}.");
}

foreach (var color in new[] { "Frosty", "Ashen", "Dark", "Light", "Red", "Walnut" })
foreach (var geometry in new[] { "A", "B", "C" })
{
    var asset = SpecialtyWall($"Shack_Wall_Wood_A_{color}_Corner_{geometry}1_2x2.png", color == "Frosty" ? "Arctic" : "Woodlands");
    Expect(FaSpecialtyWallRibbonCatalog.TryResolve([asset], asset.FamilyKey)?.CspRibbonName == $"Shack_Wall_Wood_{geometry}_{color}",
        $"Shack-wall ribbon mapping failed for {color}/{geometry}.");
}

var specialtyPaletteAsset = SpecialtyWall("Ceremorph_Wall_Green_C1_Path.png", "Astral");
var specialtyPalette = BuildModeService.Populate([specialtyPaletteAsset], new BuildRecipe(),
    new Dictionary<string, RibbonMapping>(), LibrarySourceIds.ForgottenAdventures);
Expect(specialtyPalette.WallRibbon.Status == WallRibbonResolutionStatus.Resolved
       && specialtyPalette.WallRibbon.Mapping?.CspRibbonName == "Ceremorph_Wall_Green_C",
    "Planner did not use the curated Ceremorph ribbon association.");

var parsedCeremorphWall = ParseFa(@"Astral\Ceremorph_Settlement\Structures\Building\Walls_and_Curbs\Ceremorph_Wall_C\Ceremorph_Wall_Green_Corner_C1_1x1.png");
Expect(parsedCeremorphWall.Material == "Organic" && parsedCeremorphWall.Style == "Green"
       && parsedCeremorphWall.Family == "Ceremorph Wall" && parsedCeremorphWall.Variant == "C",
    "Ceremorph wall-set metadata does not expose color and construction independently in Planner.");
var parsedAdobeWall = ParseFa(@"Desert\Base_Desert_Settlement\Structures\Building\Walls_and_Curbs\Wall_Adobe_Wide_B\Wall_Adobe_Red_Wide_Broken_Ending_B1_1x1.png");
Expect(parsedAdobeWall.Material == "Adobe" && parsedAdobeWall.Style == "Red"
       && parsedAdobeWall.Family == "Wide" && parsedAdobeWall.Variant == "B",
    "Adobe wall-set metadata does not preserve width and construction in Planner.");
var parsedTileWall = ParseFa(@"Desert\Base_Desert_Settlement\Structures\Building\Walls_and_Curbs\Wall_Tile_C\Wall_Tile_C_PatternD_Connector_A1_1x1.png");
Expect(parsedTileWall.Material == "Tile" && parsedTileWall.Style == "PatternD"
       && parsedTileWall.Family == "Tile Wall" && parsedTileWall.Variant == "C",
    "Tile-wall metadata does not preserve pattern and construction in Planner.");
var parsedStoneMetalWall = ParseFa(@"!Core_Settlements\Structures\Building\Walls_and_Curbs\Wall_StoneMetal_A\Wall_StoneMetal_Earthy_Rusty_A1_Connector_A_1x1.png");
Expect(parsedStoneMetalWall.Material == "Stone_Metal" && parsedStoneMetalWall.Style == "Earthy"
       && parsedStoneMetalWall.Family == "Rusty" && parsedStoneMetalWall.Variant == "A1",
    "Stone+Metal wall metadata does not preserve both finishes.");
var parsedShackWall = ParseFa(@"Woodlands\Base_Woodlands_Settlement\Structures\Building\Walls_and_Curbs\Shack_Wall_Wood_A\Shack_Wall_Wood_A_Walnut_Corner_B2_2x2.png");
Expect(parsedShackWall.Style == "Walnut" && parsedShackWall.Family == "Shack Wall" && parsedShackWall.Variant == "B",
    "Shack-wall metadata does not expose the actual A/B/C ribbon construction.");
var parsedShackRailing = ParseFa(@"Woodlands\Base_Woodlands_Settlement\Structures\Building\Walls_and_Curbs\Shack_Wall_Wood_A\Shack_Wall_Wood_A_Walnut_Railing_B1_Path.png");
Expect(parsedShackRailing.SubGroup == "Railings & Fences",
    "Shack railing paths remained mixed into wall ribbon sets.");
var parsedWallRubble = ParseFa(@"Desert\Base_Desert_Settlement\Structures\Building\Walls_and_Curbs\Wall_Tile_Rubble\Wall_Tile_Broken_Piece_Slate_A1_1x1.png");
Expect(parsedWallRubble.SubGroup == "Ruins & Rubble",
    "Loose wall rubble remained mixed into construction wall sets.");

var generic = new GenericAssetFilenameParser();
var genericRoot = @"C:\Generic";
var genericChair = generic.Parse(genericRoot, Path.Combine(genericRoot, "Props", "Furniture", "Chairs", "Chair_Wood_Ashen_A1.png"));
Expect(genericChair.Group == "Props" && genericChair.SubGroup == "Furniture" && genericChair.Family == "Seating", "Generic parser ignored clear folder context.");
Expect(genericChair.Material == "Wood" && genericChair.Style == "Ashen" && genericChair.Variant == "A1", "Generic parser missed conservative known metadata.");
var genericUnknown = generic.Parse(genericRoot, Path.Combine(genericRoot, "Oddities", "Relic_Mystic_Alloy_Rusty_A.png"));
Expect(genericUnknown.Group == "Unspecified" && genericUnknown.Material == "Unspecified" && genericUnknown.Style == "Rusty", "Generic parser promoted an unknown noun to Material.");
Expect(genericUnknown.RawTokens.Contains("Mystic") && genericUnknown.RelativePath.Contains("Oddities"), "Generic parser failed to preserve raw filename/path data.");

const string curatedCavePath = @"Volcanic\!Wilderness\Textures\Dirt\Cave_Floor_05_A.jpg";
var curatedCave = ParseFa(curatedCavePath);
Expect(curatedCave.SubGroup == "Cave & Underdark" && curatedCave.Family == "Cave Terrain",
    "FA parser did not recognize a cave texture from semantic filename and folder clues.");
Expect(AssetSemanticClassifier.Classify(curatedCave) is { Category: "Nature", Type: "Cave & Underdark", Subtype: "Cave Surfaces" },
    "A cave texture did not become a Nature / Cave & Underdark / Cave Surfaces asset.");
var dwarvenFloorPlaque = ParseFa(@"Mountain\Mountain_Dwarf_Settlement\Structures\Aesthetics\Floor_Inlays\Plaques\Dwarven_Floor_Metal_Black_Plaque_A1_2x4.png");
Expect(AssetSemanticClassifier.Classify(dwarvenFloorPlaque) is { Category: "Construction", Type: "Floors", Subtype: "Inlays & Plaques" },
    "A structural Dwarven floor plaque leaked into repeating Floor Surfaces.");
var hedgeMazePath = ParseFa(@"Woodlands\Botanical_Garden_Settlement\Structures\Hedgemaze\Paths\Hedge_Maze_Thin_Green_Path.png");
var hedgeTaxonomy = AssetSemanticClassifier.Classify(hedgeMazePath);
Expect(hedgeMazePath.Group == "Building" && hedgeMazePath.SubGroup == "Railings & Fences"
       && hedgeMazePath.Family == "Hedge Maze" && hedgeMazePath.Material == "Organic"
       && hedgeTaxonomy is { Type: "Boundaries", Subtype: "Hedges & Mazes" } && hedgeTaxonomy.Contexts.Contains("Build: Hedge Maze"),
    "Hedge-maze path did not become a paintable construction boundary.");
var palisade = ParseFa(@"!Core_Settlements\Structures\Building\Palisades\Palisade_Gate_Wood_Ashen_A1_2x1.png");
Expect(AssetSemanticClassifier.Classify(palisade) is { Category: "Construction", Type: "Defenses & Barricades", Subtype: "Palisades" },
    "Palisades remained mixed into ordinary boundaries and fences.");
var previouslyIndexedPalisade = palisade with { SubGroup = "Railings & Fences" };
Expect(AssetSemanticClassifier.Classify(previouslyIndexedPalisade) is { Category: "Construction", Type: "Defenses & Barricades", Subtype: "Palisades" },
    "An existing palisade index requires a reindex to receive the corrected semantic taxonomy.");
var walkwayTile = ParseFa(@"!Core_Settlements\Structures\Building\Wooden_Walkway_Tiles\Wooden_Walkway_Wood_Ashen_A1_2x2.png");
Expect(AssetSemanticClassifier.Classify(walkwayTile) is { Category: "Construction", Type: "Infrastructure", Subtype: "Platforms & Walkways" },
    "A genuine wooden walkway tile left Platforms & Walkways.");
var loosePlank = ParseFa(@"!Core_Settlements\Structures\Building\Planks\Uneven\Plank_Wood_Ashen_A1_1x2.png");
Expect(AssetSemanticClassifier.Classify(loosePlank) is { Category: "Construction", Type: "Construction Materials", Subtype: "Planks & Lumber" },
    "Loose building planks remained mixed into Platforms & Walkways.");
var cellFloor = ParseFa(@"!Core_Settlements\Structures\Building\Cells\Broken\Floor_Cell_Broken_Stone_Slate_Metal_Rusty_A1_2x2.png");
Expect(AssetSemanticClassifier.Classify(cellFloor) is { Category: "Construction", Type: "Floors", Subtype: "Cells & Chambers" },
    "Cell floor components remained mixed into Platforms & Walkways.");
var propeller = ParseFa(@"Industrial\Base_Industrial_Settlement\Structures\Mechanical_Parts\Propellers\Propeller_Metal_Rusty_A1_2x2.png");
Expect(AssetSemanticClassifier.Classify(propeller) is { Category: "Equipment & Work", Type: "Mechanical Parts", Subtype: "Propellers" },
    "Propellers remained mixed into construction platforms.");
var windTurbine = ParseFa(@"!Core_Settlements\Structures\Mechanical_Parts\Wind_Turbines\Wind_Turbine_Metal_Rusty_A1_4x4.png");
Expect(AssetSemanticClassifier.Classify(windTurbine) is { Category: "Equipment & Work", Type: "Mechanical Parts", Subtype: "Wind Turbines" },
    "Wind turbine parts remained mixed into construction platforms.");
var infrastructurePipe = ParseFa(@"!Core_Settlements\Structures\Mechanical_Parts\Pipes\Medium\Rusty\Pipe_Medium_Metal_Rusty_A1_1x1.png");
Expect(AssetSemanticClassifier.Classify(infrastructurePipe) is { Category: "Construction", Type: "Infrastructure", Subtype: "Pipes & Conduits" },
    "Architectural pipe components left construction Infrastructure.");
var mechanicalRibbon = ParseFa(@"Industrial\Base_Industrial_Settlement\Structures\Mechanical_Parts\Mechanical_Paths\Cog_Linear_Metal_Rusty_B_Path.png");
Expect(AssetSemanticClassifier.Classify(mechanicalRibbon) is { Category: "Equipment & Work", Type: "Mechanical Parts", Subtype: "Paths & Ribbons" },
    "Mechanical ribbon assets were absorbed into static cog components.");
var woodenStairs = ParseFa(@"!Core_Settlements\Structures\Building\Stairs_and_Ladders\Stairs_Wood\Stairs_Wood_Ashen_A1_2x2.png");
Expect(AssetSemanticClassifier.Classify(woodenStairs) is { Category: "Construction", Type: "Stairs & Access", Subtype: "Stairs" },
    "The Stairs_and_Ladders parent folder caused an actual staircase to classify as a ladder.");
var ladder = ParseFa(@"!Core_Settlements\Structures\Building\Stairs_and_Ladders\Ladders\Ladder_Wood_Ashen_A1_1x3.png");
Expect(AssetSemanticClassifier.Classify(ladder) is { Category: "Construction", Type: "Stairs & Access", Subtype: "Ladders" },
    "Actual ladders did not remain distinct from stairs.");
var basin = ParseFa(@"!Core_Settlements\Structures\Water_Structures\Basin_Parts\Basin_Walls\Basin_Wall_Stone_Slate_A1_2x1.png");
Expect(AssetSemanticClassifier.Classify(basin) is { Category: "Construction", Type: "Water Features", Subtype: "Basins" },
    "Basin construction pieces remained mislabeled as drains and channels.");
var aqueduct = ParseFa(@"!Core_Settlements\Structures\Water_Structures\Aqueducts\Aqueduct_Stone_Slate_A1_2x1.png");
Expect(AssetSemanticClassifier.Classify(aqueduct) is { Category: "Construction", Type: "Water Features", Subtype: "Aqueducts" },
    "Aqueduct pieces remained mislabeled as drains and channels.");
var horrorTeeth = ParseFa(@"Horror\!Wilderness\Decor\Teeth\Teeth_Flesh_Pale_A1.png");
Expect(AssetSemanticClassifier.Classify(horrorTeeth) is { Category: "Nature", Type: "Organic Terrain", Subtype: "Teeth & Saliva" },
    "Organic-horror terrain remained hidden in generic decor.");
var pottery = ParseFa(@"!Core_Settlements\Decor\Pottery\Pottery_Clay_Earthy_A1.png");
Expect(AssetSemanticClassifier.Classify(pottery) is { Category: "Furnishings & Objects", Type: "Decor & Display", Subtype: "Pottery & Ceramics" },
    "Pottery remained hidden in generic decor.");
var coinTexture = ParseFa(@"!Core_Settlements\Textures\Misc\Coins_Gold.jpg");
Expect(AssetSemanticClassifier.Classify(coinTexture) is { Category: "Furnishings & Objects", Type: "Treasure & Valuables", Subtype: "Coin Textures" },
    "Coin textures remained hidden in generic decor.");
var archaeologyTools = ParseFa(@"Desert\Base_Desert_Settlement\Workplace_Equipment\Archeology\Archeology_Tool_Metal_Rusty_A1.png");
Expect(AssetSemanticClassifier.Classify(archaeologyTools) is { Category: "Equipment & Work", Type: "Tools & Workstations", Subtype: "Archaeology" },
    "Archaeology tools remained in the general workplace fallback.");
var volcanicCliffPath = ParseFa(@"Volcanic\!Wilderness\Elevation\Cliff_Paths\Cliff_Stone_Volcanic_A1_Path_A1.png");
var cliffTaxonomy = AssetSemanticClassifier.Classify(volcanicCliffPath);
Expect(volcanicCliffPath.Group == "Nature" && volcanicCliffPath.SubGroup == "Terrain"
       && volcanicCliffPath.Family == "Cliffs" && cliffTaxonomy is { Type: "Terrain", Subtype: "Cliffs & Ledges" }
       && cliffTaxonomy.Contexts.Contains("Paint: Terrain Ribbon"),
    "Volcanic cliff path did not become a paintable terrain-ribbon asset.");
var desertDrape = ParseFa(@"Desert\Base_Desert_Settlement\Textures\Drapes\Drapes_Texture_A_Blue.png");
Expect(desertDrape.Group == "Props" && desertDrape.SubGroup == "Textiles" && desertDrape.Family == "Drapes",
    "Settlement drape texture was incorrectly treated as a floor or terrain texture.");
var desertPergola = ParseFa(@"Desert\Base_Desert_Settlement\Textures\Pergola\Pergola_Lattice_Wood_Ashen_B1.png");
Expect(desertPergola.Group == "Building" && desertPergola.SubGroup == "Roofs" && desertPergola.Family == "Pergola Lattice",
    "Pergola lattice texture did not become a roof construction detail.");
var botanicalSurface = ParseFa(@"Woodlands\Botanical_Garden_Settlement\Textures\Botanical_Garden\Botanical_Garden_Metal_Brass_Glass_A1.png");
Expect(botanicalSurface.Group == "Building" && botanicalSurface.SubGroup == "Floors"
       && botanicalSurface.Material == "Metal_Glass" && botanicalSurface.Family == "Botanical Garden",
    "Botanical Garden metal/glass surface did not become a building surface.");
var industrialFrame = ParseFa(@"Industrial\Base_Industrial_Settlement\Textures\Metal_Overlay_Frames\Metal_Frame_01_A1.png");
Expect(industrialFrame.Group == "Building" && industrialFrame.SubGroup == "Floors"
       && industrialFrame.Family == "Floor Overlay Frames",
    "Industrial floor overlay frame did not become a floor detail.");
var crystalCluster = ParseFa(@"Underdark\!Wilderness\Decor\Crystals\Blue\Crystal_Blue_Cluster_A1_2x2.png");
Expect(crystalCluster.Group == "Nature" && crystalCluster.SubGroup == "Cave & Underdark" && crystalCluster.Family == "Crystals",
    "FA parser did not classify an Underdark crystal cluster.");
var crystalRock = ParseFa(@"Underdark\!Wilderness\Decor\Crystals\Blue\Sliver\Underdark_Crystal_Rock_Sliver_Blue_A1_1x1.png");
Expect(crystalRock.SubGroup == "Cave & Underdark" && crystalRock.Family == "Crystal Rocks",
    "FA parser did not classify a colored crystal rock.");
var stoneCrystalRock = ParseFa(@"Underdark\!Wilderness\Decor\Crystals\Stone\Underdark_Crystal_Rock_Stone_A1_1x1.png");
Expect(stoneCrystalRock.SubGroup == "Cave & Underdark" && stoneCrystalRock.Family == "Crystal Rock",
    "FA parser did not preserve the stone crystal-rock family.");
var mosaicFloor = ParseFa(@"Desert\Base_Desert_Settlement\Structures\Aesthetics\Floor_Breaks\Mosaic_Floor_Break_A\Mosaic_Floor_Break_Blue_White_Center_A1_1x1.png");
Expect(mosaicFloor.Material == "Mosaic_Tile" && mosaicFloor.Style == "Blue_White",
    "FA parser did not convert Mosaic tile metadata to material plus color style.");
var selection = new ExplorerSelection();
var order = new[] { "a", "b", "c", "d" };
selection.Select("b", order); selection.Select("d", order, shift: true); Expect(selection.Selected.Order().SequenceEqual(new[] { "b", "c", "d" }), "Shift selection failed.");
selection.Select("c", order, control: true); Expect(!selection.Selected.Contains("c"), "Control selection toggle failed.");
selection.Clear(); Expect(selection.Selected.Count == 0, "Selection clear failed.");

var temp = Path.Combine(Path.GetTempPath(), "DymndAssetBrowserTests", Guid.NewGuid().ToString("N"));
var stableProfile = Environment.GetEnvironmentVariable("USERPROFILE");
if (!string.IsNullOrWhiteSpace(stableProfile))
    Expect(ApplicationStateStore.GetDefaultAppDataRoot().Equals(Path.Combine(stableProfile, "AppData", "Local", "DymndAssetBrowser"), StringComparison.OrdinalIgnoreCase),
        "Default persistence root did not remain anchored to the real Windows user profile.");
try
{
    var faRoot = Path.Combine(temp, "fa"); var genericOneRoot = Path.Combine(temp, "generic-one"); var genericTwoRoot = Path.Combine(temp, "generic-two");
    Directory.CreateDirectory(Path.Combine(faRoot, "!Core_Settlements", "Clutter", "Food", "Prepared_Meals")); Directory.CreateDirectory(genericOneRoot); Directory.CreateDirectory(genericTwoRoot);
    Expect(FaLibraryDetector.ResolveRoot(faRoot) == Path.GetFullPath(faRoot), "FA setup did not accept the _Assets-equivalent root itself.");
    var faParent = Path.Combine(temp, "fa-parent");
    var nestedFaRoot = Path.Combine(faParent, "_Assets");
    Directory.CreateDirectory(Path.Combine(nestedFaRoot, "!Core_Settlements"));
    Expect(FaLibraryDetector.ResolveRoot(faParent) == Path.GetFullPath(nestedFaRoot), "FA setup did not resolve _Assets from its parent folder.");
    Expect(FaLibraryDetector.ResolveRoot(genericOneRoot) is null, "FA setup accepted a folder without the FA core marker.");
    var soupPath = Path.Combine(faRoot, "!Core_Settlements", "Clutter", "Food", "Prepared_Meals", "Soup_Bowl_A1.png"); File.WriteAllBytes(soupPath, []);
    File.WriteAllBytes(Path.Combine(faRoot, "photo.JPG"), []); File.WriteAllBytes(Path.Combine(faRoot, "ignored.webp"), []);
    var duplicateOne = Path.Combine(genericOneRoot, "shared.png"); var duplicateTwo = Path.Combine(genericTwoRoot, "shared.png"); File.WriteAllBytes(duplicateOne, []); File.WriteAllBytes(duplicateTwo, []);
    var faSource = new AssetLibrarySource { Id = LibrarySourceIds.ForgottenAdventures, Name = "Forgotten Adventures", RootPath = faRoot, ParserProfile = LibraryParserProfiles.Fa };
    var genericOneSource = new AssetLibrarySource { Id = "generic-one", Name = "Generic One", RootPath = genericOneRoot, ParserProfile = LibraryParserProfiles.Generic };
    var genericTwoSource = new AssetLibrarySource { Id = "generic-two", Name = "Generic Two", RootPath = genericTwoRoot, ParserProfile = LibraryParserProfiles.Generic };
    var indexer = new AssetLibraryIndexer();
    var faIndexed = await indexer.ScanLibraryAsync(faSource); var genericOne = await indexer.ScanLibraryAsync(genericOneSource); var genericTwo = await indexer.ScanLibraryAsync(genericTwoSource);
    Expect(faIndexed.Count == 2, "FA full-library index did not include PNG/JPG or ignored unsupported files.");
    Expect(genericOne.Single().SourceId == "generic-one" && genericTwo.Single().SourceId == "generic-two", "Indexer did not stamp source identity.");
    Expect(genericOne.Single().StableIdentity != genericTwo.Single().StableIdentity, "Identical filenames from different libraries collided.");

    var foundryData = Path.Combine(temp, "foundry-data");
    var directFoundryFile = Path.Combine(foundryData, "assets", "My Tile #1.png");
    Directory.CreateDirectory(Path.GetDirectoryName(directFoundryFile)!);
    File.WriteAllBytes(directFoundryFile, []);
    var directFoundryAsset = brick with { SourceId = "generic-one", FilePath = directFoundryFile, SourceRoot = foundryData, RelativePath = Path.GetRelativePath(foundryData, directFoundryFile) };
    Expect(FoundryAssetPathService.Resolve(directFoundryAsset, foundryData) == "assets/My%20Tile%20%231.png",
        "Foundry filepath did not become a URL-safe path relative to the Data folder.");

    var mirroredRelative = Path.Combine("Arctic", "Tiles", "Ice Floor.png");
    var mirroredFoundryFile = Path.Combine(foundryData, "fa-nexus-assets", Path.ChangeExtension(mirroredRelative, ".webp"));
    Directory.CreateDirectory(Path.GetDirectoryName(mirroredFoundryFile)!);
    File.WriteAllBytes(mirroredFoundryFile, []);
    var mirroredFaAsset = brick with { FilePath = Path.Combine(faRoot, mirroredRelative), SourceRoot = faRoot, RelativePath = mirroredRelative };
    Expect(FoundryAssetPathService.Resolve(mirroredFaAsset, foundryData) == "fa-nexus-assets/Arctic/Tiles/Ice%20Floor.webp",
        "Foundry filepath did not resolve the FA Nexus WebP mirror.");
    Expect(FoundryAssetPathService.Resolve(mirroredFaAsset with { RelativePath = @"Missing\Asset.png" }, foundryData) is null,
        "Foundry filepath returned an unverified path for a missing mirror asset.");

    var parsedSoup = faIndexed.Single(asset => asset.FilePath == soupPath);
    var currentState = new ApplicationState
    {
        Libraries = [faSource],
        Assets = [parsedSoup],
        RibbonMappings = new Dictionary<string, RibbonMapping>(StringComparer.OrdinalIgnoreCase)
        {
            ["Building|Walls|Brick|Earthy|All|All|A"] = new()
            {
                FamilyKey = "Building|Walls|Brick|Earthy|All|All|A",
                CspTool = "Buildings"
            }
        }
    };

    var store = new ApplicationStateStore(Path.Combine(temp, "state")); await store.SaveAsync(currentState with { BuildRecipe = buildRecipe }); var reloaded = await store.LoadAsync();
    Expect(File.Exists(store.IndexPath) && File.Exists(store.StatePath), "Split settings/SQLite persistence files were not created.");
    Expect(reloaded.SchemaVersion == 6 && reloaded.Libraries.Single().ParserProfile == LibraryParserProfiles.Fa,
        "Current application state did not survive persistence.");
    Expect(reloaded.RibbonMappings.ContainsKey("Building|Walls|Brick|Earthy|All|All|A"),
        "Current ribbon mapping did not survive persistence.");
    Expect(reloaded.BuildRecipe.IsPopulated && reloaded.BuildRecipe.WallSet.SetId == buildWallSet.Id
        && reloaded.BuildRecipe.Floor.Theme == "Shack", "Build recipe and generated-palette state did not survive persistence.");
    var updatedSoup = reloaded.Assets.Single() with { Theme = "Persistence Probe" };
    await store.UpsertAssetsAsync([updatedSoup]);
    await store.SaveMetadataAsync(reloaded);
    Expect((await store.LoadAsync()).Assets.Single().Theme == "Persistence Probe", "Incremental asset upsert did not survive restart.");

    var pngPath = Path.Combine(temp, "rotation-source.png"); CreateTestPng(pngPath);
    var imageCache = new ImageCacheService(Path.Combine(temp, "cache"));
    Expect(await imageCache.GetDragFileAsync(pngPath, 0) == pngPath, "Zero-degree drag stopped using the untouched source image.");
    var rotatedPath = await imageCache.GetDragFileAsync(pngPath, 90);
    Expect(File.Exists(rotatedPath) && new FileInfo(rotatedPath).Length > 0, "Rotation cache did not produce a finalized PNG.");
    using (var stream = File.Open(rotatedPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
    {
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        Expect(decoder.Frames[0].PixelWidth == 1 && decoder.Frames[0].PixelHeight == 2, "Rotation cache dimensions were wrong.");
    }
    var flippedPath = await imageCache.GetDragFileAsync(pngPath, 0, flipHorizontally: true);
    Expect(flippedPath != pngPath && File.Exists(flippedPath), "Horizontal flip did not produce a cached PNG.");
    using (var stream = File.Open(flippedPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
    {
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var flippedFrame = decoder.Frames[0];
        var pixels = new byte[8];
        flippedFrame.CopyPixels(pixels, 8, 0);
        Expect(flippedFrame.PixelWidth == 2 && flippedFrame.PixelHeight == 1, "Horizontal flip changed the image dimensions.");
        Expect(pixels[0] == 0 && pixels[1] == 255 && pixels[2] == 0 && pixels[3] == 128,
            "Horizontal flip did not mirror the source pixels.");
    }
    var buildDragAsset = palette.Walls[0] with { FilePath = pngPath, FileName = Path.GetFileName(pngPath) };
    var buildDragPath = await imageCache.GetDragFileAsync(buildDragAsset.FilePath, 90);
    Expect(File.Exists(buildDragPath), "A Build palette asset did not remain compatible with the existing drag-file pipeline.");
    var lazyTile = new AssetTileViewModel(buildDragAsset);
    Expect(lazyTile.Thumbnail is null, "Asset tiles eagerly decoded thumbnails before becoming visible.");
    await lazyTile.EnsureThumbnailAsync(imageCache);
    Expect(lazyTile.Thumbnail is not null, "A visible asset tile could not decode its thumbnail.");
    lazyTile.ReleaseThumbnail();
    Expect(lazyTile.Thumbnail is null, "An offscreen asset tile retained its decoded thumbnail.");
}
finally
{
    try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
}

if (args.Contains("--real-scan", StringComparer.OrdinalIgnoreCase))
{
    var rootArgument = Array.FindIndex(args, value => value.Equals("--fa-root", StringComparison.OrdinalIgnoreCase));
    var realRoot = rootArgument >= 0 && rootArgument + 1 < args.Length
        ? args[rootArgument + 1]
        : Environment.GetEnvironmentVariable("DYMND_FA_LIBRARY_ROOT");
    if (string.IsNullOrWhiteSpace(realRoot) || !Directory.Exists(realRoot))
    {
        Console.Error.WriteLine("--real-scan requires --fa-root <path> or the DYMND_FA_LIBRARY_ROOT environment variable.");
        return 2;
    }
    var source = new AssetLibrarySource { Id = LibrarySourceIds.ForgottenAdventures, Name = "Forgotten Adventures", RootPath = realRoot, ParserProfile = LibraryParserProfiles.Fa };
    var real = await new AssetLibraryIndexer().ScanLibraryAsync(source);
    Expect(real.Count > 160_000, $"Real full-library scan was unexpectedly small ({real.Count:N0}).");
    var realTypeIndex = new AssetCatalogIndex(real);
    var realTypeValues = realTypeIndex.AllValues(AssetFacet.Type);
    var expectedTypes = new[]
    {
        "Walls", "Floors", "Roofs", "Openings", "Terrain", "Flora", "Furniture", "Kitchen & Dining Clutter",
        "Infrastructure", "Water Features", "Ruins & Construction Debris", "Cave & Underdark",
        "Boundaries", "Supports", "Stairs & Access", "Boats & Ships", "Ingredients"
    };
    Expect(expectedTypes.All(realTypeValues.Contains),
        "The real library did not expose every expected Type.");
    var realSubtypeValues = realTypeIndex.AllValues(AssetFacet.Subtype);
    Expect(new[] { "Surfaces", "Ground Surfaces", "Cave Surfaces", "Wall Pieces", "Cracks & Damage" }.All(realSubtypeValues.Contains),
        "The real library did not expose every expected construction and nature Subtype.");
    var wallAdvancedScope = new AssetFilter(Category: "Construction", Type: "Walls");
    var wallSourceSets = realTypeIndex.ValuesForFacet(wallAdvancedScope, AssetFacet.SourceSet);
    Expect(wallSourceSets.Contains("Wall Adobe Cracks") && wallSourceSets.Contains("Wall Adobe Wide A")
        && !wallSourceSets.Contains("Adobe Wall"),
        "The real advanced Source Folder / Set facet exposed inferred Family labels instead of objective source collections.");
    var adobeCrackFolderMatches = realTypeIndex.Filter(wallAdvancedScope with { SourceSet = "Wall Adobe Cracks" });
    var adobeWideFolderMatches = realTypeIndex.Filter(wallAdvancedScope with { SourceSet = "Wall Adobe Wide A" });
    Expect(adobeCrackFolderMatches.Count == 45 && adobeCrackFolderMatches.All(asset =>
            AssetSemanticClassifier.Classify(asset).Subtype == "Cracks & Damage"),
        "The Adobe crack source set leaked construction pieces or lost crack assets.");
    Expect(adobeWideFolderMatches.Count > 20 && adobeWideFolderMatches.All(asset =>
            AssetSemanticClassifier.Classify(asset).Subtype == "Wall Pieces"),
        "The Adobe Wide A source set leaked detailing assets.");
    var adobeWideA1 = realTypeIndex.Filter(wallAdvancedScope with
    {
        SourceSet = "Wall Adobe Wide A",
        FilenameVariant = "A1"
    });
    Expect(adobeWideA1.Count > 0 && adobeWideA1.All(asset => AssetSourceMetadata.FilenameVariant(asset) == "A1"),
        "Filename Variant did not remain exact inside a selected source set.");
    foreach (var advancedProbe in new[]
    {
        wallAdvancedScope with { SourceSet = "Wall Adobe Cracks" },
        wallAdvancedScope with { SourceSet = "Wall Adobe Wide A", FilenameVariant = "A1" },
        new AssetFilter(Category: "Nature", Type: "Terrain", SourceSet: "Snow")
    })
        Expect(AssetQueries.Filter(real, advancedProbe).Select(asset => asset.StableIdentity)
                .SequenceEqual(realTypeIndex.Filter(advancedProbe).Select(asset => asset.StableIdentity)),
            "Indexed advanced filtering diverged from direct filtering during the real-library audit.");
    var realSemantics = real.Select(asset => (Asset: asset, Semantic: AssetSemanticClassifier.Classify(asset))).ToList();
    Expect(!realSemantics.Any(item => item.Semantic is { Type: "Decor & Display", Subtype: "" }
            or { Type: "Water Features", Subtype: "Drains & Channels" }
            or { Type: "Furniture", Subtype: "Other" }
            or { Type: "Stairs & Access", Subtype: "Access Components" }),
        "A retired catch-all taxonomy bucket was repopulated by the real library.");
    Expect(realSemantics.Count(item => item.Semantic is { Type: "Stairs & Access", Subtype: "Stairs" }) > 1_100
        && realSemantics.Count(item => item.Semantic is { Type: "Stairs & Access", Subtype: "Ladders" }) > 350,
        "The real Stairs_and_Ladders folder did not separate stairs from ladders.");
    var roofSourceAssets = realSemantics.Where(item =>
        item.Asset.RelativePath.Replace('\\', '/').Contains("/Textures/Roof/", StringComparison.OrdinalIgnoreCase)
        || item.Asset.RelativePath.Replace('\\', '/').Contains("/Building/Roofs/", StringComparison.OrdinalIgnoreCase)).ToList();
    Expect(roofSourceAssets.Count > 900 && roofSourceAssets.All(item => item.Semantic.Type == "Roofs"),
        "A true roof source-folder asset escaped the deferred Roofs taxonomy.");
    var realWallTypes = realTypeIndex.Filter(new AssetFilter(Type: "Walls"));
    Expect(realWallTypes.Count > 1_000
        && realWallTypes.Any(asset => AssetSemanticClassifier.Classify(asset) is { Type: "Openings", Subtype: "Arrow Slits" })
        && realWallTypes.Any(asset => AssetSemanticClassifier.Classify(asset) is { Type: "Openings", Subtype: "Arched Wall Inserts" }),
        "The real Walls Type did not include its wall pieces, details, and cross-listed structural additions.");
    var semanticUnsorted = real.Where(asset => AssetSemanticClassifier.Classify(asset).Category == "Unsorted").ToList();
    static string Slash(string value) => value.Replace('\\', '/');
    var postOfficeAssets = real.Where(asset => asset.FileName.Contains("Post_Office", StringComparison.OrdinalIgnoreCase)).ToList();
    Expect(postOfficeAssets.Count == 6 && postOfficeAssets.All(asset =>
            AssetSemanticClassifier.Classify(asset) is { Category: "Furnishings & Objects", Type: "Signs & Notices" }),
        "One or more Post Office decals/signs remained classified as office equipment.");
    var corrugatedPanels = real.Where(asset => asset.FileName.StartsWith("Corrugated_Panel_", StringComparison.OrdinalIgnoreCase)).ToList();
    Expect(corrugatedPanels.Count == 54 && corrugatedPanels.All(asset => asset is { Group: "Building", SubGroup: "Roofs" }
            && AssetSemanticClassifier.Classify(asset) is { Type: "Roofs", Subtype: "Corrugated Panels" } semantic
            && semantic.Paths.Any(path => path is { Type: "Construction Materials", Subtype: "Panels & Sheet Material" })),
        "One or more corrugated panels escaped their roofing/construction-material cross-listing.");
    var firewoodAssets = real.Where(asset => asset.FileName.StartsWith("Firewood", StringComparison.OrdinalIgnoreCase)).ToList();
    Expect(firewoodAssets.Count == 147 && firewoodAssets.All(asset => asset is { Group: "Props", SubGroup: "Heating & Fuel" }
            && AssetSemanticClassifier.Classify(asset) is { Category: "Furnishings & Objects", Type: "Heating & Fuel", Subtype: "Firewood" }),
        "One or more firewood assets remained classified as lighting.");
    var rockySnowTextures = real.Where(asset => asset.FileName.StartsWith("Rocky_Snow", StringComparison.OrdinalIgnoreCase)).ToList();
    Expect(rockySnowTextures.Count == 5 && rockySnowTextures.All(asset =>
            AssetSemanticClassifier.Classify(asset) is { Category: "Nature", Type: "Terrain", Subtype: "Ground Surfaces" }),
        "One or more Rocky Snow texture swatches remained classified as rocks and boulders.");
    var arcticBloodAssets = real.Where(asset => asset.FileName.StartsWith("Arctic_Blood", StringComparison.OrdinalIgnoreCase)).ToList();
    Expect(arcticBloodAssets.Count == 19 && arcticBloodAssets.All(asset => asset is { Group: "Props", SubGroup: "Combat" }
            && AssetSemanticClassifier.Classify(asset) is { Category: "Combat & Hazards", Type: "Remains & Gore", Subtype: "Blood" }),
        "One or more Arctic blood assets remained classified as ice, snow, or shoreline material.");
    var snowEdgeAssets = real.Where(asset => asset.FileName.StartsWith("Arctic_Snow_Edge", StringComparison.OrdinalIgnoreCase)).ToList();
    Expect(snowEdgeAssets.Count == 46 && snowEdgeAssets.All(asset =>
            AssetSemanticClassifier.Classify(asset) is { Category: "Nature", Type: "Terrain", Subtype: "Snow Cover & Edges" }),
        "One or more Arctic snow-edge assets remained classified as a shoreline.");
    var snowPathAssets = real.Where(asset => Slash(asset.RelativePath).Contains("/Decor/Snow/", StringComparison.OrdinalIgnoreCase)
        && asset.FileName.Contains("Snow", StringComparison.OrdinalIgnoreCase)
        && new[] { "Path", "Ridge", "Trail", "Dusting", "Wheel_Track" }.Any(token => asset.FileName.Contains(token, StringComparison.OrdinalIgnoreCase))).ToList();
    Expect(snowPathAssets.Count >= 14 && snowPathAssets.All(asset =>
            AssetSemanticClassifier.Classify(asset) is { Category: "Nature", Type: "Terrain", Subtype: "Snow Paths & Ribbons" }),
        "One or more Arctic snow path/ribbon assets remained classified as a shoreline.");
    var arcticPlannerDetails = BuildModeService.MatchThemeDetails(real,
        new WallSetSelection { Theme = "Arctic" }, LibrarySourceIds.ForgottenAdventures);
    var expectedArcticDetails = real.Where(asset => asset.Theme.Equals("Arctic", StringComparison.OrdinalIgnoreCase)
        && (asset.FileName.StartsWith("Arctic_Snow_Edge", StringComparison.OrdinalIgnoreCase)
            || asset.FileName.Contains("Snow_Pile", StringComparison.OrdinalIgnoreCase)
            || asset.FileName.Contains("Snow_Patchy_Overlay", StringComparison.OrdinalIgnoreCase)
            || asset.FileName.Contains("Frost_Overlay", StringComparison.OrdinalIgnoreCase)
            || Slash(asset.RelativePath).Contains("/Textures/Overlays/", StringComparison.OrdinalIgnoreCase)
                && new[] { "Frost_", "Snow_", "Ice_" }.Any(prefix => asset.FileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))))
        .ToList();
    Expect(arcticPlannerDetails.Count > 100
        && expectedArcticDetails.All(expected => arcticPlannerDetails.Any(actual => actual.StableIdentity == expected.StableIdentity))
        && arcticPlannerDetails.All(asset => AssetSemanticClassifier.Classify(asset).Subtype is "Snow Cover & Edges" or "Frost & Snow Overlays"),
        "Selecting the Arctic Planner theme did not expose the complete snow-cover and frost/snow-overlay detailing palette.");
    var catalogStructures = real.Where(asset =>
    {
        var path = Slash(asset.RelativePath);
        return path.Contains("_Settlement/Structures/", StringComparison.OrdinalIgnoreCase)
               || path.Contains("!Core_Settlements/Structures/", StringComparison.OrdinalIgnoreCase)
               || path.Contains("Horror/!Wilderness/Walls/", StringComparison.OrdinalIgnoreCase);
    }).ToList();
    var catalogTextures = real.Where(asset => Slash(asset.RelativePath).Contains("/Textures/", StringComparison.OrdinalIgnoreCase)).ToList();
    var unsortedStructures = catalogStructures.Where(asset => AssetSemanticClassifier.Classify(asset).Category == "Unsorted").ToList();
    var unsortedTextures = catalogTextures.Where(asset => AssetSemanticClassifier.Classify(asset).Category == "Unsorted").ToList();
    Expect(catalogStructures.Count > 54_000, $"Requested structure catalog scope was unexpectedly small ({catalogStructures.Count:N0}).");
    Expect(catalogTextures.Count > 2_700, $"Requested texture catalog scope was unexpectedly small ({catalogTextures.Count:N0}).");
    Expect(unsortedStructures.Count == 0,
        $"{unsortedStructures.Count:N0} requested structure assets remained semantically unsorted; first: {unsortedStructures.FirstOrDefault()?.RelativePath}");
    Expect(unsortedTextures.Count == 0,
        $"{unsortedTextures.Count:N0} requested texture assets remained semantically unsorted; first: {unsortedTextures.FirstOrDefault()?.RelativePath}");
    Expect(real.Any(asset => asset.Group == "Building" && asset.SubGroup == "Walls" && asset.Material == "Brick_Plaster"), "Real scan missed compound Brick_Plaster walls.");
    Expect(real.Any(asset => asset.Family == "Door Frames") && real.Any(asset => asset.Family == "Window Sills"), "Real scan missed semantic opening families.");
    Expect(real.Where(asset => asset.Variant != "Unspecified").Any(), "Real scan did not populate Variant.");
    var mining = real.Where(asset => asset.Group == "Transport" && asset.SubGroup == "Mining").ToList();
    Expect(mining.Count >= 700, $"Real scan found unexpectedly few Transport > Mining assets ({mining.Count:N0}).");
    Expect(new[] { "Mine Tracks", "Mine Carts", "Drill Carts", "Mining Fill" }.All(family => mining.Any(asset => asset.Family == family)), "Real scan missed one or more requested mining families.");
    Expect(mining.Count(asset => asset.Family == "Mine Tracks" && asset.FileName.StartsWith("Track_Solo_", StringComparison.OrdinalIgnoreCase) && asset.Material == "Metal") == 50, "Real scan did not classify all 50 Track_Solo assets as Material = Metal.");
    Expect(!real.Any(asset => asset.SubGroup == "Land" && new[] { "Mine Tracks", "Mine Carts", "Drill Carts", "Mining Fill" }.Contains(asset.Family)), "Mining transport assets remained under Land.");
    var floorTextureFolders = new[] { "Marble", "Brick", "Grates", "Hay", "Rug_and_Carpets", "Stone_Diagonal_Tiles", "Stone_Floors", "Stone_Hexagonal_Tiles", "Stone_Patterned_Tiles", "Stone_Square_Tiles", "Wood", "Wooden_Floors" };
    var textureRoot = Path.Combine(realRoot, "!Core_Settlements", "Textures");
    var floorTextureFiles = floorTextureFolders.SelectMany(folder => Directory.EnumerateFiles(Path.Combine(textureRoot, folder), "*", SearchOption.TopDirectoryOnly))
        .Where(path => new[] { ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)).Select(Path.GetFullPath).ToList();
    var realByPath = real.ToDictionary(asset => Path.GetFullPath(asset.FilePath), StringComparer.OrdinalIgnoreCase);
    Expect(floorTextureFiles.Count == 784, $"Expected 784 requested core floor-texture files, found {floorTextureFiles.Count:N0}.");
    foreach (var path in floorTextureFiles)
    {
        Expect(realByPath.TryGetValue(path, out var asset), $"Requested floor texture was absent from the index: {path}");
        if (asset is null) continue;
        Expect(asset.Group == "Building" && asset.SubGroup == "Floors", $"Requested floor texture escaped Building > Floors: {asset.RelativePath} -> {asset.Group}/{asset.SubGroup}.");
        Expect(asset.Family != "Unspecified" && asset.Material != "Unspecified" && asset.Variant != "Unspecified",
            $"Confident floor metadata remained unspecified: {asset.RelativePath} -> {asset.Family}/{asset.Material}/{asset.Variant}.");
    }
    var floorSurfaces = realTypeIndex.Filter(new AssetFilter(Category: "Construction", Type: "Floors", Subtype: "Surfaces"));
    Expect(floorSurfaces.Count >= floorTextureFiles.Count && floorSurfaces.All(asset => Slash(asset.RelativePath).Contains("/Textures/", StringComparison.OrdinalIgnoreCase)),
        "Floor Surfaces included a placeable structure/detail asset outside a texture folder.");
    var floorPlaque = real.Single(asset => asset.FileName.Equals("Dwarven_Floor_Metal_Black_Plaque_A1_2x4.png", StringComparison.OrdinalIgnoreCase));
    Expect(AssetSemanticClassifier.Classify(floorPlaque) is { Type: "Floors", Subtype: "Inlays & Plaques" }
        && !floorSurfaces.Contains(floorPlaque),
        "The Dwarven floor plaque leaked into repeating Floor Surfaces.");
    var caveSurfaces = realTypeIndex.Filter(new AssetFilter(Category: "Nature", Type: "Cave & Underdark", Subtype: "Cave Surfaces"));
    Expect(caveSurfaces.Count > 0 && caveSurfaces.All(asset => Slash(asset.RelativePath).Contains("/Textures/", StringComparison.OrdinalIgnoreCase)),
        "Cave Surfaces did not resolve to actual cave texture-folder assets.");
    var bridgeRoot = Path.Combine(realRoot, "!Core_Settlements", "Structures", "Bridges");
    var bridgeFiles = Directory.EnumerateFiles(bridgeRoot, "*", SearchOption.AllDirectories)
        .Where(path => new[] { ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)).Select(Path.GetFullPath).ToList();
    var expectedBridgeFamilies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["Drawbridges"] = 24, ["Log Bridges"] = 50, ["Plank Bridges"] = 20, ["Rope Bridges"] = 3, ["Train Bridges"] = 70
    };
    Expect(bridgeFiles.Count == 167, $"Expected 167 bridge files, found {bridgeFiles.Count:N0}.");
    var indexedBridges = new List<AssetRecord>();
    foreach (var path in bridgeFiles)
    {
        Expect(realByPath.TryGetValue(path, out var asset), $"Bridge asset was absent from the index: {path}");
        if (asset is null) continue;
        indexedBridges.Add(asset);
        Expect(asset.Group == "Building" && asset.SubGroup == "Bridges", $"Bridge escaped Building > Bridges: {asset.RelativePath} -> {asset.Group}/{asset.SubGroup}.");
        Expect(asset.Family != "Unspecified" && asset.Material != "Unspecified" && asset.Variant != "Unspecified",
            $"Confident bridge metadata remained unspecified: {asset.RelativePath} -> {asset.Family}/{asset.Material}/{asset.Variant}.");
        Expect(asset.Family == "Rope Bridges" || asset.Style != "Unspecified", $"Non-rope bridge style remained unspecified: {asset.RelativePath}.");
    }
    foreach (var expected in expectedBridgeFamilies)
        Expect(indexedBridges.Count(asset => asset.Family.Equals(expected.Key, StringComparison.OrdinalIgnoreCase)) == expected.Value,
            $"{expected.Key} count did not match its {expected.Value:N0}-file source folder.");
    var realWallCatalog = WallConstructionCatalog.Build(real);
    var wallPathAnchors = real.Where(asset => asset.Group == "Building" && asset.SubGroup == "Walls"
        && !AssetSemanticClassifier.IsWallDetail(asset)
        && (asset.PartType == "Path" || asset.FileName.EndsWith("_Path.png", StringComparison.OrdinalIgnoreCase))).ToList();
    var orphanedWallAnchors = wallPathAnchors.Where(anchor => !realWallCatalog.Sets.Any(set =>
        set.WallPieces.Any(member => member.StableIdentity.Equals(anchor.StableIdentity, StringComparison.OrdinalIgnoreCase)))).ToList();
    Expect(orphanedWallAnchors.Count == 0,
        $"{orphanedWallAnchors.Count:N0} wall path anchors did not own a construction set; first: {orphanedWallAnchors.FirstOrDefault()?.RelativePath}");
    Expect(!realWallCatalog.Sets.Any(set => set.AutomaticRibbon.Status == WallRibbonResolutionStatus.Ambiguous),
        "One or more path-anchored wall sets still resolve to multiple ribbons.");
    var adobeSets = realWallCatalog.Sets.Where(set => set.WallSystem == "Adobe").ToList();
    Expect(adobeSets.All(set => !set.Variant.Any(char.IsDigit)),
        "Adobe piece-orientation numbers leaked into construction-set variants instead of remaining inside A/B/C sets.");
    var realMudbrickSets = adobeSets.Where(set => set.PrimaryAppearance.StartsWith("Mudbrick ", StringComparison.OrdinalIgnoreCase)).ToList();
    Expect(realMudbrickSets.Count > 0
        && realMudbrickSets.All(set => set.PrimaryAppearance is "Mudbrick Light" or "Mudbrick Red")
        && realMudbrickSets.All(set => set.Profile is "Thin" or "Wide")
        && realMudbrickSets.All(set => set.Variant == "A"),
        "Real Mudbrick wall sets did not use the Adobe / Mudbrick color / Thin-or-Wide model.");
    var adobeWallAssets = real.Where(asset => asset.Group == "Building" && asset.SubGroup == "Walls"
        && !AssetSemanticClassifier.IsWallDetail(asset)
        && AssetSemanticClassifier.Classify(asset).Materials.Contains("Adobe")).ToList();
    var adobeAppearances = adobeWallAssets.SelectMany(asset => AssetSemanticClassifier.Classify(asset).Appearances)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    Expect(!new[] { "Ashen", "Dark", "Walnut" }.Any(adobeAppearances.Contains),
        "Embedded wood finishes still polluted real single-material Adobe wall Appearance choices.");
    Expect(adobeSets.Where(set => set.Profile != "Wide").All(set => !set.WallPieces.Any(asset => asset.FileName.Contains("_Shelf_", StringComparison.OrdinalIgnoreCase))),
        "Wide-only Adobe shelves leaked into a Thin, Angled, Half Wall, or Crenellations set.");
    var realBrickWoodSets = realWallCatalog.Filter(new WallSetSelection
    {
        WallSystem = "Brick Wood", PrimaryAppearance = "Earthy", SecondaryAppearance = "Ashen", Variant = "A"
    }, LibrarySourceIds.ForgottenAdventures);
    Expect(realBrickWoodSets.Count == 1, $"Expected one Earthy/Ashen Brick Wood A set, found {realBrickWoodSets.Count:N0}.");
    var realBuildAdditions = BuildModeService.Populate(new AssetCatalogIndex(real), realWallCatalog,
        new BuildRecipe
        {
            WallSet = new WallSetSelection { SetId = realBrickWoodSets.Single().Id },
            Trim = new BuildSelection { Group = "Building", SubGroup = "Openings", Material = "Wood", Style = "Ashen" }
        }, new Dictionary<string, RibbonMapping>(), LibrarySourceIds.ForgottenAdventures).WallComponents;
    Expect(realBuildAdditions.Any(asset => asset.FileName.StartsWith("Arrowslit_", StringComparison.OrdinalIgnoreCase))
        && realBuildAdditions.Any(asset => asset.FileName.StartsWith("Arched_Wall_", StringComparison.OrdinalIgnoreCase))
        && !realBuildAdditions.Any(asset => asset.FileName.Contains("_Dark_", StringComparison.OrdinalIgnoreCase)
            || asset.FileName.Contains("_Slate_", StringComparison.OrdinalIgnoreCase)),
        "Real Earthy/Ashen wall additions missed arrow slits/arched walls or admitted incompatible finishes.");
    Console.WriteLine($"Real FA scan: {real.Count:N0} assets; {real.Count(asset => asset.Group == "Unspecified"):N0} Unspecified Group.");
    Console.WriteLine("Whole-library semantic remainder: " + (semanticUnsorted.Count == 0 ? "none" : string.Join(", ", semanticUnsorted
        .GroupBy(asset => $"{asset.Group} > {asset.SubGroup}")
        .OrderByDescending(group => group.Count()).Select(group => $"{group.Key} {group.Count():N0}"))));
    Console.WriteLine($"Requested catalog coverage: {catalogStructures.Count:N0} structure assets and {catalogTextures.Count:N0} texture assets; "
        + $"{unsortedStructures.Count + unsortedTextures.Count:N0} semantically unsorted.");
    Console.WriteLine("Structure retrieval roles: " + string.Join(", ", catalogStructures
        .GroupBy(asset => AssetSemanticClassifier.Classify(asset).Type)
        .OrderByDescending(group => group.Count()).ThenBy(group => group.Key)
        .Select(group => $"{group.Key} {group.Count():N0}")));
    var otherConstruction = catalogStructures.Where(asset => AssetSemanticClassifier.Classify(asset).Type == "Other Construction").ToList();
    Console.WriteLine("Other-construction source folders: " + string.Join(", ", otherConstruction
        .GroupBy(asset => Slash(Path.GetDirectoryName(asset.RelativePath) ?? string.Empty))
        .OrderByDescending(group => group.Count()).ThenBy(group => group.Key).Take(30)
        .Select(group => $"{group.Key} {group.Count():N0}")));
    Console.WriteLine("Texture retrieval roles: " + string.Join(", ", catalogTextures
        .GroupBy(asset => AssetSemanticClassifier.Classify(asset).Type)
        .OrderByDescending(group => group.Count()).ThenBy(group => group.Key)
        .Select(group => $"{group.Key} {group.Count():N0}")));
    Console.WriteLine($"Wall construction catalog: {realWallCatalog.Sets.Count:N0} sets from {wallPathAnchors.Count:N0} path anchors; "
        + $"{realWallCatalog.Sets.Count(set => set.AutomaticRibbon.Status == WallRibbonResolutionStatus.Resolved):N0} ribbon-resolved, zero ambiguous/orphaned anchors.");
    Console.WriteLine($"Transport > Mining: {mining.Count:N0} assets; " + string.Join(", ", mining.GroupBy(asset => asset.Family).OrderBy(group => group.Key).Select(group => $"{group.Key} {group.Count():N0}")));
    Console.WriteLine($"Core settlement floor textures: {floorTextureFiles.Count:N0} files verified individually under Building > Floors.");
    Console.WriteLine($"Bridges: {bridgeFiles.Count:N0} files verified individually under Building > Bridges; "
        + string.Join(", ", indexedBridges.GroupBy(asset => asset.Family).OrderBy(group => group.Key).Select(group => $"{group.Key} {group.Count():N0}")));
    Console.WriteLine($"Earthy/Ashen Brick Wood A wall additions: {realBuildAdditions.Count:N0}; arrow slits and arched-wall inserts verified.");
}

var stateRootArg = Array.FindIndex(args, value => value.Equals("--state-root", StringComparison.OrdinalIgnoreCase));
if (stateRootArg >= 0 && stateRootArg + 1 < args.Length)
{
    var liveState = await new ApplicationStateStore(args[stateRootArg + 1]).LoadAsync();
    Expect(liveState.SchemaVersion == 6, "Copied current state did not retain schema 6.");
    Expect(liveState.Libraries.Count == 1 && liveState.Libraries[0].ParserProfile == LibraryParserProfiles.Fa, "Copied current state did not retain the FA source profile.");
    Expect(liveState.Assets.Count > 160_000, "Copied current state lost indexed assets.");
    Console.WriteLine($"Live-state copy: {liveState.Assets.Count:N0} assets, {liveState.RibbonMappings.Count:N0} mappings.");
    var startupTimer = System.Diagnostics.Stopwatch.StartNew();
    var liveViewModel = new MainViewModel(new ApplicationStateStore(args[stateRootArg + 1]));
    await liveViewModel.InitializeAsync();
    startupTimer.Stop();
    Expect(liveViewModel.IndexedAssetCount == liveState.Assets.Count, "Main UI model lost indexed assets during candidate startup.");
    Expect(liveViewModel.Sections.SelectMany(section => section.Assets).All(tile => tile.Thumbnail is null),
        "Main UI model eagerly decoded thumbnails before a viewport requested them.");
    liveViewModel.ResetFilters();
    liveViewModel.SearchText = "spider";
    await Task.Delay(2_000);
    var expectedSearchMatches = liveState.Assets.Count(asset =>
        asset.FileName.Contains("spider", StringComparison.OrdinalIgnoreCase));
    var displayedSearchMatches = liveViewModel.Sections.Sum(section => section.Assets.Count);
    Expect(expectedSearchMatches > 240 && displayedSearchMatches == expectedSearchMatches,
        $"Explicit filename search displayed {displayedSearchMatches:N0} of {expectedSearchMatches:N0} matches instead of the complete virtualized result set.");
    var browserSection = liveViewModel.Sections.First(section => section.Assets.Count > 0);
    var expandedBrowserRows = liveViewModel.BrowserRows.Count;
    liveViewModel.ToggleBrowserSection(browserSection.Name);
    Expect(liveViewModel.BrowserRows.Count < expandedBrowserRows
        && liveViewModel.BrowserRows.Any(row => row.IsHeader && row.Name == browserSection.Name && row.IsCollapsed),
        "Collapsing a Browser result header did not remove that section's asset rows.");
    liveViewModel.ToggleBrowserSection(browserSection.Name);
    Expect(liveViewModel.BrowserRows.Count == expandedBrowserRows,
        "Re-expanding a Browser result header did not restore its asset rows.");
    liveViewModel.SearchText = string.Empty;
    liveViewModel.SelectedGroup = "Building";
    liveViewModel.SelectedSubGroup = "Walls";
    Expect(liveViewModel.Materials.Count > 1 && liveViewModel.Styles.Count > 1,
        "Indexed UI facets did not remain usable after real-state startup.");
    liveViewModel.ResetFilters();
    liveViewModel.SelectedCategory = "Construction";
    Expect(liveViewModel.Types.Contains("Walls") && liveViewModel.Types.Contains("Floors")
        && liveViewModel.Types.Contains("Openings") && liveViewModel.Types.Contains("Roofs"),
        "Live Type choices omitted useful virtual construction parents.");
    liveViewModel.SelectedType = "Walls";
    await Task.Delay(3_000);
    Expect(liveViewModel.Sections.Count > 1
        && liveViewModel.Sections.All(section => section.Name.StartsWith("Walls >", StringComparison.OrdinalIgnoreCase)),
        "Selecting the Walls parent did not return wall assets grouped under precise collapsible leaf headers.");
    Expect(liveViewModel.SourceSets.Contains("Wall Adobe Cracks") && liveViewModel.SourceSets.Contains("Wall Adobe Wide A")
        && !liveViewModel.SourceSets.Contains("Adobe Wall"),
        "Live Advanced filters exposed overloaded Family values instead of source sets.");
    liveViewModel.SelectedSourceSet = "Wall Adobe Wide A";
    await Task.Delay(2_000);
    Expect(liveViewModel.Sections.SelectMany(section => section.Assets).Any()
        && liveViewModel.Sections.SelectMany(section => section.Assets).All(tile =>
            AssetSourceMetadata.SourceSet(tile.Asset) == "Wall Adobe Wide A"
            && AssetSemanticClassifier.Classify(tile.Asset).Subtype == "Wall Pieces"),
        "The live Source Folder / Set dropdown leaked Adobe wall cracks into the Wide A construction set.");
    Expect(liveViewModel.FilenameVariants.Contains("A1"),
        "The live Filename Variant dropdown did not cascade from the selected source set.");
    liveViewModel.SelectedFilenameVariant = "A1";
    await Task.Delay(2_000);
    Expect(liveViewModel.Sections.SelectMany(section => section.Assets).All(tile =>
            AssetSourceMetadata.FilenameVariant(tile.Asset) == "A1"),
        "The live Filename Variant dropdown did not enforce its exact value.");
    await liveViewModel.SaveStateAsync();
    var reopenedViewModel = new MainViewModel(new ApplicationStateStore(args[stateRootArg + 1]));
    await reopenedViewModel.InitializeAsync();
    Expect(reopenedViewModel.SelectedSourceSet == "Wall Adobe Wide A"
        && reopenedViewModel.SelectedFilenameVariant == "A1",
        "Advanced Source Folder / Set and Filename Variant selections did not survive reopening.");
    liveViewModel.ResetFilters();
    liveViewModel.SelectedCategory = "Construction";
    liveViewModel.SelectedType = "Walls";
    liveViewModel.SelectedSubtype = "Wall Pieces";
    liveViewModel.SelectedMaterial = "Adobe";
    liveViewModel.SelectedTheme = "Desert";
    Expect(liveViewModel.Styles.Contains("Mudbrick Light") && liveViewModel.Styles.Contains("Mudbrick Red")
        && !new[] { "Ashen", "Dark", "Walnut" }.Any(value => liveViewModel.Styles.Contains(value)),
        "Live Browser Adobe choices omitted Mudbrick colors or still exposed embedded wood finishes.");
    Console.WriteLine($"Main view-model startup: {startupTimer.Elapsed.TotalMilliseconds:N0} ms; visible tile models are thumbnail-lazy.");

    liveViewModel.SetBuildViewportWidth(1_240);
    liveViewModel.ShowBuild();
    await liveViewModel.ResetBuildComponentAsync(BuildComponent.Floor);
    await liveViewModel.ResetBuildComponentAsync(BuildComponent.Trim);
    await liveViewModel.ResetBuildComponentAsync(BuildComponent.Walls);
    Expect(liveViewModel.WallsBuildSelector.CandidateCount > 1
        && liveViewModel.BuildWallSampleAssets.Count == liveViewModel.WallsBuildSelector.CandidateCount
        && liveViewModel.BuildPaletteRows.First().LeftSection == BuildPaletteSection.WallSetSamples
        && liveViewModel.BuildWallSampleAssets.All(tile => tile.Thumbnail is null),
        "Planner did not show one lazy representative card per matching wall set.");
    Expect(liveViewModel.BuildFloorSampleAssets.Count is > 0 and <= 240
        && liveViewModel.BuildTrimSampleAssets.Count is > 0 and <= 240
        && liveViewModel.BuildPaletteRows.Any(row => row.IsHeader && row.RightSection == BuildPaletteSection.FloorSamples)
        && liveViewModel.BuildPaletteRows.Any(row => row.IsHeader && row.RightSection == BuildPaletteSection.TrimSamples),
        "Planner did not show bounded, lazy Floor and Trim window-shopping galleries.");
    var previewHeaders = liveViewModel.BuildPaletteRows.Where(row => row.IsHeader).ToList();
    Expect(previewHeaders.Count == 2
        && previewHeaders[0].LeftSection == BuildPaletteSection.WallSetSamples
        && previewHeaders[0].RightSection == BuildPaletteSection.FloorSamples
        && previewHeaders[1].LeftSection == BuildPaletteSection.Detailing
        && previewHeaders[1].RightSection == BuildPaletteSection.TrimSamples,
        "Planner preview did not retain the Walls/Floor over Detailing/Trim 2x2 layout.");

    liveViewModel.WallsBuildSelector.SelectedWallSystem = "Adobe";
    if (liveViewModel.WallsBuildSelector.PrimaryAppearances.Contains("Red"))
        liveViewModel.WallsBuildSelector.SelectedPrimaryAppearance = "Red";
    Expect(liveViewModel.FloorBuildSelector.SelectedMaterial == "Adobe"
        && liveViewModel.TrimBuildSelector.SelectedMaterial == "Adobe"
        && (!liveViewModel.FloorBuildSelector.Styles.Contains("Red") || liveViewModel.FloorBuildSelector.SelectedStyle == "Red")
        && (!liveViewModel.TrimBuildSelector.Styles.Contains("Red") || liveViewModel.TrimBuildSelector.SelectedStyle == "Red"),
        "A single-material wall choice did not seed reset Floor and Trim selectors with its compatible material/appearance.");
    await liveViewModel.ResetBuildComponentAsync(BuildComponent.Floor);
    await liveViewModel.ResetBuildComponentAsync(BuildComponent.Trim);
    await liveViewModel.ResetBuildComponentAsync(BuildComponent.Walls);

    var firstTrimSample = liveViewModel.BuildTrimSampleAssets.First();
    Expect(liveViewModel.SelectPlannerSample(firstTrimSample)
        && string.IsNullOrEmpty(liveViewModel.TrimBuildSelector.Selection.AssetIdentity)
        && liveViewModel.TrimBuildSelector.SelectedMaterial == firstTrimSample.Asset.Material
        && (firstTrimSample.Asset.Style == "Unspecified"
            || liveViewModel.TrimBuildSelector.SelectedStyle == firstTrimSample.Asset.Style)
        && liveViewModel.BuildTrimSampleAssets.Count > 1,
        "Clicking a live Trim sample did not retain the material/appearance family for window shopping.");
    var firstFloorSample = liveViewModel.BuildFloorSampleAssets.First();
    var floorFilterOptions = liveViewModel.PlannerFilterOptions(firstFloorSample);
    Expect(floorFilterOptions.Count > 0 && liveViewModel.SelectPlannerSample(firstFloorSample)
        && liveViewModel.FloorBuildSelector.Selection.AssetIdentity == firstFloorSample.Asset.StableIdentity
        && liveViewModel.BuildFloorSampleAssets.Count == 1,
        "Clicking a live Floor sample did not select and retain exactly that asset.");
    var broadenFloor = floorFilterOptions.First();
    Expect(liveViewModel.ApplyPlannerFilter(firstFloorSample, broadenFloor.Key)
        && string.IsNullOrEmpty(liveViewModel.FloorBuildSelector.Selection.AssetIdentity)
        && liveViewModel.BuildFloorSampleAssets.Count > 0,
        "Right-click-style Floor facet filtering did not broaden the exact choice to that property.");
    var firstWallSample = liveViewModel.BuildWallSampleAssets.First();
    Expect(liveViewModel.SelectWallSetSample(firstWallSample)
        && liveViewModel.WallsBuildSelector.CandidateCount == 1
        && liveViewModel.BuildWallSampleAssets.Count == 0
        && liveViewModel.BuildWallAssets.Count > 0,
        "Selecting a wall-set sample did not replace the sample gallery with the complete wall kit.");
    await liveViewModel.PopulateBuildAsync(saveState: false);
    var buildAssetCount = liveViewModel.BuildWallAssets.Count + liveViewModel.BuildWallDetailAssets.Count + liveViewModel.BuildFloorAssets.Count
        + liveViewModel.BuildDoorFrameAssets.Count + liveViewModel.BuildWindowSillAssets.Count
        + liveViewModel.BuildOtherOpeningAssets.Count;
    Expect(buildAssetCount > 0 && liveViewModel.BuildPaletteRows.Count < buildAssetCount,
        "Planner did not compose its large palette into virtualizable display rows.");
    Expect(liveViewModel.BuildPaletteRows.FirstOrDefault()?.IsHeader == true,
        "Planner palette did not begin with a collapsible section header.");
    var wallFloorHeader = liveViewModel.BuildPaletteRows.First();
    Expect(wallFloorHeader.LeftHeaderHasBody == (liveViewModel.BuildWallAssets.Count > 0)
        && wallFloorHeader.RightHeaderHasBody == (liveViewModel.BuildFloorAssets.Count > 0),
        "Planner section header/body continuity metadata did not match populated sections.");
    var wallBodyRows = liveViewModel.BuildPaletteRows.Skip(1).TakeWhile(row => !row.IsHeader)
        .Where(row => row.LeftAssets.Count > 0).ToList();
    Expect(wallBodyRows.Count == 0 || wallBodyRows.Count(row => row.IsLeftLastBodyRow) == 1,
        "Planner wall section did not identify exactly one final visual body row.");
    Expect(liveViewModel.BuildWallAssets.Concat(liveViewModel.BuildWallDetailAssets).Concat(liveViewModel.BuildFloorAssets)
        .Concat(liveViewModel.BuildDoorFrameAssets).Concat(liveViewModel.BuildWindowSillAssets)
        .Concat(liveViewModel.BuildOtherOpeningAssets).All(tile => tile.Thumbnail is null),
        "Planner eagerly decoded offscreen thumbnails instead of leaving them viewport-lazy.");
    var expandedRowCount = liveViewModel.BuildPaletteRows.Count;
    if (liveViewModel.BuildWallAssets.Count > 0)
    {
        liveViewModel.ToggleBuildSection(BuildPaletteSection.Walls);
        Expect(liveViewModel.BuildPaletteRows.Where(row => !row.IsHeader).All(row =>
                row.LeftAssets.All(tile => !liveViewModel.BuildWallAssets.Contains(tile))),
            "Collapsing the Planner wall section did not remove its assets from display rows.");
        liveViewModel.ToggleBuildSection(BuildPaletteSection.Walls);
        Expect(liveViewModel.BuildPaletteRows.Count == expandedRowCount
            && liveViewModel.BuildPaletteRows.Any(row => row.LeftAssets.Any(tile => liveViewModel.BuildWallAssets.Contains(tile))),
            "Re-expanding the Planner wall section did not restore its assets to display rows.");
    }
    Console.WriteLine($"Planner virtualization: {buildAssetCount:N0} assets composed into {expandedRowCount:N0} rows with zero eager thumbnails.");
}

var bridgeAuditArg = Array.FindIndex(args, value => value.Equals("--bridge-state-audit", StringComparison.OrdinalIgnoreCase));
if (bridgeAuditArg >= 0 && bridgeAuditArg + 1 < args.Length)
{
    var live = await new ApplicationStateStore(args[bridgeAuditArg + 1]).LoadAsync();
    var bridgePathMarker = $"{Path.DirectorySeparatorChar}Structures{Path.DirectorySeparatorChar}Bridges{Path.DirectorySeparatorChar}";
    var liveBridges = live.Assets.Where(asset => asset.FilePath.Contains(bridgePathMarker, StringComparison.OrdinalIgnoreCase)).ToList();
    Console.WriteLine("Live bridge taxonomy: " + string.Join(", ", liveBridges.GroupBy(asset => $"{asset.Group} > {asset.SubGroup} > {asset.Family}; {asset.Material}; {asset.Style}")
        .OrderBy(group => group.Key).Select(group => $"{group.Key} = {group.Count():N0}")));
}

if (failures.Count > 0) { Console.Error.WriteLine(string.Join(Environment.NewLine, failures)); return 1; }
Console.WriteLine("All Dym&D Asset Companion taxonomy, source, filter, index, and cache smoke tests passed.");
return 0;

void Check(AssetRecord asset, string group, string subgroup, string material, string style, string theme, string family, string variant)
{
    Expect(asset.Group == group, $"{asset.FileName}: Group {asset.Group} != {group}"); Expect(asset.SubGroup == subgroup, $"{asset.FileName}: SubGroup {asset.SubGroup} != {subgroup}");
    Expect(asset.Material == material, $"{asset.FileName}: Material {asset.Material} != {material}"); Expect(asset.Style == style, $"{asset.FileName}: Style {asset.Style} != {style}");
    Expect(asset.Theme == theme, $"{asset.FileName}: Theme {asset.Theme} != {theme}"); Expect(asset.Family == family, $"{asset.FileName}: Family {asset.Family} != {family}");
    Expect(asset.Variant == variant, $"{asset.FileName}: Variant {asset.Variant} != {variant}");
}
void Expect(bool condition, string message) { if (!condition) failures.Add("FAIL: " + message); }
static void CreateTestPng(string path)
{
    var pixels = new byte[] { 255, 0, 0, 255, 0, 255, 0, 128 };
    var bitmap = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 8);
    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var stream = File.Create(path); encoder.Save(stream);
}
