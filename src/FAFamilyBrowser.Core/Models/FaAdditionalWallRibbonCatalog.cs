using System.Text.RegularExpressions;

namespace FAFamilyBrowser.Core.Models;

public static class FaAdditionalWallRibbonCatalog
{
    private static readonly IReadOnlyDictionary<string, string> FinishCodes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Slate"] = "01", ["Earthy"] = "02", ["Sandstone"] = "03",
            ["Redrock"] = "04", ["Volcanic"] = "05"
        };

    private static readonly Regex BrickPattern = new(
        "^Wall_Brick_(?<finish>Slate|Earthy|Sandstone|Redrock|Volcanic)_(?<geometry>A|B|C)(?:_|\\.)",
        Options);
    private static readonly Regex BrickWoodPattern = new(
        "^Wall_BrickWood_(?<finish>Slate|Earthy|Sandstone|Redrock|Volcanic)_(?<wood>Ashen|Dark|Light|Red|Walnut)_(?<geometry>A|B|C)(?:_|\\.)",
        Options);
    private static readonly Regex PlasterPattern = new(
        "^Wall_Plaster_White_(?<geometry>A)(?:_|\\.)",
        Options);
    private static readonly Regex StoneMetalPattern = new(
        "^Wall_StoneMetal_(?<finish>Slate|Earthy|Sandstone|Redrock|Volcanic)_(?<metal>Gray|Rusty)_(?<geometry>A1|A2)(?:_|\\.)",
        Options);
    private static readonly Regex CurbPattern = new(
        "^Curb_Stone_(?<finish>Slate|Earthy|Sandstone|Redrock|Volcanic)_(?<geometry>A|B)(?:_|\\.)",
        Options);
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    public static RibbonMapping? TryResolve(IReadOnlyList<AssetRecord> walls, string mappingKey)
    {
        if (walls.Count == 0 || walls.Any(asset => !asset.SourceId.Equals(
                LibrarySourceIds.ForgottenAdventures, StringComparison.OrdinalIgnoreCase))) return null;

        var descriptions = walls.Select(asset => Describe(asset.FileName)).ToList();
        if (descriptions.Any(description => description is null)) return null;
        var resolved = descriptions.Select(description => description!.Value).ToList();
        if (resolved.Select(description => $"{description.ToolGroup}|{description.RibbonName}")
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1) return null;

        var selected = resolved[0];
        return new RibbonMapping
        {
            FamilyKey = mappingKey,
            CspTool = "Buildings",
            CspToolGroup = selected.ToolGroup,
            CspRibbonName = selected.RibbonName,
            SuggestedRibbonName = selected.RibbonName,
            SuggestionConfidence = 1.0,
            IsManuallyConfirmed = false
        };
    }

    public static (string ToolGroup, string RibbonName)? Describe(string fileName)
    {
        var brick = BrickPattern.Match(fileName);
        if (brick.Success && !brick.Groups["geometry"].Value.Equals("C", StringComparison.OrdinalIgnoreCase)
            && FinishCodes.TryGetValue(brick.Groups["finish"].Value, out var brickFinish))
            return ("Brick Walls", $"Wall_Brick_{Upper(brick, "geometry")}_{brickFinish}");

        var brickWood = BrickWoodPattern.Match(fileName);
        if (brickWood.Success && FinishCodes.TryGetValue(brickWood.Groups["finish"].Value, out var brickWoodFinish))
        {
            var wood = Canonical(brickWood.Groups["wood"].Value);
            if (MixedBrushExists(brickWoodFinish, wood))
                return ("Brick Wood Walls", $"Wall_BrickWood_{Upper(brickWood, "geometry")}_{brickWoodFinish}_{wood}");
        }

        var plaster = PlasterPattern.Match(fileName);
        if (plaster.Success) return ("Plaster Walls", "Wall_Plaster_A_01");

        var stoneMetal = StoneMetalPattern.Match(fileName);
        if (stoneMetal.Success && FinishCodes.TryGetValue(stoneMetal.Groups["finish"].Value, out var stoneMetalFinish))
            return ("Stone Metal Walls", $"Wall_StoneMetal_{Upper(stoneMetal, "geometry")}_{stoneMetalFinish}_{Canonical(stoneMetal.Groups["metal"].Value)}");

        var curb = CurbPattern.Match(fileName);
        if (curb.Success && FinishCodes.TryGetValue(curb.Groups["finish"].Value, out var curbFinish))
            return ("Curbs", $"Curb_Stone_{Upper(curb, "geometry")}_{curbFinish}");

        return null;
    }

    private static bool MixedBrushExists(string finish, string accent) => finish switch
    {
        "01" or "02" or "03" => true,
        "04" => accent is "Dark" or "Red",
        "05" => accent is "Red" or "Walnut",
        _ => false
    };

    private static string Upper(Match match, string group) => match.Groups[group].Value.ToUpperInvariant();
    private static string Canonical(string value) => char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
}
