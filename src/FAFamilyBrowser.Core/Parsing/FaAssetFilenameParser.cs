using System.Text.RegularExpressions;
using FAFamilyBrowser.Core.Models;

namespace FAFamilyBrowser.Core.Parsing;

public sealed partial class FaAssetFilenameParser : IAssetFilenameParser
{
    private static readonly HashSet<string> Themes = new(StringComparer.OrdinalIgnoreCase)
    { "Aquatic", "Arctic", "Astral", "Desert", "Feywilds", "Horror", "Industrial", "Jungle", "Mountain", "Swamp", "Underdark", "Volcanic", "Woodlands" };
    private static readonly HashSet<string> DefaultStyles = new(StringComparer.OrdinalIgnoreCase)
    { "Ashen", "Autumn", "Beige", "Black", "Bloody", "Blue", "Brass", "Bronze", "Brown", "Camouflage", "Chalk", "Clear", "Copper", "Creamy", "Dark", "Dry", "Earthy", "Eldritch", "Frosty", "Gold", "Golden", "Gray", "Green", "Ice", "Light", "Moonlit", "Mossy", "Multicolor", "Navy", "Olive", "Orange", "Pale", "Peachy", "Pink", "Polished", "Purple", "Red", "Redrock", "Rusty", "Sandstone", "Silver", "Slate", "Snowy", "Soot", "Stitched", "Striped", "Tan", "Teal", "Volcanic", "Walnut", "White", "Yellow" };
    private static readonly HashSet<string> KnownMaterials = new(StringComparer.OrdinalIgnoreCase)
    { "Adobe", "Bone", "Brick", "Canvas", "Ceramic", "Clay", "Cloth", "Concrete", "Crystal", "Dirt", "Fabric", "Flesh", "Fur", "Glass", "Leather", "Marble", "Metal", "Organic", "Paper", "Plaster", "Porcelain", "Rock", "Rope", "Sand", "Stone", "Terracotta", "Thatch", "Tile", "Wax", "Wicker", "Wood" };
    private static readonly HashSet<string> CoreFloorTextureFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Marble", "Brick", "Grates", "Hay", "Rug_and_Carpets", "Stone_Diagonal_Tiles", "Stone_Floors",
        "Stone_Hexagonal_Tiles", "Stone_Patterned_Tiles", "Stone_Square_Tiles", "Wood", "Wooden_Floors"
    };
    private static readonly HashSet<string> SettlementFloorTextureFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Adobe", "Botanical_Garden", "Brick", "Ceramic_Diagonal_Tiles", "Ceramic_Tiles", "Grates", "Hay",
        "Marble", "Metal", "Metal_And_Stone", "Mosaics", "Rug_and_Carpets", "Rugs_And_Carpets",
        "Shack_Floors", "Stone_Diagonal_Tiles", "Stone_Floors", "Stone_Hexagonal_Tiles",
        "Stone_Patterned_Tiles", "Stone_Square_Tiles", "Stone_Tiles", "Wood", "Wooden_Floors"
    };
    private static readonly (string Label, string[] Tokens)[] PartRules =
    {
        ("T Joint", new[] { "Joint", "T" }), ("Cross Joint", new[] { "Joint", "Cross" }),
        ("Diagonal", new[] { "Connector", "DIAG" }), ("Corner", new[] { "Corner" }),
        ("Curve", new[] { "Curve" }), ("Straight", new[] { "Straight" }),
        ("Ends", new[] { "Broken", "Ending" }), ("Ends", new[] { "Ending" }), ("Ends", new[] { "End" }),
        ("Connector", new[] { "Connector" }), ("Joint", new[] { "Joint" }), ("Path", new[] { "Path" })
    };

    public AssetRecord Parse(string sourceRoot, string filePath, IReadOnlySet<string>? styleVocabulary = null)
    {
        var fileName = Path.GetFileName(filePath);
        var tokens = Path.GetFileNameWithoutExtension(fileName).Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var relativePath = Path.GetRelativePath(sourceRoot, filePath);
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var folders = parts.Take(Math.Max(0, parts.Length - 1)).ToArray();
        var taxonomy = ClassifyPath(folders, tokens);
        var styles = new HashSet<string>(DefaultStyles, StringComparer.OrdinalIgnoreCase);
        if (styleVocabulary is not null) styles.UnionWith(styleVocabulary.Where(style => DefaultStyles.Contains(CanonicalStyle(style))));
        var parsed = ParseTokens(tokens, taxonomy.SubGroup, styles);
        var food = taxonomy.SubGroup is "Prepared Meals" or "Ingredients & Groceries";
        var material = parsed.Material;
        var style = parsed.Style;
        var family = taxonomy.Family ?? "Unspecified";
        var variant = parsed.Family;
        if (taxonomy.SubGroup == "Walls" && family == "Unspecified"
            && TryGetMixedWallSecondaryFinish(tokens, parsed.Material, styles, out var secondaryFinish))
            family = secondaryFinish;
        if (TryGetCoreFloorTextureFolder(folders, out var floorTextureFolder))
        {
            material = CoreFloorTextureMaterial(floorTextureFolder, parsed.Material);
            style = CoreFloorTextureStyle(floorTextureFolder, tokens, parsed.Style);
            variant = CoreFloorTextureVariant(floorTextureFolder, tokens, parsed.Family);
        }
        if (taxonomy.SubGroup == "Bridges")
        {
            material = BridgeMaterial(taxonomy.Family, tokens);
            style = BridgeStyle(tokens, styles);
        }
        if (IsMosaicAsset(folders, tokens))
        {
            material = "Mosaic_Tile";
            style = MosaicStyle(tokens, styles, style);
        }
        else if (tokens.Contains("Terracotta", StringComparer.OrdinalIgnoreCase))
        {
            material = "Terracotta";
        }
        if (taxonomy.SubGroup is "Walls" or "Roofs"
            && tokens.Contains("Adobe", StringComparer.OrdinalIgnoreCase))
        {
            material = "Adobe";
        }
        if (taxonomy.SubGroup == "Walls")
            (material, style, family, variant) = WallSetMetadata(folders, tokens, material, style, family, variant);
        if (TryGetSettlementTextureMetadata(folders, tokens, taxonomy, material, style, family, variant, out var texture))
            (material, style, family, variant) = texture;
        if (taxonomy.Family is "Hedge Maze" or "Topiary")
        {
            material = "Organic";
            style = tokens.Select(CanonicalStyle).FirstOrDefault(styles.Contains) ?? style;
        }
        if (ContainsSequence(tokens, ["Adobe", "Render", "Float"]))
        {
            material = "Wood";
        }
        if (taxonomy.Family == "Mine Tracks")
        {
            if (tokens.Length > 1 && tokens[0].Equals("Track", StringComparison.OrdinalIgnoreCase)
                && tokens[1].Equals("Solo", StringComparison.OrdinalIgnoreCase)) material = "Metal";
            else if (tokens.Length > 1 && new[] { "Ashen", "Dark", "Light", "Red", "Walnut", "Wood" }
                .Contains(tokens[1], StringComparer.OrdinalIgnoreCase)) material = "Wood";
            if (style == "Unspecified" && tokens.Length > 1 && styles.Contains(CanonicalStyle(tokens[1])))
                style = CanonicalStyle(tokens[1]);
        }
        return new AssetRecord
        {
            SourceRoot = sourceRoot, FilePath = filePath, FileName = fileName, RelativePath = relativePath,
            RawTokens = string.Join("|", tokens), Group = taxonomy.Group, SubGroup = taxonomy.SubGroup, Theme = taxonomy.Theme,
            Material = food ? "N/A" : material, Style = style,
            Family = family, Variant = variant,
            PartType = parsed.PartType, PartVariant = parsed.PartVariant, EncodedSize = parsed.EncodedSize,
            ParseConfidence = taxonomy.Group == "Unspecified" ? .45 : .85
        };
    }

    private static bool TryGetMixedWallSecondaryFinish(
        string[] tokens,
        string material,
        IReadOnlySet<string> styles,
        out string secondaryFinish)
    {
        secondaryFinish = string.Empty;
        if (material is not ("Brick_Wood" or "Stone_Wood" or "Stone_Metal")) return false;

        var wallIndex = Array.FindIndex(tokens, token => token.Equals("Wall", StringComparison.OrdinalIgnoreCase));
        var variantIndex = Enumerable.Range(Math.Max(0, wallIndex + 1), Math.Max(0, tokens.Length - wallIndex - 1))
            .FirstOrDefault(index => FamilyToken().IsMatch(tokens[index]), tokens.Length);
        var finishes = tokens
            .Skip(Math.Max(0, wallIndex + 1))
            .Take(Math.Max(0, variantIndex - Math.Max(0, wallIndex + 1)))
            .Select(CanonicalStyle)
            .Where(styles.Contains)
            .ToArray();
        if (finishes.Length < 2) return false;
        secondaryFinish = finishes[1];
        return true;
    }

    private static (string Material, string Style, string Family, string Variant) WallSetMetadata(
        string[] folders, string[] tokens, string material, string style, string family, string variant)
    {
        var name = string.Join('_', tokens);
        var setFolder = folders.Select(Clean).LastOrDefault(folder =>
            folder.Contains("Wall", StringComparison.OrdinalIgnoreCase)
            || folder.StartsWith("Curb_", StringComparison.OrdinalIgnoreCase));

        var ceremorph = Regex.Match(name,
            "^Ceremorph_Wall_(?<color>Blue|Gray|Green|Purple)_.*?_(?<geometry>[A-G])[0-9](?:_|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (ceremorph.Success)
            return ("Organic", CanonicalStyle(ceremorph.Groups["color"].Value), "Ceremorph Wall",
                ceremorph.Groups["geometry"].Value.ToUpperInvariant());

        var stoneMetal = Regex.Match(name,
            "^Wall_StoneMetal_(?<finish>Slate|Earthy|Sandstone|Redrock|Volcanic)_(?<metal>Gray|Rusty)_(?<geometry>A[12])(?:_|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (stoneMetal.Success)
            return ("Stone_Metal", CanonicalStyle(stoneMetal.Groups["finish"].Value),
                CanonicalStyle(stoneMetal.Groups["metal"].Value), stoneMetal.Groups["geometry"].Value.ToUpperInvariant());

        var curb = Regex.Match(name,
            "^Curb_Stone_(?<finish>Slate|Earthy|Sandstone|Redrock|Volcanic)_(?<geometry>[AB])(?:_|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (curb.Success)
            return ("Stone", CanonicalStyle(curb.Groups["finish"].Value), "Curbs",
                curb.Groups["geometry"].Value.ToUpperInvariant());

        var adobeColor = Regex.Match(name, "^(?:Half_)?Wall_(?:Crenellations_)?Adobe_(?<color>Red|Sandstone|White)_",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (adobeColor.Success)
        {
            var shape = setFolder?.Contains("Crenellations", StringComparison.OrdinalIgnoreCase) == true ? "Crenellations"
                : setFolder?.Contains("Half_Wall", StringComparison.OrdinalIgnoreCase) == true ? "Half Wall — Thin"
                : setFolder?.Contains("Sloped", StringComparison.OrdinalIgnoreCase) == true ? "Angled"
                : setFolder?.Contains("Thin", StringComparison.OrdinalIgnoreCase) == true ? "Thin"
                : setFolder?.Contains("Wide", StringComparison.OrdinalIgnoreCase) == true ? "Wide"
                : "Adobe Wall";
            var geometry = GeometryFromSetFolder(setFolder) ?? GeometryToken(tokens, "A");
            return ("Adobe", CanonicalStyle(adobeColor.Groups["color"].Value), shape, geometry);
        }

        var mudbrick = Regex.Match(name, "^Wall_Mudbrick_(?<color>Light|Red)_(?<shape>Thin|Wide)_",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (mudbrick.Success)
            return ("Adobe", $"Mudbrick {CanonicalStyle(mudbrick.Groups["color"].Value)}",
                CanonicalStyle(mudbrick.Groups["shape"].Value), "A");

        var mosaic = Regex.Match(name, "^Mosaic_Wall_(?<geometry>[AB])_(?<color>Blue|Sandstone|Slate|Terracotta|White)_",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (mosaic.Success)
            return ("Mosaic_Tile", CanonicalStyle(mosaic.Groups["color"].Value), "Mosaic Wall",
                mosaic.Groups["geometry"].Value.ToUpperInvariant());

        var tile = Regex.Match(name,
            "^Wall_Tile_(?<geometry>[ABC])_(?<wallstyle>Pattern[A-D]|Sandstone|Slate|Terracotta|White)_",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (tile.Success)
            return ("Tile", PreservePatternStyle(tile.Groups["wallstyle"].Value), "Tile Wall",
                tile.Groups["geometry"].Value.ToUpperInvariant());

        var metal = Regex.Match(name,
            "^Wall_Metal_(?<color>Brass|Gray|Polished|Rusty|Soot)_(?<geometry>D1|D2|E1|E2|A|B|C|F)(?:_|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (metal.Success)
            return ("Metal", CanonicalStyle(metal.Groups["color"].Value), "Metal Wall",
                metal.Groups["geometry"].Value.ToUpperInvariant());

        var dwarven = Regex.Match(name,
            "^Dwarven_Wall_Stone_(?<finish>Earthy|Redrock|Sandstone|Slate|Volcanic)(?:_Metal_(?<metal>Black|Brass|Gray|Green|Polished|Rusty))?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (dwarven.Success)
        {
            var geometry = GeometryFromSetFolder(setFolder) ?? GeometryToken(tokens, variant);
            var ruined = tokens.Contains("Ruined", StringComparer.OrdinalIgnoreCase) ? " — Ruined" : string.Empty;
            var metalAccent = dwarven.Groups["metal"].Success
                ? $" — {CanonicalStyle(dwarven.Groups["metal"].Value)} Metal"
                : string.Empty;
            return (dwarven.Groups["metal"].Success ? "Stone_Metal" : "Stone",
                CanonicalStyle(dwarven.Groups["finish"].Value), $"Dwarven Wall{metalAccent}{ruined}", geometry);
        }

        var shack = Regex.Match(name,
            "^Shack_Wall_Wood_A_(?<color>Frosty|Ashen|Dark|Light|Red|Walnut)_",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (shack.Success)
        {
            var geometry = Regex.Match(name, "_(?:Corner_|Diagonal_|End_)?(?<geometry>[ABC])[0-9](?:_|$)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (geometry.Success)
                return ("Wood", CanonicalStyle(shack.Groups["color"].Value), "Shack Wall",
                    geometry.Groups["geometry"].Value.ToUpperInvariant());
        }

        var drow = Regex.Match(name,
            "^Drow_Wall_(?<overlay>Overlay_)?(?:Stone_(?<finish>Earthy|Redrock|Sandstone|Slate|Volcanic))?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (drow.Success)
        {
            var geometry = GeometryFromSetFolder(setFolder) ?? GeometryToken(tokens, variant);
            var overlay = drow.Groups["overlay"].Success;
            var drowStyle = overlay
                ? tokens.FirstOrDefault(token => new[] { "Black", "Brass", "Gray", "Green", "Polished", "Rusty" }
                    .Contains(token, StringComparer.OrdinalIgnoreCase)) ?? style
                : drow.Groups["finish"].Success ? CanonicalStyle(drow.Groups["finish"].Value) : style;
            return (overlay ? "Metal" : tokens.Contains("Metal", StringComparer.OrdinalIgnoreCase) ? "Stone_Metal" : "Stone",
                CanonicalStyle(drowStyle), overlay ? "Drow Metal Overlay" : "Drow Wall", geometry);
        }

        var flesh = Regex.Match(name,
            "^Flesh_(?<color>Black|Bloody|Pale|Red)?_?(?<brain>Brain_)?Wall_.*?_(?<geometry>[A-M][0-9])(?:_|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (flesh.Success)
            return ("Flesh", flesh.Groups["color"].Success ? CanonicalStyle(flesh.Groups["color"].Value) : style,
                flesh.Groups["brain"].Success ? "Brain Wall" : "Flesh Wall",
                flesh.Groups["geometry"].Value.ToUpperInvariant());

        return (material, style, family, variant);
    }

    private static string? GeometryFromSetFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return null;
        var match = Regex.Match(folder, "_(?<geometry>[A-Z])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["geometry"].Value.ToUpperInvariant() : null;
    }

    private static string GeometryToken(IEnumerable<string> tokens, string fallback) => tokens
        .Select(token => Regex.Match(token, "^(?<geometry>[A-Z])(?:[0-9]+)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        .LastOrDefault(match => match.Success)?.Groups["geometry"].Value.ToUpperInvariant() ?? fallback;

    private static string PreservePatternStyle(string value) => value.StartsWith("Pattern", StringComparison.OrdinalIgnoreCase)
        ? $"Pattern{char.ToUpperInvariant(value[^1])}"
        : CanonicalStyle(value);

    public static IReadOnlySet<string> DiscoverStyleVocabulary(IEnumerable<string> filePaths)
    {
        var styles = new HashSet<string>(DefaultStyles, StringComparer.OrdinalIgnoreCase);
        // The FA corpus contains many descriptors immediately before variant
        // codes (Broken, Rectangle, Path, etc.). Those are not authoritative
        // styles, so discovery may only confirm the curated style vocabulary.
        foreach (var path in filePaths)
            foreach (var token in Path.GetFileNameWithoutExtension(path).Split('_', StringSplitOptions.RemoveEmptyEntries))
                if (DefaultStyles.Contains(CanonicalStyle(token))) styles.Add(CanonicalStyle(token));
        return styles;
    }

    private static (string Group, string SubGroup, string Theme, string? Family) ClassifyPath(string[] folders, string[] tokens)
    {
        var theme = folders.Select(Clean).FirstOrDefault(Themes.Contains) ?? "Unspecified";
        var building = Has(folders, "Structures", "Building");
        var mining = Has(folders, "Workplace_Equipment", "Mining");
        if (Has(folders, "Wilderness", "Decor", "Crystals"))
        {
            var family = HasAny(folders, "Stone") ? "Crystal Rock"
                : ContainsSequence(tokens, ["Crystal", "Rock"]) ? "Crystal Rocks"
                : "Crystals";
            return ("Nature", "Cave & Underdark", theme, family);
        }
        if (tokens.Contains("Cave", StringComparer.OrdinalIgnoreCase)
            && HasAny(folders, "Textures", "Elevation"))
        {
            return ("Nature", "Cave & Underdark", theme, "Cave Terrain");
        }
        if (mining && (HasAny(folders, "Drillcarts", "Drill_Carts") || ContainsSequence(tokens, ["Dwarven", "Drillcart"])))
            return ("Transport", "Mining", theme, "Drill Carts");
        if (mining && (HasAny(folders, "Mine_Cart_Fills") || ContainsSequence(tokens, ["Minecart", "Fill"]) || ContainsSequence(tokens, ["Coal", "Spill"])))
            return ("Transport", "Mining", theme, "Mining Fill");
        if (mining && HasAny(folders, "Mine_Tracks")) return ("Transport", "Mining", theme, "Mine Tracks");
        if (mining && HasAny(folders, "Mine_Carts", "Minecarts")) return ("Transport", "Mining", theme, "Mine Carts");
        if (HasAny(folders, "Vehicles"))
        {
            if (HasAny(folders, "Sails", "Rigging", "Sails_&_Rigging", "Hulls", "Anchors", "Masts", "Yards", "Ship_Railings")) return ("Transport", "Marine", theme, "Sails & Rigging");
            if (HasAny(folders, "Docks", "Piers", "Moorings")) return ("Transport", "Marine", theme, "Marine Infrastructure");
            if (HasAny(folders, "Canoes")) return ("Transport", "Marine", theme, "Canoes");
            if (HasAny(folders, "Boats")) return ("Transport", "Marine", theme, "Boats");
            if (HasAny(folders, "Ships")) return ("Transport", "Marine", theme, "Ships");
            if (HasAny(folders, "Carts", "Wagons", "Carts_&_Wagons", "Carts_and_Wagons")) return ("Transport", "Land", theme, "Carts & Wagons");
            if (HasAny(folders, "Sleds", "Sleighs", "Sleds_&_Sleighs")) return ("Transport", "Land", theme, "Sleds & Sleighs");
            return ("Transport", "Transport Support", theme, null);
        }
        if (HasAny(folders, "Docks", "Piers", "Moorings") && HasAny(folders, "Aquatic", "Marine")) return ("Transport", "Marine", theme, "Marine Infrastructure");
        if (HasAny(folders, "Decor") && HasAny(folders, "Snow")
            && tokens.Contains("Blood", StringComparer.OrdinalIgnoreCase))
            return ("Props", "Combat", theme, "Gore");
        if (HasAny(folders, "Gore")) return ("Props", "Combat", theme, "Gore");
        if (HasAny(folders, "Combat")) return ("Props", "Combat", theme, null);
        if (HasAny(folders, "Firewood")) return ("Props", "Heating & Fuel", theme, "Firewood");
        if (HasAny(folders, "Lightsources", "Light_Sources")) return ("Props", "Lighting", theme, null);
        if (HasAny(folders, "Statues")) return ("Props", "Decor", theme, "Statues");
        if (HasAny(folders, "Natural_Decor")) return ("Nature", "Flora", theme,
            HasAny(folders, "Crops", "_Old_Crops", "Herbs") ? "Plants" : HasAny(folders, "Sticks", "Branches") ? "Sticks & Branches" : null);
        if (Has(folders, "Workplace_Equipment", "Farming") && HasAny(folders, "Crops", "Herbs")) return ("Nature", "Flora", theme, "Plants");
        if (HasAny(folders, "Burial_and_Graves", "Graves", "Sarcophagi")) return ("Props", "Decor", theme, "Burial & Graves");
        if (HasAny(folders, "Paths", "Roads_and_Paths", "Roads_&_Paths") && !HasAny(folders, "Structures")) return ("Nature", "Terrain", theme, "Paths");
        if (HasAny(folders, "Elevation")) return ("Nature", "Terrain", theme, ElevationFamily(folders, tokens));
        if (HasAny(folders, "Pillars") && !building) return ("Building", "Pillars & Supports", theme, null);
        if (HasAny(folders, "Walls") && HasAny(folders, "!Wilderness", "Wilderness")) return ("Building", "Walls", theme, null);
        if (Has(folders, "Structures", "Hedgemaze")) return ("Building", "Railings & Fences", theme,
            HasAny(folders, "Topiary") ? "Topiary" : "Hedge Maze");
        if (Has(folders, "Structures", "Aesthetics") && HasAny(folders, "Floor_Breaks", "Floor_Inlays", "Floors"))
            return ("Building", "Floors", theme, HasAny(folders, "Floor_Breaks") ? "Floor Breaks" : HasAny(folders, "Floor_Inlays") ? "Floor Inlays" : null);
        if (Has(folders, "Structures", "Aesthetics") || Has(folders, "Structures", "Tentacles")) return ("Props", "Decor", theme, null);
        if (Has(folders, "Structures", "Platforms")) return ("Building", "Floors", theme, "Platforms");
        if (Has(folders, "Structures", "Bridges")) return ("Building", "Bridges", theme, BridgeFamily(folders));
        if (Has(folders, "Structures", "Brine_Pools")) return ("Building", "Water Features", theme, "Pools");
        if (HasAny(folders, "Giant_Orbs", "Energy_Artifacts")) return ("Props", "Decor", theme,
            HasAny(folders, "Giant_Orbs") ? "Giant Orbs" : "Energy Artifacts");
        if (TryClassifyTexturePath(folders, tokens, theme, out var textureTaxonomy)) return textureTaxonomy;
        if (TryGetCoreFloorTextureFolder(folders, out var floorTextureFolder))
            return ("Building", "Floors", theme, CoreFloorTextureFamily(floorTextureFolder, tokens));
        if (HasAny(folders, "Textures") && HasAny(folders, "Stone_Floors", "Stone_Tiles", "Floor_Tiles", "Shack_Floors", "Rugs_And_Carpets")) return ("Building", "Floors", theme, null);
        if (HasAny(folders, "Textures")) return ("Nature", "Terrain", theme, null);
        if (HasAny(folders, "Decor") && HasAny(folders, "Rocks", "Crystals", "Stalagmites", "Tracks", "Burrows")) return ("Nature", "Terrain", theme, null);
        if (HasAny(folders, "Decor")
            && (HasAny(folders, "Ice_Floes", "Cracked_Ice_Chunks")
                || HasAny(folders, "Snow") && tokens.Any(token => token.Equals("Ice", StringComparison.OrdinalIgnoreCase)
                    || token.Equals("Floe", StringComparison.OrdinalIgnoreCase))))
            return ("Nature", "Water", theme, null);
        if (HasAny(folders, "Decor") && HasAny(folders, "Snow")) return ("Nature", "Terrain", theme, "Snow Cover");
        if (Has(folders, "Structures", "Building", "Walls_and_Curbs")
            && tokens.Contains("Railing", StringComparer.OrdinalIgnoreCase))
            return ("Building", "Railings & Fences", theme, null);
        if (Has(folders, "Structures", "Building", "Walls_and_Curbs")
            && HasAny(folders, "Wall_Tile_Rubble", "Wall_Mosaic_Rubble"))
            return ("Building", "Ruins & Rubble", theme, null);
        if (Has(folders, "Structures", "Building", "Walls_and_Curbs")) return ("Building", "Walls", theme, null);
        if (building && HasAny(folders, "Floors", "Floor_Breaks", "Floor_Plates")) return ("Building", "Floors", theme,
            HasAny(folders, "Floor_Breaks") ? "Floor Breaks" : HasAny(folders, "Floor_Plates") ? "Floor Plates" : null);
        if (building && HasAny(folders, "Doors", "Windows", "Arches")) return ("Building", "Openings", theme,
            HasAny(folders, "Door_Frames") ? "Door Frames" : HasAny(folders, "Window_Sills") ? "Window Sills" : HasAny(folders, "Arches") ? "Arches" : HasAny(folders, "Windows") ? "Windows" : "Doors");
        if (building && HasAny(folders, "Roofs")) return ("Building", "Roofs", theme, null);
        if (building && HasAny(folders, "Pods", "Dome", "Igloo")) return ("Building", "Shelters", theme,
            LastKnown(folders, "Pods", "Dome", "Igloo"));
        if (building && HasAny(folders, "Corrugated_Panels")) return ("Building", "Roofs", theme, "Corrugated Panels");
        if (building && HasAny(folders, "Stairs_and_Ladders", "Stairs", "Ladders")) return ("Building", "Stairs", theme, null);
        if (building && HasAny(folders, "Pillars", "Supports")) return ("Building", "Pillars & Supports", theme, null);
        if (building && HasAny(folders, "Railings_and_Fences", "Railings", "Fences")) return ("Building", "Railings & Fences", theme, null);
        if (building && HasAny(folders, "Palisades")) return ("Building", "Defenses & Barricades", theme, "Palisades");
        if (building && HasAny(folders, "Grates")) return ("Building", "Floors", theme, "Floor Grates");
        if (building && HasAny(folders, "Planks", "Wooden_Walkway_Tiles", "Cells", "Wires")) return ("Building", "Infrastructure", theme, null);
        if (building && HasAny(folders, "Fireplaces")) return ("Building", "Fireplaces", theme, null);
        if (Has(folders, "Structures", "Beams_and_Supports")) return ("Building", "Pillars & Supports", theme, null);
        if (Has(folders, "Structures", "Shelters")) return ("Building", "Shelters", theme, null);
        if (Has(folders, "Structures", "Rubble") || HasAny(folders, "Ruins")) return ("Building", "Ruins & Rubble", theme, null);
        if (Has(folders, "Structures", "Water_Structures")) return ("Building", "Water Features", theme, null);
        if (Has(folders, "Structures", "Mechanical_Parts")) return ("Building", "Infrastructure", theme, null);
        if (HasAny(folders, "Structures") && HasAny(folders, "Arches")) return ("Building", "Openings", theme, "Arches");
        if (Has(folders, "Structures", "Building", "Furniture")) return ("Props", "Furniture", theme, LastKnown(folders, "Seating", "Tables", "Bedding"));
        if (HasAny(folders, "Furniture")) return ("Props", "Furniture", theme, LastKnown(folders, "Seating", "Tables", "Bedding"));
        if (HasAny(folders, "Decor", "Decoration", "Decorations")) return ("Props", "Decor", theme, null);
        if (HasAny(folders, "Workplace_Equipment", "Workplace")) return ("Props", "Workplace", theme, null);
        if (Has(folders, "Clutter", "Food", "Prepared_Meals")) return ("Props", "Prepared Meals", theme, null);
        if (Has(folders, "Clutter", "Food")) return ("Props", "Ingredients & Groceries", theme, null);
        if (Has(folders, "Clutter", "Paper_Goods", "Books_and_Tomes") || Has(folders, "Clutter", "Books_and_Tomes"))
            return ("Props", "Books & Tomes", theme, null);
        if (Has(folders, "Clutter", "Paper_Goods") || Has(folders, "Clutter", "Writing_Implements") || Has(folders, "Clutter", "Pens"))
            return ("Props", "Paper & Writing", theme, null);
        if (Has(folders, "Clutter", "Kitchenware") || Has(folders, "Clutter", "Glassware"))
            return ("Props", "Kitchen & Tableware", theme, null);
        if (Has(folders, "Clutter", "Treasure")) return ("Props", "Treasure & Valuables", theme, null);
        if (Has(folders, "Clutter", "Clothing")) return ("Props", "Clothing", theme, null);
        if (Has(folders, "Clutter", "Cloth")) return ("Props", "Textiles", theme, null);
        if (Has(folders, "Clutter", "Games_and_Toys")) return ("Props", "Games & Toys", theme, null);
        if (Has(folders, "Clutter", "Adventuring_Gear") || Has(folders, "Clutter", "Canes_and_Umbrellas"))
            return ("Props", "Adventuring Gear", theme, null);
        if (Has(folders, "Clutter", "Vanity") || Has(folders, "Clutter", "Baby_Accessories") || Has(folders, "Clutter", "Ornaments"))
            return ("Props", "Vanity & Personal", theme, null);
        if (Has(folders, "Clutter", "Magic_Items") || Has(folders, "Clutter", "Brain_Jars") || Has(folders, "Clutter", "Potions"))
            return ("Props", "Magic & Alchemy", theme, null);
        if (Has(folders, "Clutter", "Arranged_Clutter")) return ("Props", "Arranged Clutter", theme, null);
        if (Has(folders, "Clutter", "Misc")) return ("Props", "Trade & Misc", theme, null);
        if (HasAny(folders, "Vegetation", "Flora", "Plants", "Trees", "Fungi", "Mushrooms")) return ("Nature", "Flora", theme,
            FloraFamily(folders));
        if (HasAny(folders, "Terrain", "Elevation", "Rocks_&_Stones", "Rocks", "Cliffs")) return ("Nature", "Terrain", theme, null);
        if (HasAny(folders, "Water", "River", "Rivers", "Lake", "Lakes", "Coasts", "Aquatic")) return ("Nature", "Water", theme, null);
        if (HasAny(folders, "Clutter")) return ("Props", "Trade & Misc", theme, null);
        if (HasAny(folders, "Effects", "!Effects")) return ("Effects", "Effects", theme, null);
        if (Has(folders, "Structures", "Building")) return ("Building", "Other", theme, null);
        if (HasAny(folders, "Structures")) return ("Building", "Other", theme, LastKnown(folders,
            "Arches", "Domes", "Columns", "Platforms", "Towers", "Gates", "Canopies"));
        return ("Unspecified", "Unspecified", theme, null);
    }

    private static (string Material, string Style, string Family, string PartType, string PartVariant, string EncodedSize) ParseTokens(string[] tokens, string subGroup, IReadOnlySet<string> styles)
    {
        var size = tokens.LastOrDefault() is { } last && SizeToken().IsMatch(last) ? last : string.Empty;
        var end = size.Length == 0 ? tokens.Length : tokens.Length - 1;
        var marker = FindMarker(tokens, subGroup);
        var start = marker >= 0 ? marker + 1 : 0;
        while (start < end && IsStructuralWord(tokens[start])) start++;
        var styleIndex = -1;
        for (var i = start + 1; i < end; i++) if (styles.Contains(CanonicalStyle(tokens[i]))) { styleIndex = i; break; }
        var familyIndex = Enumerable.Range(start, Math.Max(0, end - start)).FirstOrDefault(index => FamilyToken().IsMatch(tokens[index])
            && (index == 0 || !tokens[index - 1].Equals("Pattern", StringComparison.OrdinalIgnoreCase))
            && (index == 0 || !tokens[index - 1].Equals("Design", StringComparison.OrdinalIgnoreCase)), -1);
        var material = "Unspecified";
        var materialEnd = familyIndex >= 0 ? familyIndex : end;
        var materialTokens = tokens[start..materialEnd].SelectMany(MaterialParts).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (materialTokens.Length > 0) material = string.Join("_", materialTokens);
        var style = styleIndex >= 0 && styleIndex < end ? CanonicalStyle(tokens[styleIndex]) : "Unspecified";
        var family = familyIndex >= 0 ? tokens[familyIndex] : "Unspecified";
        var remainderStart = familyIndex >= 0 ? familyIndex + 1 : (styleIndex >= 0 ? styleIndex + 1 : start);
        var remainder = tokens.Skip(remainderStart).Take(Math.Max(0, end - remainderStart)).ToArray();
        var rule = PartRules.FirstOrDefault(item => ContainsSequence(tokens, item.Tokens));
        return (material, style, family, rule.Label ?? (remainder.FirstOrDefault() ?? "Other"), string.Join(" ", remainder), size);
    }

    private static int FindMarker(string[] tokens, string subGroup)
    {
        var candidates = subGroup switch
        {
            "Walls" => new[] { "Wall", "Curb" }, "Floors" => new[] { "Floor" }, "Openings" => new[] { "Door", "Window", "Arch" },
            "Roofs" => new[] { "Roof" }, "Marine" => new[] { "Canoe", "Boat", "Ship", "Sail", "Anchor", "Mast", "Hull" },
            "Land" => new[] { "Cart", "Wagon", "Sled", "Sleigh" }, "Bridges" => new[] { "Bridge" },
            "Mining" => new[] { "Track", "Minecart", "Drillcart" }, _ => Array.Empty<string>()
        };
        return Array.FindIndex(tokens, token => candidates.Contains(token, StringComparer.OrdinalIgnoreCase));
    }
    private static bool IsStructuralWord(string token) => new[] { "Frame", "Sill", "Break", "Plate" }.Contains(token, StringComparer.OrdinalIgnoreCase);
    private static IEnumerable<string> MaterialParts(string token)
    {
        if (KnownMaterials.Contains(token)) return [TitleMaterial(token)];
        var aliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["StoneMetal"] = ["Stone", "Metal"], ["StoneWood"] = ["Stone", "Wood"], ["WoodStone"] = ["Wood", "Stone"],
            ["PlasterWood"] = ["Plaster", "Wood"], ["BrickPlaster"] = ["Brick", "Plaster"], ["BrickWood"] = ["Brick", "Wood"]
        };
        return aliases.TryGetValue(token, out var parts) ? parts : [];
    }
    private static string TitleMaterial(string value) => char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
    private static string CanonicalStyle(string token)
    {
        var withoutNumericSuffix = Regex.Replace(token, "(?<=[A-Za-z])\\d+$", string.Empty);
        return DefaultStyles.FirstOrDefault(style => style.Equals(withoutNumericSuffix, StringComparison.OrdinalIgnoreCase)) ?? token;
    }
    private static bool TryGetCoreFloorTextureFolder(string[] folders, out string folder)
    {
        folder = string.Empty;
        var cleaned = folders.Select(Clean).ToArray();
        var texturesIndex = Array.FindIndex(cleaned, value => value.Equals("Textures", StringComparison.OrdinalIgnoreCase));
        if (texturesIndex < 1 || !cleaned[texturesIndex - 1].Equals("Core_Settlements", StringComparison.OrdinalIgnoreCase)
            || texturesIndex + 1 >= cleaned.Length || !CoreFloorTextureFolders.Contains(cleaned[texturesIndex + 1])) return false;
        folder = cleaned[texturesIndex + 1];
        return true;
    }
    private static string CoreFloorTextureFamily(string folder, string[] tokens)
    {
        if (folder.Equals("Marble", StringComparison.OrdinalIgnoreCase))
            return ContainsSequence(tokens, ["Marble", "Tiles", "Cracked"]) ? "Cracked Marble Tiles"
                : ContainsSequence(tokens, ["Marble", "Tiles"]) ? "Marble Tiles" : "Marble";
        if (folder.Equals("Brick", StringComparison.OrdinalIgnoreCase)) return "Brick Floors";
        if (folder.Equals("Grates", StringComparison.OrdinalIgnoreCase)) return "Floor Grates";
        if (folder.Equals("Hay", StringComparison.OrdinalIgnoreCase)) return "Hay";
        if (folder.Equals("Rug_and_Carpets", StringComparison.OrdinalIgnoreCase))
        {
            if (tokens.FirstOrDefault()?.Equals("Rug", StringComparison.OrdinalIgnoreCase) == true) return "Rug Overlays";
            if (tokens.ElementAtOrDefault(1)?.Equals("Plain", StringComparison.OrdinalIgnoreCase) == true) return "Plain Carpet";
            var design = Regex.Match(tokens.ElementAtOrDefault(1) ?? string.Empty, "^Design(?<letter>[A-Z])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return design.Success ? $"Carpet Design {design.Groups["letter"].Value.ToUpperInvariant()}" : "Carpet";
        }
        if (folder.Equals("Stone_Diagonal_Tiles", StringComparison.OrdinalIgnoreCase)) return "Diagonal Tiles";
        if (folder.Equals("Stone_Hexagonal_Tiles", StringComparison.OrdinalIgnoreCase)) return "Hexagonal Tiles";
        if (folder.Equals("Stone_Patterned_Tiles", StringComparison.OrdinalIgnoreCase)) return "Patterned Tiles";
        if (folder.Equals("Stone_Square_Tiles", StringComparison.OrdinalIgnoreCase))
            return ContainsSequence(tokens, ["Stone", "Tiles", "Cracked"]) ? "Cracked Stone Tiles"
                : ContainsSequence(tokens, ["Stone", "Square", "Tiles"]) ? "Square Tiles" : "Stone Tiles";
        if (folder.Equals("Wood", StringComparison.OrdinalIgnoreCase)) return "Wood Texture";
        if (folder.Equals("Wooden_Floors", StringComparison.OrdinalIgnoreCase))
            return ContainsSequence(tokens, ["Wood", "Floor", "Scratches", "Overlay"]) ? "Wood Floor Scratch Overlays"
                : ContainsSequence(tokens, ["Wood", "Damage", "Overlay"]) ? "Wood Damage Overlays" : "Wooden Flooring";
        if (folder.Equals("Stone_Floors", StringComparison.OrdinalIgnoreCase))
        {
            if (ContainsSequence(tokens, ["Flat", "Stones", "Overlay"])) return "Flat Stone Overlays";
            if (ContainsSequence(tokens, ["Herringbone", "Overlay"])) return "Herringbone Overlays";
            if (ContainsSequence(tokens, ["Cobblestone", "Square"])) return "Square Cobblestone";
            if (tokens.FirstOrDefault()?.Equals("Cobblestone", StringComparison.OrdinalIgnoreCase) == true) return "Cobblestone";
            if (ContainsSequence(tokens, ["Flat", "Stone", "Floor"])) return "Flat Stone";
            if (tokens.FirstOrDefault()?.Equals("Herringbone", StringComparison.OrdinalIgnoreCase) == true) return "Herringbone";
            if (ContainsSequence(tokens, ["Large", "Flagstone"])) return "Large Flagstone";
            if (ContainsSequence(tokens, ["Rectangular", "Tiles"])) return "Rectangular Tiles";
            if (ContainsSequence(tokens, ["Rock", "Tiles"])) return "Rock Tiles";
            if (ContainsSequence(tokens, ["Smooth", "Stone", "Floor"])) return "Smooth Stone";
            return "Stone Floors";
        }
        return "Floor Textures";
    }
    private static string CoreFloorTextureMaterial(string folder, string parsedMaterial)
    {
        if (folder.Equals("Grates", StringComparison.OrdinalIgnoreCase)) return parsedMaterial;
        if (folder.Equals("Marble", StringComparison.OrdinalIgnoreCase)) return "Marble";
        if (folder.Equals("Brick", StringComparison.OrdinalIgnoreCase)) return "Brick";
        if (folder.Equals("Hay", StringComparison.OrdinalIgnoreCase)) return "Hay";
        if (folder.Equals("Rug_and_Carpets", StringComparison.OrdinalIgnoreCase)) return "Fabric";
        if (folder.StartsWith("Stone_", StringComparison.OrdinalIgnoreCase)) return "Stone";
        if (folder.Equals("Wood", StringComparison.OrdinalIgnoreCase) || folder.Equals("Wooden_Floors", StringComparison.OrdinalIgnoreCase)) return "Wood";
        return parsedMaterial;
    }
    private static string CoreFloorTextureStyle(string folder, string[] tokens, string parsedStyle)
    {
        if (folder.Equals("Brick", StringComparison.OrdinalIgnoreCase) && tokens.Any(token => token.Equals("Dirt", StringComparison.OrdinalIgnoreCase))) return "Dirt";
        if (folder.Equals("Stone_Floors", StringComparison.OrdinalIgnoreCase) && tokens.Any(token => Regex.IsMatch(token, "^Dirt\\d*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))) return "Dirt";
        if (!folder.Equals("Rug_and_Carpets", StringComparison.OrdinalIgnoreCase)) return parsedStyle;
        if (tokens.FirstOrDefault()?.Equals("Rug", StringComparison.OrdinalIgnoreCase) == true)
            return tokens.ElementAtOrDefault(1) is { } rugStyle ? HumanizeCompactStyle(rugStyle) : parsedStyle;
        return tokens.ElementAtOrDefault(2) is { } carpetStyle ? HumanizeCompactStyle(carpetStyle) : parsedStyle;
    }
    private static string CoreFloorTextureVariant(string folder, string[] tokens, string parsedVariant)
    {
        if (!folder.Equals("Wooden_Floors", StringComparison.OrdinalIgnoreCase) || !tokens.Contains("Overlay", StringComparer.OrdinalIgnoreCase)) return parsedVariant;
        return tokens.FirstOrDefault(token => Regex.IsMatch(token, "^(?:AG|H|I)\\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) ?? parsedVariant;
    }
    private static bool TryClassifyTexturePath(string[] folders, string[] tokens, string theme,
        out (string Group, string SubGroup, string Theme, string? Family) taxonomy)
    {
        taxonomy = default;
        if (!HasAny(folders, "Textures")) return false;
        if (TryGetCoreFloorTextureFolder(folders, out var coreFolder))
        {
            taxonomy = ("Building", "Floors", theme, CoreFloorTextureFamily(coreFolder, tokens));
            return true;
        }

        var cleaned = folders.Select(Clean).ToArray();
        var textureIndex = Array.FindLastIndex(cleaned, value => value.Equals("Textures", StringComparison.OrdinalIgnoreCase));
        if (textureIndex < 0) return false;
        var folder = textureIndex + 1 < cleaned.Length ? cleaned[textureIndex + 1] : string.Empty;
        var wilderness = cleaned.Any(value => value.Equals("Wilderness", StringComparison.OrdinalIgnoreCase));

        if (tokens.Contains("Cave", StringComparer.OrdinalIgnoreCase))
        {
            taxonomy = ("Nature", "Cave & Underdark", theme, "Cave Terrain");
            return true;
        }
        if (wilderness)
        {
            if (folder.Equals("Water", StringComparison.OrdinalIgnoreCase)
                || folder.Equals("Acid", StringComparison.OrdinalIgnoreCase)
                || folder.Equals("Lava", StringComparison.OrdinalIgnoreCase))
                taxonomy = ("Nature", "Water", theme, null);
            else if (folder.Equals("Overlays", StringComparison.OrdinalIgnoreCase)
                     || cleaned.Any(value => value.Contains("Overlay", StringComparison.OrdinalIgnoreCase)))
                taxonomy = ("Nature", "Terrain", theme, "Terrain Overlays");
            else taxonomy = ("Nature", "Terrain", theme, TextureFamily(folder, tokens));
            return true;
        }

        if (folder.Equals("Roof", StringComparison.OrdinalIgnoreCase))
            taxonomy = ("Building", "Roofs", theme, "Roof Surfaces");
        else if (folder.Equals("Drapes", StringComparison.OrdinalIgnoreCase))
            taxonomy = ("Props", "Textiles", theme, "Drapes");
        else if (folder.Equals("Pergola", StringComparison.OrdinalIgnoreCase))
            taxonomy = ("Building", "Roofs", theme, "Pergola Lattice");
        else if (folder.Equals("Glass", StringComparison.OrdinalIgnoreCase))
            taxonomy = ("Building", "Textures", theme, "Glass Surfaces");
        else if (folder.Equals("Metal_Overlay_Frames", StringComparison.OrdinalIgnoreCase))
            taxonomy = ("Building", "Floors", theme, "Floor Overlay Frames");
        else if (folder.Equals("Liquids", StringComparison.OrdinalIgnoreCase)
                 || tokens.Contains("Liquid", StringComparer.OrdinalIgnoreCase))
            taxonomy = ("Nature", "Water", theme, "Magical Liquids");
        else if (folder.Equals("Crops", StringComparison.OrdinalIgnoreCase)
                 || folder.Equals("Cultivated_Soil", StringComparison.OrdinalIgnoreCase)
                 || folder.Equals("Ground", StringComparison.OrdinalIgnoreCase))
            taxonomy = ("Nature", "Terrain", theme, TextureFamily(folder, tokens));
        else if (folder.Equals("Rug", StringComparison.OrdinalIgnoreCase))
            taxonomy = ("Props", "Textiles", theme, "Rugs");
        else if (folder.Equals("Misc", StringComparison.OrdinalIgnoreCase))
            taxonomy = ("Props", "Decor", theme, "Texture Overlays");
        else if (SettlementFloorTextureFolders.Contains(folder) || textureIndex == cleaned.Length - 1)
            taxonomy = ("Building", "Floors", theme, TextureFamily(folder, tokens));
        else taxonomy = ("Nature", "Terrain", theme, TextureFamily(folder, tokens));
        return true;
    }

    private static bool TryGetSettlementTextureMetadata(string[] folders, string[] tokens,
        (string Group, string SubGroup, string Theme, string? Family) taxonomy,
        string material, string style, string family, string variant,
        out (string Material, string Style, string Family, string Variant) metadata)
    {
        metadata = default;
        if (!HasAny(folders, "Textures")) return false;
        var cleaned = folders.Select(Clean).ToArray();
        var textureIndex = Array.FindLastIndex(cleaned, value => value.Equals("Textures", StringComparison.OrdinalIgnoreCase));
        var folder = textureIndex >= 0 && textureIndex + 1 < cleaned.Length ? cleaned[textureIndex + 1] : string.Empty;
        family = taxonomy.Family ?? family;

        if (folder.Equals("Botanical_Garden", StringComparison.OrdinalIgnoreCase)) material = "Metal_Glass";
        else if (folder.Equals("Rug", StringComparison.OrdinalIgnoreCase) || folder.Equals("Rug_and_Carpets", StringComparison.OrdinalIgnoreCase)
                 || folder.Equals("Rugs_And_Carpets", StringComparison.OrdinalIgnoreCase) || folder.Equals("Drapes", StringComparison.OrdinalIgnoreCase)) material = "Fabric";
        else if (folder.Equals("Shack_Floors", StringComparison.OrdinalIgnoreCase) || folder.Equals("Wooden_Floors", StringComparison.OrdinalIgnoreCase)) material = "Wood";
        else if (folder.Equals("Adobe", StringComparison.OrdinalIgnoreCase)) material = "Adobe";
        else if (folder.Contains("Ceramic", StringComparison.OrdinalIgnoreCase)) material = "Ceramic";
        else if (folder.Contains("Mosaic", StringComparison.OrdinalIgnoreCase)) material = "Mosaic_Tile";
        else if (folder.Contains("Stone", StringComparison.OrdinalIgnoreCase) && folder.Contains("Metal", StringComparison.OrdinalIgnoreCase)) material = "Stone_Metal";
        else if (folder.Contains("Stone", StringComparison.OrdinalIgnoreCase)) material = "Stone";
        else if (folder.Contains("Metal", StringComparison.OrdinalIgnoreCase)
                 || folder.Equals("Grates", StringComparison.OrdinalIgnoreCase) && !IsSpecified(material)) material = "Metal";
        else if (tokens.FirstOrDefault()?.Equals("Ceremorph", StringComparison.OrdinalIgnoreCase) == true) material = "Organic";

        if (!IsSpecified(variant))
            variant = tokens.LastOrDefault(token => FamilyToken().IsMatch(token)) ?? "Texture";
        metadata = (material, style, family, variant);
        return true;
    }

    private static string ElevationFamily(string[] folders, string[] tokens)
    {
        if (HasAny(folders, "Cliff_Top_Paths")) return "Cliff Tops";
        if (HasAny(folders, "Under_Cliff_Paths")) return "Under-Cliff Paths";
        if (HasAny(folders, "Cliff_Paths")) return tokens.Contains("Cave", StringComparer.OrdinalIgnoreCase) ? "Cave Cliffs" : "Cliffs";
        if (HasAny(folders, "Bank_Paths")) return "Banks";
        if (HasAny(folders, "Ridge_and_Slope_Paths")) return "Ridges & Slopes";
        return "Elevation";
    }

    private static string TextureFamily(string folder, string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return tokens.Contains("Overlay", StringComparer.OrdinalIgnoreCase) ? "Surface Overlays" : "Floor Surfaces";
        return HumanizeFolder(folder);
    }

    private static string HumanizeFolder(string value) => value.Replace('_', ' ');
    private static bool IsSpecified(string? value) => !string.IsNullOrWhiteSpace(value)
        && !value.Equals("Unspecified", StringComparison.OrdinalIgnoreCase);
    private static string HumanizeCompactStyle(string value)
    {
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BlackGold"] = "Black & Gold", ["BlueSilver"] = "Blue & Silver", ["CreamTeal"] = "Cream & Teal",
            ["GoldRed"] = "Gold & Red", ["GreenBlack"] = "Green & Black", ["RedGold"] = "Red & Gold",
            ["TealCream"] = "Teal & Cream", ["Brown2"] = "Brown 2"
        };
        return aliases.TryGetValue(value, out var result) ? result : value;
    }
    private static string BridgeFamily(string[] folders)
    {
        if (HasAny(folders, "Draw_Bridges")) return "Drawbridges";
        if (HasAny(folders, "Log_Bridges")) return "Log Bridges";
        if (HasAny(folders, "Plank_Bridges")) return "Plank Bridges";
        if (HasAny(folders, "Rope_Brides", "Rope_Bridges")) return "Rope Bridges";
        if (HasAny(folders, "Train_Bridges")) return "Train Bridges";
        return "Bridges";
    }
    private static string BridgeMaterial(string? family, string[] tokens)
    {
        if (family?.Equals("Rope Bridges", StringComparison.OrdinalIgnoreCase) == true) return "Rope";
        if (tokens.Contains("Metal", StringComparer.OrdinalIgnoreCase)) return "Metal";
        return "Wood";
    }
    private static string BridgeStyle(string[] tokens, IReadOnlySet<string> styles)
    {
        foreach (var token in tokens)
        {
            var canonical = CanonicalStyle(token);
            if (styles.Contains(canonical)) return canonical;
        }
        return "Unspecified";
    }
    private static bool IsMosaicAsset(string[] folders, string[] tokens) =>
        HasAny(folders, "Mosaics")
        || folders.Select(Clean).Any(folder => folder.StartsWith("Wall_Mosaic_", StringComparison.OrdinalIgnoreCase)
            || folder.StartsWith("Mosaic_Floor_Break_", StringComparison.OrdinalIgnoreCase))
        || tokens.FirstOrDefault()?.Equals("Mosaic", StringComparison.OrdinalIgnoreCase) == true;
    private static string MosaicStyle(string[] tokens, IReadOnlySet<string> styles, string parsedStyle)
    {
        if (ContainsSequence(tokens, ["Blue", "White"])) return "Blue_White";
        if (tokens.Contains("Terracotta", StringComparer.OrdinalIgnoreCase)) return "Terracotta";
        if (tokens.Contains("Mixed", StringComparer.OrdinalIgnoreCase)) return "Mixed";
        foreach (var token in tokens)
        {
            var canonical = CanonicalStyle(token);
            if (styles.Contains(canonical)) return canonical;
        }
        return parsedStyle;
    }
    private static bool Has(string[] folders, params string[] sequence) => FindSequence(folders.Select(Clean).ToArray(), sequence) >= 0;
    private static bool HasAny(string[] folders, params string[] values) => folders.Select(Clean).Any(folder => values.Contains(folder, StringComparer.OrdinalIgnoreCase));
    private static string? LastKnown(string[] folders, params string[] values) => folders.Select(Clean).LastOrDefault(folder => values.Contains(folder, StringComparer.OrdinalIgnoreCase))?.Replace('_', ' ');
    private static string? FloraFamily(string[] folders)
    {
        var family = LastKnown(folders, "Trees", "Fungi", "Mushrooms", "Cacti", "Flowers", "Grass", "Leaves", "Plants", "Roots", "Vines", "Moss", "Bushes", "Sticks_Twigs_Branches");
        return family?.Equals("Mushrooms", StringComparison.OrdinalIgnoreCase) == true ? "Fungi" : family;
    }
    private static string Clean(string value) => value.TrimStart('!');
    private static bool ContainsSequence(IReadOnlyList<string> source, IReadOnlyList<string> sequence) => FindSequence(source, sequence) >= 0;
    private static int FindSequence(IReadOnlyList<string> source, IReadOnlyList<string> sequence)
    {
        for (var start = 0; start <= source.Count - sequence.Count; start++) if (Enumerable.Range(0, sequence.Count).All(i => string.Equals(source[start + i], sequence[i], StringComparison.OrdinalIgnoreCase))) return start;
        return -1;
    }
    [GeneratedRegex("^[A-Z](?:\\d+)?$", RegexOptions.CultureInvariant)] private static partial Regex FamilyToken();
    [GeneratedRegex("^\\d+x\\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex SizeToken();
}
