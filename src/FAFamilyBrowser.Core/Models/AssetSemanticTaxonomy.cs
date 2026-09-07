namespace FAFamilyBrowser.Core.Models;

public sealed record AssetSemanticTaxonomy(
    string Category,
    string Type,
    string Subtype,
    IReadOnlyList<string> Materials,
    IReadOnlyList<string> Appearances,
    IReadOnlyList<string> Contexts,
    IReadOnlyList<AssetSemanticPath> Paths)
{
    public static AssetSemanticTaxonomy Unsorted { get; } = new("Unsorted", "Unsorted", string.Empty, [], [], [],
        [new AssetSemanticPath("Unsorted", "Unsorted", string.Empty)]);
}

public sealed record AssetSemanticPath(string Category, string Type, string Subtype)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Subtype)
        ? Type
        : $"{Type} > {Subtype}";
}

public static class AssetTypeHierarchy
{
    private const string Separator = " > ";

    private static readonly IReadOnlyDictionary<string, string[]> CrossListedPaths =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Openings > Arrow Slits"] = ["Walls > Wall Additions > Arrow Slits"],
            ["Openings > Arched Wall Inserts"] = ["Walls > Wall Additions > Arched Wall Inserts"],
            ["Roofs > Corrugated Panels"] = ["Construction Materials > Panels & Sheet Material"]
        };

    public static IReadOnlyList<AssetSemanticPath> Paths(string category, string exactType)
    {
        if (string.IsNullOrWhiteSpace(exactType)) return [];
        var paths = new List<string> { exactType };
        if (CrossListedPaths.TryGetValue(exactType, out var aliases))
            paths.AddRange(aliases);
        return paths.Distinct(StringComparer.OrdinalIgnoreCase).Select(path => ToPath(category, path)).ToList();
    }

    public static string DisplayPath(AssetSemanticTaxonomy taxonomy, string selectedType, string selectedSubtype)
    {
        var path = taxonomy.Paths.FirstOrDefault(candidate =>
            Matches(candidate.Type, selectedType) && Matches(candidate.Subtype, selectedSubtype));
        return (path ?? taxonomy.Paths.First()).DisplayName;
    }

    private static AssetSemanticPath ToPath(string category, string hierarchy)
    {
        var parts = hierarchy.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new AssetSemanticPath(category, parts.FirstOrDefault() ?? "Unsorted",
            parts.Length > 1 ? parts[^1] : string.Empty);
    }

    private static bool Matches(string value, string filter) => string.IsNullOrWhiteSpace(filter)
        || filter.Equals(AssetQueries.All, StringComparison.OrdinalIgnoreCase)
        || value.Equals(filter, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Projects indexed metadata into the retrieval-oriented taxonomy.
/// </summary>
public static class AssetSemanticClassifier
{
    public const string Any = "All";
    private static readonly HashSet<string> AppearanceTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ashen", "Autumn", "Beige", "Black", "Bloody", "Blue", "Brass", "Bronze", "Brown", "Camouflage",
        "Chalk", "Clear", "Copper", "Creamy", "Dark", "Dry", "Earthy", "Eldritch", "Frosty", "Gold",
        "Golden", "Gray", "Green", "Ice", "Light", "Moonlit", "Mossy", "Multicolor", "Navy",
        "Olive", "Orange", "Pale", "Peachy", "Pink", "Polished", "Purple", "Red", "Redrock",
        "Rotten", "Rusty", "Sandstone", "Silver", "Slate", "Snowy", "Soot", "Stitched", "Striped",
        "Tan", "Teal", "Terracotta", "Volcanic", "Walnut", "White", "Yellow"
    };

    public static AssetSemanticTaxonomy Classify(AssetRecord asset)
    {
        var category = CategoryFor(asset);
        var hierarchy = TypeFor(asset);
        var paths = AssetTypeHierarchy.Paths(category, hierarchy);
        if (paths.Count == 0) return AssetSemanticTaxonomy.Unsorted;
        var primary = paths[0];
        return new AssetSemanticTaxonomy(primary.Category, primary.Type, primary.Subtype, MaterialsFor(asset),
            AppearancesFor(asset), ContextsFor(asset, hierarchy), paths);
    }

    public static bool IsWallDetail(AssetRecord asset)
    {
        var path = NormalizedText(asset.RelativePath);
        var name = NormalizedText(asset.FileName);
        if (ContainsAny(path, "wall adobe cracks", "wall mosaic rubble", "wall tile rubble",
                "wall brick c bricks", "structures aesthetics wall decorations",
                "structures aesthetics wall misc")) return true;
        if (ContainsAny(path, "metal overlay")
            && (ContainsAny(path, "walls and curbs", "drow wall") || ContainsAny(name, "drow wall"))) return true;
        if (ContainsAny(name, "wall damage overlay")) return true;
        return asset.Group.Equals("Building", StringComparison.OrdinalIgnoreCase)
            && asset.SubGroup.Equals("Walls", StringComparison.OrdinalIgnoreCase)
            && ContainsAny(name, "cracking", "crack", "damage", "overlay", "broken piece", "loose piece", "rubble", "debris");
    }

    private static IReadOnlyList<string> MaterialsFor(AssetRecord asset)
    {
        if (!Specified(asset.Material) || asset.Material.Equals("N/A", StringComparison.OrdinalIgnoreCase)) return [];
        if (asset.Material.Equals("Mosaic_Tile", StringComparison.OrdinalIgnoreCase)) return ["Mosaic Tile"];
        return asset.Material.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Humanize).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> AppearancesFor(AssetRecord asset)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Specified(asset.Style) && !asset.Style.Equals("N/A", StringComparison.OrdinalIgnoreCase)) values.Add(Humanize(asset.Style));
        if (asset.Group.Equals("Building", StringComparison.OrdinalIgnoreCase)
            && asset.SubGroup.Equals("Walls", StringComparison.OrdinalIgnoreCase)
            && !IsWallDetail(asset))
        {
            if (asset.Material.Contains('_') && AppearanceTokens.Contains(asset.Family)) values.Add(Humanize(asset.Family));
            return values.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        foreach (var token in Path.GetFileNameWithoutExtension(asset.FileName).Split('_', StringSplitOptions.RemoveEmptyEntries))
            if (AppearanceTokens.Contains(token) || token.StartsWith("Pattern", StringComparison.OrdinalIgnoreCase)) values.Add(Humanize(token));
        return values.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string CategoryFor(AssetRecord asset)
    {
        if (IsWallDetail(asset)) return "Construction";
        if (IsSnowBloodAsset(asset) || IsFirewood(asset)) return IsSnowBloodAsset(asset)
            ? "Combat & Hazards"
            : "Furnishings & Objects";
        if (asset.Group.Equals("Props", StringComparison.OrdinalIgnoreCase)
            && asset.SubGroup.Equals("Decor", StringComparison.OrdinalIgnoreCase))
            return DecorTaxonomy(asset, NormalizedText(asset.RelativePath), NormalizedText(asset.FileName)).Category;
        if (asset.Group.Equals("Building", StringComparison.OrdinalIgnoreCase)
            && asset.SubGroup.Equals("Infrastructure", StringComparison.OrdinalIgnoreCase)
            && ContainsAny(NormalizedText(asset.RelativePath), "mechanical parts")
            && !IsMechanicalInfrastructure(NormalizedText(asset.RelativePath), NormalizedText(asset.FileName))) return "Equipment & Work";
        return asset.Group switch
        {
            "Building" => "Construction",
            "Nature" => "Nature",
            "Transport" => "Transport",
            "Effects" => "Effects",
            "Props" when asset.SubGroup is "Prepared Meals" or "Ingredients & Groceries" or "Kitchen & Tableware" => "Food & Dining",
            "Props" when asset.SubGroup is "Workplace" or "Adventuring Gear" or "Trade & Misc" => "Equipment & Work",
            "Props" when asset.SubGroup == "Combat" => "Combat & Hazards",
            "Props" => "Furnishings & Objects",
            _ => "Unsorted"
        };
    }

    private static string TypeFor(AssetRecord asset)
    {
        var path = NormalizedText(asset.RelativePath);
        var name = NormalizedText(asset.FileName);
        if (IsWallDetail(asset)) return WallDetailType(path, name);
        if (IsSnowBloodAsset(asset)) return "Remains & Gore > Blood";
        if (IsFirewood(asset)) return "Heating & Fuel > Firewood";
        if (ContainsAny(path, "corrugated panels") || ContainsAny(name, "corrugated panel"))
            return "Roofs > Corrugated Panels";
        if (asset.Group.Equals("Building", StringComparison.OrdinalIgnoreCase)
            && asset.SubGroup.Equals("Infrastructure", StringComparison.OrdinalIgnoreCase))
        {
            if (ContainsAny(path, "mechanical parts")) return IsMechanicalInfrastructure(path, name)
                ? InfrastructureType(path, name)
                : MechanicalPartType(path, name);
            if (ContainsAny(path, "wooden walkway tiles")) return "Infrastructure > Platforms & Walkways";
            if (ContainsAny(path, " building planks ")) return "Construction Materials > Planks & Lumber";
            if (ContainsAny(path, " building cells ")) return "Floors > Cells & Chambers";
        }
        return (asset.Group, asset.SubGroup) switch
        {
            ("Building", "Walls") => "Walls > Wall Pieces",
            ("Building", "Floors") when asset.Family.Contains("Break", StringComparison.OrdinalIgnoreCase)
                || ContainsAny(path, "floor breaks") || ContainsAny(name, "floor break", "floor damage") => "Floors > Damage & Breaks",
            ("Building", "Floors") when ContainsAny(path, "floor inlays", "plaques")
                || ContainsAny(name, "inlay", "plaque", "medallion") => "Floors > Inlays & Plaques",
            ("Building", "Floors") when ContainsAny(path, "overlay frame", "floor overlays")
                || ContainsAny(name, "overlay", "edge") => "Floors > Overlays & Edges",
            ("Building", "Floors") when !IsTexturePath(path)
                && (ContainsAny(path, "grate", "opening", "trapdoor") || ContainsAny(name, "grate", "opening", "trapdoor")) => "Floors > Grates & Openings",
            ("Building", "Floors") when IsTexturePath(path) => "Floors > Surfaces",
            ("Building", "Floors") => "Floors > Floor Pieces",
            ("Building", "Roofs") when ContainsAny(path, "corrugated panels") || ContainsAny(name, "corrugated panel") => "Roofs > Corrugated Panels",
            ("Building", "Roofs") when ContainsAny(name, "overlay", "damage", "break", "ridge", "cap") => "Roofs > Roof Details",
            ("Building", "Roofs") => "Roofs > Roof Surfaces",
            ("Building", "Openings") => OpeningType(asset, path, name),
            ("Building", "Textures") when asset.Family.Equals("Glass Surfaces", StringComparison.OrdinalIgnoreCase) => "Surface Materials > Glass",
            ("Building", "Textures") => "Surface Materials > Other",
            ("Building", "Stairs") => StairType(path, name),
            ("Building", "Pillars & Supports") when ContainsAny(path, "beam", "support") => "Supports > Beams & Supports",
            ("Building", "Pillars & Supports") when ContainsAny(path, "arch") => "Supports > Arches",
            ("Building", "Pillars & Supports") => "Supports > Pillars & Columns",
            ("Building", "Defenses & Barricades") when ContainsAny(path, "palisade") || ContainsAny(name, "palisade") => "Defenses & Barricades > Palisades",
            ("Building", "Defenses & Barricades") => "Defenses & Barricades > Defensive Structures",
            // Preserve correct retrieval for an existing index made before Palisades received
            // its dedicated parser subgroup; semantic taxonomy does not require reindexing.
            ("Building", "Railings & Fences") when ContainsAny(path, "palisade") || ContainsAny(name, "palisade") => "Defenses & Barricades > Palisades",
            ("Building", "Railings & Fences") when ContainsAny(path, "hedgemaze", "hedge maze") => "Boundaries > Hedges & Mazes",
            ("Building", "Railings & Fences") when ContainsAny(path, "curb") => "Boundaries > Curbs",
            ("Building", "Railings & Fences") when ContainsAny(path, "fence") => "Boundaries > Fences",
            ("Building", "Railings & Fences") => "Boundaries > Railings",
            ("Building", "Bridges") => "Bridges",
            ("Building", "Fireplaces") => "Fireplaces & Hearths",
            ("Building", "Water Features") => WaterFeatureType(path, name),
            ("Building", "Infrastructure") => InfrastructureType(path, name),
            ("Building", "Shelters") => ShelterType(path, name),
            ("Building", "Ruins & Rubble") when ContainsAny(path, "wall mosaic rubble", "wall tile rubble", "wall adobe cracks")
                => "Walls > Wall Details > Loose Pieces & Rubble",
            ("Building", "Ruins & Rubble") => RuinsType(path, name),
            ("Building", _) => "Other Construction",

            ("Nature", "Cave & Underdark") when ContainsAny(path, "crystal") || ContainsAny(name, "crystal") => "Cave & Underdark > Crystals",
            ("Nature", "Cave & Underdark") when ContainsAny(path, "mushroom", "fungi") || ContainsAny(name, "mushroom", "fungi") => "Cave & Underdark > Fungi",
            ("Nature", "Cave & Underdark") when ContainsAny(name, "rock", "stalag", "stalac", "formation") => "Cave & Underdark > Cave Formations",
            ("Nature", "Cave & Underdark") when ContainsAny(path, "path", "ribbon") || ContainsAny(name, "path", "ribbon") => "Cave & Underdark > Cave Paths & Ribbons",
            ("Nature", "Cave & Underdark") when IsTexturePath(path) => "Cave & Underdark > Cave Surfaces",
            ("Nature", "Cave & Underdark") => "Cave & Underdark > Cave Features",
            ("Nature", "Flora") => FloraType(asset, path, name),
            ("Nature", "Terrain") => TerrainType(path, name),
            ("Nature", "Water") => NaturalWaterType(path, name),
            ("Nature", _) => "Other Nature",

            ("Props", "Furniture") => FurnitureType(asset, path),
            ("Props", "Lighting") => "Lighting",
            ("Props", "Heating & Fuel") => "Heating & Fuel > Firewood",
            ("Props", "Decor") => DecorTaxonomy(asset, path, name).Hierarchy,
            ("Props", "Books & Tomes") => "Books & Writing > Books & Tomes",
            ("Props", "Paper & Writing") => "Books & Writing > Paper & Writing Tools",
            ("Props", "Clothing") => "Personal Items > Clothing",
            ("Props", "Vanity & Personal") => "Personal Items > Vanity & Personal",
            ("Props", "Textiles") => "Textiles",
            ("Props", "Games & Toys") => "Games, Toys & Music",
            ("Props", "Treasure & Valuables") => "Treasure & Valuables",
            ("Props", "Magic & Alchemy") => "Magic, Ritual & Alchemy",
            ("Props", "Arranged Clutter") => "Arranged Clutter",
            ("Props", "Ingredients & Groceries") => IngredientType(path, name),
            ("Props", "Prepared Meals") => "Prepared Food",
            ("Props", "Kitchen & Tableware") => KitchenClutterType(path, name),
            ("Props", "Adventuring Gear") => "Adventuring Gear",
            ("Props", "Workplace") => WorkplaceType(path, name),
            ("Props", "Trade & Misc") => "Trade & Market Equipment",
            ("Props", "Combat") => CombatType(asset, path, name),
            ("Props", _) => "Other Objects",

            ("Transport", "Land") => "Land Vehicles",
            ("Transport", "Marine") when asset.Family.Contains("Sail", StringComparison.OrdinalIgnoreCase) || ContainsAny(path, "hull", "sail", "rigging", "ship wall", "ship railing") => "Boats & Ships > Vessel Components",
            ("Transport", "Marine") => "Boats & Ships > Vessels",
            ("Transport", "Mining") => "Rail & Mining Transport",
            ("Transport", _) => "Transport Support",

            ("Effects", _) => EffectType(path, name),
            _ => "Unsorted"
        };
    }

    private static string WallDetailType(string path, string name)
    {
        if (ContainsAny(name, "cracking", "crack", "damage")) return "Walls > Wall Details > Cracks & Damage";
        if (ContainsAny(name, "overlay") || ContainsAny(path, "metal overlay")) return "Walls > Wall Details > Overlays";
        if (ContainsAny(path, "wall decorations", "wall misc") || ContainsAny(name, "wall decoration", "wall head", "feature wall", "dwarven decoration"))
            return "Walls > Wall Details > Decorations";
        if (ContainsAny(path, "rubble", "wall brick c bricks") || ContainsAny(name, "broken piece", "loose piece", "debris"))
            return "Walls > Wall Details > Loose Pieces & Rubble";
        return "Walls > Wall Details > Other";
    }

    private static string OpeningType(AssetRecord asset, string path, string name)
    {
        if (ContainsAny(path, "arrowslit") || ContainsAny(name, "arrowslit")) return "Openings > Arrow Slits";
        if (ContainsAny(name, "arched wall")) return "Openings > Arched Wall Inserts";
        if (asset.Family.Equals("Arches", StringComparison.OrdinalIgnoreCase) || ContainsAny(path, " arches ")) return "Openings > Arches";
        if (asset.Family.Contains("Frame", StringComparison.OrdinalIgnoreCase) || ContainsAny(path, "door frame")) return "Openings > Doors & Frames";
        if (asset.Family.Contains("Sill", StringComparison.OrdinalIgnoreCase) || ContainsAny(path, "window sill")) return "Openings > Windows & Sills";
        if (ContainsAny(path, "shutter") || ContainsAny(name, "shutter")) return "Openings > Shutters";
        if (ContainsAny(path, "gate") || ContainsAny(name, "gate")) return "Openings > Gates";
        if (ContainsAny(path, "hatch") || ContainsAny(name, "hatch")) return "Openings > Hatches";
        if (ContainsAny(path, "window") || ContainsAny(name, "window")) return "Openings > Windows & Sills";
        return "Openings > Doors & Frames";
    }

    private static string WaterFeatureType(string path, string name)
    {
        if (ContainsAny(path, "well") || ContainsAny(name, "well")) return "Water Features > Wells";
        if (ContainsAny(path, "fountain") || ContainsAny(name, "fountain")) return "Water Features > Fountains";
        if (ContainsAny(path, "pool") || ContainsAny(name, "pool")) return "Water Features > Pools";
        if (ContainsAny(path, "birdbath") || ContainsAny(name, "birdbath")) return "Water Features > Birdbaths";
        if (ContainsAny(path, "water wheels") || ContainsAny(name, "water wheel")) return "Water Features > Water Wheels";
        if (ContainsAny(path, "aqueduct grates") || ContainsAny(name, "aqueduct grate")) return "Water Features > Aqueduct Grates";
        if (ContainsAny(path, "aqueduct") || ContainsAny(name, "aqueduct")) return "Water Features > Aqueducts";
        if (ContainsAny(path, "basin fills") || ContainsAny(name, "basin fill")) return "Water Features > Basin Fills";
        if (ContainsAny(path, "basin parts", "basin bases", "basin depths", "basin walls") || ContainsAny(name, "basin")) return "Water Features > Basins";
        if (ContainsAny(path, "drain", "channel") || ContainsAny(name, "drain", "channel")) return "Water Features > Drains & Channels";
        return "Water Features > Other Water Structures";
    }

    private static string StairType(string path, string name)
    {
        if (ContainsAny(path, "stairs overlay") || ContainsAny(name, "stair overlay")) return "Stairs & Access > Stair Overlays";
        if (ContainsAny(path, " paths ") && ContainsAny(name, "stair")) return "Stairs & Access > Stair Paths & Ribbons";
        if (ContainsAny(name, "ramp")) return "Stairs & Access > Ramps";
        if (ContainsAny(name, "ladder")) return "Stairs & Access > Ladders";
        if (ContainsAny(name, "stair", "step")) return "Stairs & Access > Stairs";
        return "Stairs & Access > Access Components";
    }

    private static string ShelterType(string path, string name)
    {
        var text = $"{path} {name}";
        if (ContainsAny(text, "awning")) return "Shelters & Prefab Structures > Awnings";
        if (ContainsAny(text, "lean to")) return "Shelters & Prefab Structures > Lean-tos";
        if (ContainsAny(text, "igloo")) return "Shelters & Prefab Structures > Igloos";
        if (ContainsAny(text, "dome")) return "Shelters & Prefab Structures > Domes";
        if (ContainsAny(text, "pod")) return "Shelters & Prefab Structures > Pods";
        if (ContainsAny(text, "tarp")) return "Shelters & Prefab Structures > Tarps";
        if (ContainsAny(text, "tent")) return "Shelters & Prefab Structures > Tents";
        return "Shelters & Prefab Structures > Other Shelters";
    }

    private static string RuinsType(string path, string name)
    {
        var text = $"{path} {name}";
        if (ContainsAny(text, "wood", "plank", "timber")) return "Ruins & Construction Debris > Timber";
        if (ContainsAny(text, "glass")) return "Ruins & Construction Debris > Glass";
        if (ContainsAny(text, "stone", "brick", "masonry", "marble")) return "Ruins & Construction Debris > Stone & Masonry";
        if (ContainsAny(text, "ceremorph", "tentacle", "organic")) return "Ruins & Construction Debris > Alien & Organic Debris";
        if (ContainsAny(text, "rubble debris")) return "Ruins & Construction Debris > Mixed Debris";
        return "Ruins & Construction Debris > Miscellaneous Debris";
    }

    private static string InfrastructureType(string path, string name) => ContainsAny(path, "road", "paving", "pavement")
        ? "Infrastructure > Roads & Paving"
        : ContainsAny(path, "panel") || ContainsAny(name, "panel") ? "Infrastructure > Panels"
        : ContainsAny(path, "pipe", "wire", "conduit") || ContainsAny(name, "pipe", "wire") ? "Infrastructure > Pipes & Conduits"
        : ContainsAny(path, "grate") || ContainsAny(name, "grate") ? "Infrastructure > Grates"
        : ContainsAny(path, "platform", "walkway") || ContainsAny(name, "platform", "walkway") ? "Infrastructure > Platforms & Walkways"
        : "Infrastructure > Other Infrastructure";

    private static string MechanicalPartType(string path, string name)
    {
        if (ContainsAny(path, "mechanical paths")) return "Mechanical Parts > Paths & Ribbons";
        if (ContainsAny(path, "wind turbines") || ContainsAny(name, "wind turbine")) return "Mechanical Parts > Wind Turbines";
        if (ContainsAny(path, "propellers") || ContainsAny(name, "propeller")) return "Mechanical Parts > Propellers";
        if (ContainsAny(path, "cogs") || ContainsAny(name, "cog", "gear")) return "Mechanical Parts > Cogs & Gears";
        if (ContainsAny(path, "bells") || ContainsAny(name, "bell")) return "Mechanical Parts > Bells";
        if (ContainsAny(path, "nuts and bolts") || ContainsAny(name, "nut", "bolt")) return "Mechanical Parts > Fasteners";
        if (ContainsAny(path, "clock parts") || ContainsAny(name, "clock")) return "Mechanical Parts > Clockwork Parts";
        if (ContainsAny(path, "scrap metal") || ContainsAny(name, "scrap")) return "Mechanical Parts > Scrap Metal";
        if (ContainsAny(path, "springs") || ContainsAny(name, "spring")) return "Mechanical Parts > Springs";
        if (ContainsAny(path, "levers", "cranks") || ContainsAny(name, "lever", "crank")) return "Mechanical Parts > Levers & Cranks";
        if (ContainsAny(path, "winches") || ContainsAny(name, "winch")) return "Mechanical Parts > Winches";
        return "Mechanical Parts > Miscellaneous Parts";
    }

    private static bool IsMechanicalInfrastructure(string path, string name) =>
        ContainsAny(path, "pipes", "wires and cables", "conduit", "grates")
        || ContainsAny(name, "pipe", "wire", "cable", "conduit", "grate");

    private static string FloraType(AssetRecord asset, string path, string name)
    {
        var text = $"{asset.Family} {path} {name}".ToLowerInvariant();
        if (ContainsAny(text, "water plants", "lilypad", "cattail")) return "Flora > Aquatic Plants";
        if (ContainsAny(text, "cactus", "cacti", "saguaro", "prickly pear", "aloe", "ocotillo")) return "Flora > Cacti & Succulents";
        if (ContainsAny(text, "moss", "lichen")) return "Flora > Moss & Lichen";
        if (ContainsAny(text, "leaf", "leaves")) return "Flora > Leaves & Leaf Piles";
        if (ContainsAny(text, "stick", "twig", "branch")) return "Flora > Sticks, Twigs & Branches";
        if (ContainsAny(text, "hay", "straw")) return "Flora > Hay & Straw";
        if (ContainsAny(text, "herb")) return "Flora > Herbs";
        if (ContainsAny(text, "exotic", "flytrap", "pitcher plant", "bioluminescent")) return "Flora > Exotic & Carnivorous Plants";
        if (ContainsAny(text, "tree")) return "Flora > Trees";
        if (ContainsAny(text, "shrub", "bush")) return "Flora > Shrubs";
        if (ContainsAny(text, "flower")) return "Flora > Flowers & Plants";
        if (ContainsAny(text, "grass", "crop")) return "Flora > Grass & Crops";
        if (ContainsAny(text, "vine", "tendril")) return "Flora > Vines & Tendrils";
        if (ContainsAny(text, "root")) return "Flora > Roots";
        if (ContainsAny(text, "dead", "corpse")) return "Flora > Dead Flora";
        if (ContainsAny(text, "mushroom", "fungi")) return "Flora > Fungi";
        return "Flora > General Plants";
    }

    private static string NaturalWaterType(string path, string name)
    {
        var text = $"{path} {name}";
        if (ContainsAny(text, "lava")) return "Water & Liquids > Lava Surfaces";
        if (ContainsAny(text, "acid")) return "Water & Liquids > Acid";
        if (ContainsAny(text, "liquid")) return "Water & Liquids > Magical Liquids";
        if (ContainsAny(name, "snow edge", "snow patchy overlay", "snow pile")) return "Terrain > Snow Cover & Edges";
        if (ContainsAny(path, "snow paths")
            || ContainsAny(name, "snow") && ContainsAny(name, "path", "ridge", "trail", "dusting", "wheel track"))
            return "Terrain > Snow Paths & Ribbons";
        if (ContainsAny(name, "ice floe", "cracked ice")) return "Water & Liquids > Ice & Snow";
        if (ContainsAny(text, "shore", "bank", "edge", "ribbon", "path")) return "Water & Liquids > Shorelines & Ribbons";
        if (IsTexturePath(path)) return "Water & Liquids > Water Surfaces";
        if (ContainsAny(text, "waterfall")) return "Water & Liquids > Waterfalls";
        if (ContainsAny(text, "cascade")) return "Water & Liquids > Cascades";
        if (ContainsAny(text, "foam")) return "Water & Liquids > Foam";
        if (ContainsAny(text, "wave")) return "Water & Liquids > Waves";
        if (ContainsAny(text, "ripple")) return "Water & Liquids > Ripples";
        if (ContainsAny(text, "jet")) return "Water & Liquids > Jets";
        if (ContainsAny(text, "snow", "ice floe", "cracked ice")) return "Water & Liquids > Ice & Snow";
        return "Water & Liquids > Other Water Features";
    }

    private static string TerrainType(string path, string name)
    {
        var text = $"{path} {name}";
        if (ContainsAny(name, "frost overlay", "snow overlay", "ice overlay", "snow patchy overlay", "snow disturbed overlay"))
            return "Terrain > Frost & Snow Overlays";
        if (ContainsAny(text, "overlay")) return "Terrain > Ground Overlays";
        if (IsTexturePath(path)) return "Terrain > Ground Surfaces";
        if (ContainsAny(name, "snow edge", "snow pile", "snow patch")) return "Terrain > Snow Cover & Edges";
        if (ContainsAny(path, "snow paths")
            || ContainsAny(name, "snow") && ContainsAny(name, "path", "ridge", "trail", "dusting", "wheel track"))
            return "Terrain > Snow Paths & Ribbons";
        if (ContainsAny(text, "cliff", "ledge")) return "Terrain > Cliffs & Ledges";
        if (ContainsAny(text, "slope", "bank", "ridge")) return "Terrain > Slopes, Banks & Ridges";
        if (ContainsAny(text, "hill", "mound")) return "Terrain > Hills & Mounds";
        if (ContainsAny(text, "boulder", "rock", "stone")) return "Terrain > Rocks & Boulders";
        if (ContainsAny(text, "burrow")) return "Terrain > Ground Features > Burrows";
        if (ContainsAny(text, "hole", "pit")) return "Terrain > Ground Features > Holes & Pits";
        if (ContainsAny(text, "trench")) return "Terrain > Ground Features > Trenches";
        if (ContainsAny(text, "path", "road")) return "Terrain > Paths & Roads";
        return "Terrain > Terrain Features";
    }

    private static string FurnitureType(AssetRecord asset, string path)
    {
        var text = $"{asset.Family} {path}";
        if (ContainsAny(text, "seat", "chair", "bench", "stool")) return "Furniture > Seating";
        if (ContainsAny(text, "table")) return "Furniture > Tables";
        if (ContainsAny(text, "bed", "bedding")) return "Furniture > Beds";
        if (ContainsAny(text, "desk")) return "Furniture > Desks";
        if (ContainsAny(text, "shelf", "shelv", "bookcase", "bookshel")) return "Furniture > Shelving";
        if (ContainsAny(text, "cabinet", "wardrobe", "cupboard")) return "Furniture > Cabinets";
        if (ContainsAny(text, "chest", "crate", "barrel", "basket", "container")) return "Storage & Containers";
        if (ContainsAny(text, "display case", "display pillow")) return "Furniture > Display Cases";
        if (ContainsAny(text, "mirror")) return "Furniture > Mirrors";
        if (ContainsAny(text, "altar", "shrine")) return "Furniture > Altars & Shrines";
        if (ContainsAny(text, "bathroom", "bathtub", "toilet", "privy", "basin", "sink", "tap")) return "Furniture > Bathroom Fixtures";
        if (ContainsAny(text, "cooking appliance", "stove", "oven")) return "Furniture > Cooking Appliances";
        if (ContainsAny(text, "room divider", "screen")) return "Furniture > Room Dividers";
        if (ContainsAny(text, "nursery", "cot", "baby bath", "stroller", "mobile")) return "Furniture > Nursery Furniture";
        if (ContainsAny(text, "orrery", "armillary")) return "Furniture > Orreries & Astronomical";
        if (ContainsAny(text, "stand", "plinth", "book stand")) return "Furniture > Stands & Plinths";
        if (ContainsAny(text, "adornment")) return "Decor & Display > Furniture Adornments";
        if (ContainsAny(text, "arranged furniture")) return "Arranged Clutter";
        return "Furniture > Miscellaneous Furniture";
    }

    private static (string Category, string Hierarchy) DecorTaxonomy(AssetRecord asset, string path, string name)
    {
        var text = $"{asset.Family} {path} {name}";
        if (ContainsAny(text, " textures misc coins", "coins gold", "coins silver", "coins copper", "coins mixed"))
            return ("Furnishings & Objects", "Treasure & Valuables > Coin Textures");
        if (ContainsAny(text, " bones ", "skeleton", "skull", "ribcage"))
            return ("Combat & Hazards", "Remains & Gore > Bones & Skeletons");
        if (ContainsAny(text, "restraints and torture", "torture", "torment", "barbwire", "meat hook"))
        {
            if (ContainsAny(text, " cage")) return ("Combat & Hazards", "Restraints & Torture > Cages");
            if (ContainsAny(text, "chain", "restraint", "barbwire", "meat hook")) return ("Combat & Hazards", "Restraints & Torture > Chains & Restraints");
            return ("Combat & Hazards", "Restraints & Torture > Devices");
        }
        if (ContainsAny(text, " ore ", "rock ore")) return ("Nature", "Terrain > Ores & Mineral Deposits");
        if (ContainsAny(text, "spider cocoon", "webbing", "insect hive")) return ("Nature", "Wildlife Features > Webs, Cocoons & Hives");
        if (ContainsAny(text, " nest", " eggs ")) return ("Nature", "Wildlife Features > Nests & Eggs");
        if (ContainsAny(text, " sand ", "sand pile", "sand dusting")) return ("Nature", "Terrain > Sand Features");
        if (ContainsAny(text, "frost overlay")) return ("Nature", "Terrain > Frost & Snow Overlays");
        if (ContainsAny(text, "puddle")) return ("Nature", "Water & Liquids > Puddles");
        if (ContainsAny(text, "cairn")) return ("Nature", "Terrain > Cairns");
        if (ContainsAny(text, "teeth", "saliva")) return ("Nature", "Organic Terrain > Teeth & Saliva");
        if (ContainsAny(text, "eyes", "eyeball", "orifice")) return ("Nature", "Organic Terrain > Eyes & Orifices");
        if (ContainsAny(text, "wart", "cyst", "pustule", "growth")) return ("Nature", "Organic Terrain > Growths & Lesions");
        if (ContainsAny(text, "tentacle")) return ("Nature", "Organic Terrain > Tentacles");
        if (ContainsAny(path, "signs and boards") || ContainsAny(text, "sign", "notice", "plaque", "mail and post box"))
            return ("Furnishings & Objects", "Signs & Notices");
        if (ContainsAny(text, "outdoor cooking", "bbq", " spit")) return ("Food & Dining", "Kitchen & Dining Clutter > Outdoor Cooking");
        if (ContainsAny(text, " storage ", "sack", "bucket", "safe", "parcel", "pottery shelving", "spice box", " keg"))
            return ("Furnishings & Objects", "Storage & Containers");
        if (ContainsAny(text, "musical instrument", "percussion", "woodwind", "brass instrument", "string instrument"))
            return ("Furnishings & Objects", "Games, Toys & Music > Musical Instruments");
        if (ContainsAny(text, "rugs and carpets", "woven mat", "door mat")) return ("Furnishings & Objects", "Textiles > Rugs & Carpets");
        if (ContainsAny(text, "mirror")) return ("Furnishings & Objects", "Furniture > Mirrors");
        if (ContainsAny(text, "office")) return ("Equipment & Work", "Office Equipment");
        if (ContainsAny(text, "pet accessories")) return ("Furnishings & Objects", "Pet Accessories");
        if (ContainsAny(text, "clothing rails and hangers", "hanging clothes")) return ("Furnishings & Objects", "Personal Items > Clothing Storage");
        if (ContainsAny(text, "burial and graves", "sarcophag", "tombstone", "coffin", " dirt grave"))
            return ("Furnishings & Objects", "Decor & Display > Burial & Memorials");
        if (ContainsAny(text, "pottery", "amphora", "vase", "urn", " jug", "pitcher", "plant pot"))
            return ("Furnishings & Objects", "Decor & Display > Pottery & Ceramics");
        if (ContainsAny(text, "statue", "bust", "plinth", "dwarven head", "sundial"))
            return ("Furnishings & Objects", "Decor & Display > Statues, Busts & Plinths");
        if (ContainsAny(text, "wall hanging", "banner", "flag", "bunting", "drape", "curtain", "troph"))
            return ("Furnishings & Objects", "Decor & Display > Wall Hangings & Banners");
        if (ContainsAny(text, "painting", "portrait", "cave painting")) return ("Furnishings & Objects", "Decor & Display > Art & Paintings");
        if (ContainsAny(text, "holiday", "christmas", "gift", "stocking", "naughty nice"))
            return ("Furnishings & Objects", "Decor & Display > Holiday Decorations");
        if (ContainsAny(text, "mannequin")) return ("Furnishings & Objects", "Decor & Display > Mannequins");
        if (ContainsAny(text, "arranged decor")) return ("Furnishings & Objects", "Arranged Clutter");
        if (ContainsAny(text, "structures aesthetics", "decoration")) return ("Furnishings & Objects", "Decor & Display > Architectural Decor");
        if (ContainsAny(text, "ceremorph", "astral", "giant orb", "energy artifact", "crystal reader", "mind reader"))
            return ("Furnishings & Objects", "Decor & Display > Alien & Arcane Decor");
        return ("Furnishings & Objects", "Decor & Display > Other Decor");
    }

    private static string IngredientType(string path, string name)
    {
        var text = $"{path} {name}";
        return ContainsAny(text, "drink", "beer", "ale", "wine", "bottle")
            ? "Prepared Food > Drinks"
            : "Ingredients";
    }

    private static string KitchenClutterType(string path, string name)
    {
        var text = $"{path} {name}";
        if (ContainsAny(text, "pot", "pan", "cauldron", "cookware", "cutting board", "chopping board", "prep"))
            return "Kitchen & Dining Clutter > Cookware & Preparation";
        if (ContainsAny(text, "cup", "mug", "glass", "goblet", "tray", "platter", "serving"))
            return "Kitchen & Dining Clutter > Drinkware & Serving";
        return "Kitchen & Dining Clutter > Tableware";
    }

    private static string WorkplaceType(string path, string name)
    {
        var text = $"{path} {name}";
        if (ContainsAny(text, "farm")) return "Tools & Workstations > Farming";
        if (ContainsAny(text, "smith", "forge")) return "Tools & Workstations > Smithing";
        if (ContainsAny(text, "carpent", "woodwork")) return "Tools & Workstations > Carpentry";
        if (ContainsAny(text, "mine", "mining")) return "Tools & Workstations > Mining";
        if (ContainsAny(text, "fish")) return "Tools & Workstations > Fishing";
        if (ContainsAny(text, "alchemy")) return "Tools & Workstations > Alchemy";
        if (ContainsAny(text, "tailor", "sewing", "knitting", "loom", "spinning wheel")) return "Tools & Workstations > Tailoring";
        if (ContainsAny(text, "leatherwork", "tanning", "fur")) return "Tools & Workstations > Leatherworking";
        if (ContainsAny(text, "medical", "syringe", "bandage")) return "Tools & Workstations > Medical";
        if (ContainsAny(text, "pottery", "clay")) return "Tools & Workstations > Pottery";
        if (ContainsAny(text, " art ", "paint", "brushes and pencils")) return "Tools & Workstations > Art";
        if (ContainsAny(text, "cleaning", "laundry", "mop", "ironing")) return "Tools & Workstations > Cleaning & Laundry";
        if (ContainsAny(text, "building tools", "brickmaking", "masonry")) return "Tools & Workstations > Construction";
        if (ContainsAny(text, "wheelbarrow", "crane", "hoist")) return "Tools & Workstations > Material Handling";
        if (ContainsAny(text, "archeology", "archaeology")) return "Tools & Workstations > Archaeology";
        if (ContainsAny(text, "book making", "papyrus making")) return "Tools & Workstations > Book & Papyrus Making";
        if (ContainsAny(text, "dyeing", " dyes ")) return "Tools & Workstations > Dyeing";
        if (ContainsAny(text, "law enforcement", "badge", "fingerprint", "handheld ram")) return "Tools & Workstations > Law Enforcement";
        if (ContainsAny(text, "toolbox")) return "Tools & Workstations > Toolboxes";
        if (ContainsAny(text, "machine", "mechan", "gear", "engine")) return "Machinery & Mechanisms";
        return "Tools & Workstations > General Tools";
    }

    private static string CombatType(AssetRecord asset, string path, string name)
    {
        var text = $"{asset.Family} {path} {name}";
        if (ContainsAny(text, "gore", "corpse", "body", "blood", "bone", "skeleton")) return "Remains & Gore";
        if (ContainsAny(text, "siege", "cannon", "catapult", "ballista", "gatling")) return "Siege Weapons";
        if (ContainsAny(text, "ammo", "ammunition", "arrow", "bolt")) return "Ammunition";
        if (ContainsAny(text, "armor", "armour", "shield")) return "Armor & Shields";
        if (ContainsAny(text, "trap", "hazard")) return "Traps & Hazards";
        if (ContainsAny(text, "barricade", "defense", "defence")) return "Defenses & Barricades";
        return "Weapons";
    }

    private static string EffectType(string path, string name)
    {
        var text = $"{path} {name}";
        if (ContainsAny(text, "fire", "flame", "smoke")) return "Fire & Smoke";
        if (ContainsAny(text, "light", "shadow")) return "Light & Shadow";
        if (ContainsAny(text, "rain", "snow", "weather", "fog")) return "Weather & Atmosphere";
        if (ContainsAny(text, "water", "liquid", "splash")) return "Water & Liquid";
        if (ContainsAny(text, "impact", "damage", "crack")) return "Impact & Damage";
        return "Magic & Other Effects";
    }

    private static IReadOnlyList<string> ContextsFor(AssetRecord asset, string type)
    {
        var contexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var text = NormalizedText($"{asset.RelativePath} {asset.FileName} {type}");

        if (type.StartsWith("Ingredients", StringComparison.OrdinalIgnoreCase)
            || type.StartsWith("Prepared Food", StringComparison.OrdinalIgnoreCase)
            || type.StartsWith("Kitchen & Dining Clutter", StringComparison.OrdinalIgnoreCase)) contexts.Add("Kitchen & Food Preparation");
        if (type.StartsWith("Prepared Food", StringComparison.OrdinalIgnoreCase)
            || type.StartsWith("Kitchen & Dining Clutter", StringComparison.OrdinalIgnoreCase)) contexts.Add("Dining & Food Service");
        if (type.StartsWith("Ingredients", StringComparison.OrdinalIgnoreCase)
            || type.StartsWith("Prepared Food", StringComparison.OrdinalIgnoreCase)
            || type.StartsWith("Kitchen & Dining Clutter", StringComparison.OrdinalIgnoreCase)
            || type is "Storage & Containers" or "Furniture > Tables" or "Furniture > Shelving") contexts.Add("Tavern Kitchen");
        if (ContainsAny(text, "tavern", "inn", "ale", "beer", "keg")) contexts.Add("Tavern & Inn");
        if (ContainsAny(text, "home", "household", "domestic")) contexts.Add("Home");
        if (ContainsAny(text, "castle", "fortress", "guard", "armory")) contexts.Add("Castle & Fortress");
        if (ContainsAny(text, "palace", "manor", "royal", "throne")) contexts.Add("Palace & Manor");
        if (ContainsAny(text, "cave", "underdark")) contexts.Add("Cave & Underdark");
        if (ContainsAny(text, "workshop", "smith", "forge", "carpent")) contexts.Add("Workshop");
        if (ContainsAny(text, "farm", "crop", "agricult")) contexts.Add("Farm");
        if (ContainsAny(text, "mine", "mining")) contexts.Add("Mine");
        if (ContainsAny(text, "dock", "ship", "marine", "boat")) contexts.Add("Dock & Ship");
        if (ContainsAny(text, "camp", "tent")) contexts.Add("Camp");
        if (ContainsAny(text, "market", "shop", "trade")) contexts.Add("Shop & Market");
        if (ContainsAny(text, "temple", "shrine", "ritual")) contexts.Add("Temple & Shrine");

        if (type == "Floors > Surfaces") contexts.Add("Build: Floor Surface");
        if (type.StartsWith("Floors >", StringComparison.OrdinalIgnoreCase) && type != "Floors > Surfaces") contexts.Add("Build: Floor Detail");
        if (type is "Roofs > Roof Surfaces" or "Roofs > Corrugated Panels") contexts.Add("Build: Roof Surface");
        if (type == "Roofs > Roof Details") contexts.Add("Build: Roof Detail");
        if (type.StartsWith("Walls > Wall Pieces", StringComparison.OrdinalIgnoreCase)) contexts.Add("Build: Wall Construction");
        if (type.StartsWith("Walls > Wall Details", StringComparison.OrdinalIgnoreCase)) contexts.Add("Build: Wall Detail");
        if (type.StartsWith("Openings >", StringComparison.OrdinalIgnoreCase)) contexts.Add("Build: Opening / Trim");
        if (type == "Boundaries > Hedges & Mazes") contexts.Add("Build: Hedge Maze");
        if (type.StartsWith("Terrain >", StringComparison.OrdinalIgnoreCase))
            contexts.Add(type.Contains("Cliff", StringComparison.OrdinalIgnoreCase)
                || type.Contains("Slope", StringComparison.OrdinalIgnoreCase)
                || type.Contains("Path", StringComparison.OrdinalIgnoreCase)
                ? "Paint: Terrain Ribbon" : "Paint: Ground Surface");
        if (type is "Terrain > Snow Cover & Edges" or "Terrain > Frost & Snow Overlays")
            contexts.Add("Build: Arctic Detailing");
        if (type == "Cave & Underdark > Cave Surfaces") contexts.Add("Paint: Cave Surface");

        return contexts.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string NormalizedText(string value) => value.Replace('_', ' ').Replace('\\', ' ').Replace('/', ' ').ToLowerInvariant();
    private static bool IsSnowBloodAsset(AssetRecord asset)
    {
        var path = NormalizedText(asset.RelativePath);
        return ContainsAny(path, "decor snow") && ContainsAny(NormalizedText(asset.FileName), "blood");
    }
    private static bool IsFirewood(AssetRecord asset) => ContainsAny(NormalizedText($"{asset.RelativePath} {asset.FileName}"), "firewood");
    private static bool IsTexturePath(string path) => path.Contains(" textures ", StringComparison.OrdinalIgnoreCase);
    private static bool ContainsAny(string value, params string[] terms) => terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    private static bool Specified(string? value) => !string.IsNullOrWhiteSpace(value)
        && !value.Equals("Unspecified", StringComparison.OrdinalIgnoreCase);
    private static string Humanize(string value) => value.Replace('_', ' ');
}
