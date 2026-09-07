using System.Text.RegularExpressions;
using FAFamilyBrowser.Core.Models;

namespace FAFamilyBrowser.Core.Parsing;

public sealed partial class GenericAssetFilenameParser : IAssetFilenameParser
{
    private static readonly HashSet<string> Materials = new(StringComparer.OrdinalIgnoreCase)
    { "brick", "cloth", "concrete", "dirt", "glass", "leather", "metal", "plaster", "rock", "sand", "stone", "thatch", "tile", "wood" };
    private static readonly HashSet<string> Styles = new(StringComparer.OrdinalIgnoreCase)
    { "ashen", "bloody", "dark", "earthy", "fancy", "frosty", "light", "moonlit", "polished", "redrock", "rusty", "sandstone", "slate", "volcanic", "walnut" };
    private static readonly HashSet<string> Themes = new(StringComparer.OrdinalIgnoreCase)
    { "aquatic", "arctic", "astral", "desert", "feywilds", "horror", "industrial", "jungle", "mountain", "swamp", "underdark", "volcanic", "woodlands" };

    public AssetRecord Parse(string sourceRoot, string filePath, IReadOnlySet<string>? styleVocabulary = null)
    {
        var fileName = Path.GetFileName(filePath);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var tokens = stem.Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var relativePath = Path.GetRelativePath(sourceRoot, filePath);
        var folders = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .SkipLast(1).Select(Normalize).ToArray();
        var taxonomy = ClassifyFolders(folders);
        var material = tokens.FirstOrDefault(Materials.Contains) ?? folders.SelectMany(Tokenize).FirstOrDefault(Materials.Contains) ?? "Unspecified";
        var styles = new HashSet<string>(Styles, StringComparer.OrdinalIgnoreCase);
        if (styleVocabulary is not null) styles.UnionWith(styleVocabulary.Where(style => Styles.Contains(style)));
        var style = tokens.FirstOrDefault(styles.Contains) ?? folders.SelectMany(Tokenize).FirstOrDefault(styles.Contains) ?? "Unspecified";
        var theme = folders.SelectMany(Tokenize).FirstOrDefault(Themes.Contains) ?? "Unspecified";
        var variant = tokens.LastOrDefault(token => VariantToken().IsMatch(token)) ?? "Unspecified";
        var size = tokens.LastOrDefault(token => SizeToken().IsMatch(token)) ?? string.Empty;

        return new AssetRecord
        {
            SourceRoot = sourceRoot,
            FilePath = filePath,
            FileName = fileName,
            RelativePath = relativePath,
            RawTokens = string.Join("|", tokens),
            Group = taxonomy.Group,
            SubGroup = taxonomy.SubGroup,
            Family = taxonomy.Family,
            Material = Title(material),
            Style = Title(style),
            Theme = Title(theme),
            Variant = variant,
            EncodedSize = size,
            PartType = "Other",
            ParseConfidence = taxonomy.Group == "Unspecified" ? .25 : .70
        };
    }

    private static (string Group, string SubGroup, string Family) ClassifyFolders(string[] folders)
    {
        if (HasAny(folders, "vehicles", "vehicle", "transport", "boats", "canoes", "ships", "carts", "wagons", "sleds", "sleighs"))
        {
            if (HasAny(folders, "boats", "canoes", "ships", "marine", "nautical", "sails", "rigging"))
                return ("Transport", "Marine", Family(folders, ("canoes", "Canoes"), ("boats", "Boats"), ("ships", "Ships"), ("sails", "Sails & Rigging"), ("rigging", "Sails & Rigging")));
            return ("Transport", "Land", Family(folders, ("carts", "Carts & Wagons"), ("wagons", "Carts & Wagons"), ("sleds", "Sleds & Sleighs"), ("sleighs", "Sleds & Sleighs"), ("mine carts", "Mine Carts")));
        }
        if (HasAny(folders, "walls", "floors", "openings", "doors", "windows", "roofs", "stairs", "ladders", "pillars", "supports", "shelters", "ruins", "rubble", "infrastructure"))
        {
            var subgroup = FirstMatch(folders, ("walls", "Walls"), ("floors", "Floors"), ("doors", "Openings"), ("windows", "Openings"),
                ("openings", "Openings"), ("roofs", "Roofs"), ("stairs", "Stairs"), ("ladders", "Stairs"), ("pillars", "Pillars & Supports"),
                ("supports", "Pillars & Supports"), ("shelters", "Shelters"), ("ruins", "Ruins & Rubble"), ("rubble", "Ruins & Rubble"),
                ("infrastructure", "Infrastructure"));
            var family = Family(folders, ("door frames", "Door Frames"), ("windows", "Windows"), ("floor plates", "Floor Plates"), ("floor breaks", "Floor Breaks"));
            return ("Building", subgroup, family);
        }
        if (HasAny(folders, "furniture", "decor", "workplace", "adventuring gear", "adventure gear", "food", "kitchen", "clothing", "combat", "magic", "alchemy", "books", "writing", "treasure"))
        {
            var subgroup = FirstMatch(folders, ("furniture", "Furniture"), ("decor", "Decor"), ("workplace", "Workplace"),
                ("adventuring gear", "Adventuring Gear"), ("adventure gear", "Adventuring Gear"), ("food", "Food & Kitchen"),
                ("kitchen", "Food & Kitchen"), ("clothing", "Clothing"), ("combat", "Combat"), ("magic", "Magic & Alchemy"),
                ("alchemy", "Magic & Alchemy"), ("books", "Books & Writing"), ("writing", "Books & Writing"), ("treasure", "Treasure & Valuables"));
            var family = Family(folders, ("seating", "Seating"), ("chairs", "Seating"), ("tables", "Tables"), ("bedding", "Bedding"), ("prepared meals", "Prepared Meals"));
            return ("Props", subgroup, family);
        }
        if (HasAny(folders, "flora", "plants", "trees", "fungi")) return ("Nature", "Flora", Family(folders, ("trees", "Trees"), ("fungi", "Fungi")));
        if (HasAny(folders, "terrain", "elevation", "rocks", "cliffs")) return ("Nature", "Terrain", "Unspecified");
        if (HasAny(folders, "water", "rivers", "lakes", "coasts")) return ("Nature", "Water", "Unspecified");
        if (HasAny(folders, "effects", "effect", "fx")) return ("Effects", "Effects", "Unspecified");
        return ("Unspecified", "Unspecified", "Unspecified");
    }

    private static bool HasAny(IEnumerable<string> folders, params string[] values) => folders.Any(folder => values.Contains(folder, StringComparer.OrdinalIgnoreCase));
    private static string FirstMatch(IEnumerable<string> folders, params (string Token, string Label)[] values) =>
        values.FirstOrDefault(value => folders.Contains(value.Token, StringComparer.OrdinalIgnoreCase)).Label ?? "Unspecified";
    private static string Family(IEnumerable<string> folders, params (string Token, string Label)[] values) =>
        values.FirstOrDefault(value => folders.Contains(value.Token, StringComparer.OrdinalIgnoreCase)).Label ?? "Unspecified";
    private static IEnumerable<string> Tokenize(string value) => value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    private static string Normalize(string value) => Regex.Replace(value.Trim().TrimStart('!').Replace('_', ' ').Replace('-', ' '), "\\s+", " ").ToLowerInvariant();
    private static string Title(string value) => value == "Unspecified" ? value : string.Join(' ', value.Split(' ').Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    [GeneratedRegex("^[A-Z](?:\\d+)?$", RegexOptions.CultureInvariant)] private static partial Regex VariantToken();
    [GeneratedRegex("^\\d+x\\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex SizeToken();
}
