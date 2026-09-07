using System.Text.RegularExpressions;

namespace FAFamilyBrowser.Core.Models;

public static class FaMixedWallRibbonCatalog
{
    private static readonly IReadOnlyDictionary<string, string> FinishCodes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Slate"] = "01", ["Earthy"] = "02", ["Sandstone"] = "03",
            ["Redrock"] = "04", ["Volcanic"] = "05"
        };

    private static readonly IReadOnlyDictionary<string, string> WoodGeometryCodes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "A", ["B"] = "B", ["C"] = "C", ["D"] = "D",
            ["E"] = "E1", ["F"] = "E2", ["G"] = "E3"
        };

    private static readonly Regex StoneWoodPattern = new(
        "^Wall_StoneWood_(?<style>Slate|Earthy|Sandstone|Redrock|Volcanic)_(?<wood>Ashen|Dark|Light|Red|Walnut)_(?<geometry>A|B|C|D)(?:_|\\.)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PlasterWoodPattern = new(
        "^Wall_PlasterWood_(?<wood>Ashen|Dark|Light|Red|Walnut)_(?<geometry>A1|A2|B|C1|C2|D)(?:_|\\.)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex WoodPattern = new(
        "^Wall_Wood_(?<wood>Ashen|Dark|Light|Red|Walnut)_(?<geometry>A|B|C|D|E|F|G)(?:_|\\.)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static RibbonMapping? TryResolve(IReadOnlyList<AssetRecord> walls, string mappingKey)
    {
        if (walls.Count == 0 || walls.Any(asset => !asset.SourceId.Equals(
                LibrarySourceIds.ForgottenAdventures, StringComparison.OrdinalIgnoreCase))) return null;

        var descriptions = walls.Select(Describe).ToList();
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

    private static (string ToolGroup, string RibbonName)? Describe(AssetRecord asset)
    {
        var stoneWood = StoneWoodPattern.Match(asset.FileName);
        if (stoneWood.Success && FinishCodes.TryGetValue(stoneWood.Groups["style"].Value, out var finish))
        {
            var wood = Canonical(stoneWood.Groups["wood"].Value);
            if (StoneWoodBrushExists(finish, wood))
                return ("Stone Wood Walls", $"Wall_StoneWood_{stoneWood.Groups["geometry"].Value.ToUpperInvariant()}_{finish}_{wood}");
        }

        var plasterWood = PlasterWoodPattern.Match(asset.FileName);
        if (plasterWood.Success)
        {
            var wood = Canonical(plasterWood.Groups["wood"].Value);
            return ("Plaster Wood Walls", $"Wall_PlasterWood_{plasterWood.Groups["geometry"].Value.ToUpperInvariant()}_{wood}");
        }

        var woodWall = WoodPattern.Match(asset.FileName);
        if (woodWall.Success && WoodGeometryCodes.TryGetValue(woodWall.Groups["geometry"].Value, out var geometry))
        {
            var wood = Canonical(woodWall.Groups["wood"].Value);
            if ((geometry is "E2" or "E3") && (wood is "Red" or "Walnut")) return null;
            return ("Wood Walls", $"Wall_Wood_{geometry}_{wood}");
        }

        return null;
    }

    private static bool StoneWoodBrushExists(string finish, string wood) => finish switch
    {
        "01" or "02" => true,
        "03" => wood is "Ashen" or "Dark" or "Light",
        "04" => wood is "Dark" or "Red",
        "05" => wood is "Red" or "Walnut",
        _ => false
    };

    private static string Canonical(string value) => char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
}
