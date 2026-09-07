using System.Text.RegularExpressions;

namespace FAFamilyBrowser.Core.Models;

public static class FaStoneWallRibbonCatalog
{
    private static readonly IReadOnlyDictionary<string, string> FinishCodes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Slate"] = "01",
            ["Earthy"] = "02",
            ["Sandstone"] = "03",
            ["Redrock"] = "04",
            ["Volcanic"] = "05"
        };

    private static readonly IReadOnlyDictionary<string, string> GeometryCodes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "A",
            ["B1"] = "B1",
            ["B2"] = "B2",
            ["B3"] = "B3",
            ["C"] = "C",
            ["D"] = "D",
            ["E"] = "E",
            ["F"] = "F",
            ["G"] = "G",
            ["H"] = "H",
            ["I1"] = "I",
            ["I2"] = "J",
            ["I3"] = "J2",
            ["J1"] = "K",
            ["J2"] = "K2",
            ["K1"] = "L",
            ["K2"] = "M"
        };

    private static readonly Regex CoreStoneWallPattern = new(
        "^Wall_Stone_(?<style>Slate|Earthy|Sandstone|Redrock|Volcanic)_(?<geometry>A|B1|B2|B3|C|D|E|F|G|H|I1|I2|I3|J1|J2|K1|K2)(?:_|\\.)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    public static RibbonMapping? TryResolve(IReadOnlyList<AssetRecord> walls, string mappingKey)
    {
        if (walls.Count == 0 || walls.Any(asset => !asset.SourceId.Equals(
                LibrarySourceIds.ForgottenAdventures, StringComparison.OrdinalIgnoreCase))) return null;

        var descriptions = walls.Select(Describe).ToList();
        if (descriptions.Any(description => description is null)) return null;
        var resolved = descriptions.Select(description => description!.Value).ToList();
        var styles = resolved.Select(description => description.Style).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var geometries = resolved.Select(description => description.Geometry).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (styles.Count != 1 || geometries.Count != 1) return null;
        if (!FinishCodes.TryGetValue(styles[0], out var finishCode)) return null;

        var ribbonName = $"Wall_Stone_{geometries[0]}_{finishCode}";
        return new RibbonMapping
        {
            FamilyKey = mappingKey,
            CspTool = "Buildings",
            CspToolGroup = "Stone Walls",
            CspRibbonName = ribbonName,
            SuggestedRibbonName = ribbonName,
            SuggestionConfidence = resolved.Min(description => description.Confidence),
            IsManuallyConfirmed = false
        };
    }

    private static (string Style, string Geometry, double Confidence)? Describe(AssetRecord asset)
    {
        var core = CoreStoneWallPattern.Match(asset.FileName);
        if (core.Success && GeometryCodes.TryGetValue(core.Groups["geometry"].Value, out var geometry))
            return (core.Groups["style"].Value, geometry, 1.0);

        return null;
    }
}
