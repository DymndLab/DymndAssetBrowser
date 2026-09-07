using System.Text.RegularExpressions;

namespace FAFamilyBrowser.Core.Models;

/// <summary>
/// Curated associations between canonical Forgotten Adventures specialty-wall filenames and the
/// ribbon names shipped in the FA CSP brush package. These are product data, not user overrides.
/// </summary>
public static class FaSpecialtyWallRibbonCatalog
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    private static readonly Regex CeremorphPattern = new(
        "^Ceremorph_Wall_(?<color>Blue|Gray|Green|Purple)_(?:(?:Connector|Corner|Diagonal|Ending|Joint|Straight_Path)_)?(?<geometry>[A-G])(?:[0-9])?(?:_|\\.|$)",
        Options);
    private static readonly Regex AdobePattern = new(
        "^Wall_Adobe_(?<color>Red|Sandstone|White)_(?<shape>Sloped|Thin|Wide)_(?<geometry>[A-D])(?:[0-9])?(?:_|\\.|$)",
        Options);
    private static readonly Regex HalfAdobePattern = new(
        "^Half_Wall_Adobe_(?<color>Red|Sandstone|White)_Thin_(?<geometry>A)(?:[0-9])?(?:_|\\.|$)",
        Options);
    private static readonly Regex CrenellationPattern = new(
        "^Wall_Crenellations_Adobe_(?<color>Red|Sandstone|White)_(?<geometry>[AB])(?:[0-9])?(?:_|\\.|$)",
        Options);
    private static readonly Regex MudbrickPattern = new(
        "^Wall_Mudbrick_(?<color>Light|Red)_(?<shape>Thin|Wide)_(?<geometry>A)(?:[0-9])?(?:_|\\.|$)",
        Options);
    private static readonly Regex MosaicPattern = new(
        "^Mosaic_Wall_(?<geometry>[AB])_(?<color>Blue|Sandstone|Slate|Terracotta|White)(?:_|\\.|$)",
        Options);
    private static readonly Regex TilePattern = new(
        "^Wall_Tile_(?<geometry>[ABC])_(?<style>PatternA|PatternB|PatternC|PatternD|Sandstone|Slate|Terracotta|White)(?:_|\\.|$)",
        Options);
    private static readonly Regex MetalPattern = new(
        "^Wall_Metal_(?<color>Brass|Gray|Polished|Rusty|Soot)_(?<geometry>D1|D2|E1|E2|A|B|C|F)(?:_|\\.|$)",
        Options);
    private static readonly Regex DwarvenPattern = new(
        "^Dwarven_Wall_Stone_(?<finish>Earthy|Redrock|Sandstone|Slate|Volcanic)(?:_Metal_(?<metal>Black|Brass|Gray|Green|Polished|Rusty))?.*?_(?<geometry>[A-H])(?:[0-9])?(?:_|\\.|$)",
        Options);
    private static readonly Regex ShackPattern = new(
        "^Shack_Wall_Wood_A_(?<color>Frosty|Ashen|Dark|Light|Red|Walnut)_(?:(?:Corner|Diagonal|End)_)?(?<geometry>[ABC])(?:[0-9])(?:_|\\.|$)",
        Options);

    private static readonly IReadOnlyDictionary<string, string> MetalGeometryCodes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "A", ["B"] = "B", ["C"] = "C", ["D1"] = "D",
            ["D2"] = "E", ["E1"] = "F1", ["E2"] = "F2", ["F"] = "G"
        };

    private static readonly HashSet<string> ExistingDwarvenBrushes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dwarven_Wall_A_Earthy", "Dwarven_Wall_A_Redrock", "Dwarven_Wall_A_Sandstone", "Dwarven_Wall_A_Slate", "Dwarven_Wall_A_Volcanic",
        "Dwarven_Wall_B_Earthy", "Dwarven_Wall_B_Redrock", "Dwarven_Wall_B_Sandstone", "Dwarven_Wall_B_Slate", "Dwarven_Wall_B_Volcanic",
        "Dwarven_Wall_C_Slate",
        "Dwarven_Wall_D_Earthy", "Dwarven_Wall_D_Redrock", "Dwarven_Wall_D_Sandstone", "Dwarven_Wall_D_Slate", "Dwarven_Wall_D_Volcanic",
        "Dwarven_Wall_E_Slate", "Dwarven_Wall_F_Slate", "Dwarven_Wall_Ruined_F_Slate",
        "Dwarven_Wall_G_Earthy_Green", "Dwarven_Wall_G_Redrock_Rusty", "Dwarven_Wall_G_Sandstone_Black",
        "Dwarven_Wall_G_Slate_Brass", "Dwarven_Wall_G_Slate_Gray", "Dwarven_Wall_G_Volcanic_Polished",
        "Dwarven_Wall_H_Earthy", "Dwarven_Wall_H_Redrock", "Dwarven_Wall_H_Sandstone", "Dwarven_Wall_H_Slate", "Dwarven_Wall_H_Volcanic"
    };

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

    public static (string ToolGroup, string RibbonName)? Describe(AssetRecord asset)
    {
        var name = asset.FileName;

        var ceremorph = CeremorphPattern.Match(name);
        if (ceremorph.Success)
            return ("Ceremorph Walls", $"Ceremorph_Wall_{Canonical(ceremorph, "color")}_{Upper(ceremorph, "geometry")}");

        var halfAdobe = HalfAdobePattern.Match(name);
        if (!halfAdobe.Success && SetFolder(asset, "Half_Wall_Adobe_A"))
            halfAdobe = Regex.Match(name, "^Half_Wall_Adobe_(?<color>Red|Sandstone|White)_Thin_", Options);
        if (halfAdobe.Success)
            return ("Desert Walls", $"Desert_Half_Wall_Adobe_{Canonical(halfAdobe, "color")}_Thin_A");

        var adobe = AdobePattern.Match(name);
        if (!adobe.Success)
        {
            var folder = Regex.Match(asset.RelativePath.Replace('\\', '/'),
                "/Wall_Adobe_(?<shape>Sloped|Thin|Wide)_(?<geometry>[A-D])(?:/|$)", Options);
            var color = Regex.Match(name, "^Wall_Adobe_(?<color>Red|Sandstone|White)_(?:Sloped|Thin|Wide)_", Options);
            if (folder.Success && color.Success)
                adobe = Regex.Match($"Wall_Adobe_{color.Groups["color"].Value}_{folder.Groups["shape"].Value}_{folder.Groups["geometry"].Value}_", AdobePattern.ToString(), Options);
        }
        if (adobe.Success)
        {
            var shape = Canonical(adobe, "shape") == "Sloped" ? "Angled" : Canonical(adobe, "shape");
            return ("Desert Walls", $"Desert_Wall_Adobe_{Canonical(adobe, "color")}_{shape}_{Upper(adobe, "geometry")}");
        }

        var crenellation = CrenellationPattern.Match(name);
        if (crenellation.Success)
            return ("Desert Walls", $"Desert_Crenellations_Adobe_{Canonical(crenellation, "color")}_{Upper(crenellation, "geometry")}");

        var mudbrick = MudbrickPattern.Match(name);
        if (mudbrick.Success)
        {
            var color = Canonical(mudbrick, "color") == "Light" ? "Brown" : "Red";
            return ("Desert Walls", $"Desert_Wall_Mudbrick_{color}_{Canonical(mudbrick, "shape")}_A");
        }

        var mosaic = MosaicPattern.Match(name);
        if (mosaic.Success)
        {
            var geometry = Upper(mosaic, "geometry");
            var color = Canonical(mosaic, "color");
            var prefix = geometry == "B" && color == "Blue" ? "Desert_Mosaic_Wall" : "Mosaic_Wall";
            return ("Mosaic & Tile Walls", $"{prefix}_{geometry}_{color}");
        }

        var tile = TilePattern.Match(name);
        if (tile.Success)
        {
            var style = tile.Groups["style"].Value;
            if (style.StartsWith("Pattern", StringComparison.OrdinalIgnoreCase))
                style = $"Pattern{char.ToUpperInvariant(style[^1])}";
            else
                style = Canonical(style);
            return ("Mosaic & Tile Walls", $"Tile_Wall_{Upper(tile, "geometry")}_{style}");
        }

        var metal = MetalPattern.Match(name);
        if (metal.Success && MetalGeometryCodes.TryGetValue(metal.Groups["geometry"].Value, out var metalGeometry))
            return ("Metal Walls", $"Wall_Metal_{metalGeometry}_{Canonical(metal, "color")}");

        var dwarven = DwarvenPattern.Match(name);
        if (dwarven.Success)
        {
            var geometry = Upper(dwarven, "geometry");
            var finish = Canonical(dwarven, "finish");
            var ruined = name.Contains("_Ruined_", StringComparison.OrdinalIgnoreCase) ? "Ruined_" : string.Empty;
            var metalAccent = dwarven.Groups["metal"].Success ? $"_{Canonical(dwarven, "metal")}" : string.Empty;
            var ribbon = $"Dwarven_Wall_{ruined}{geometry}_{finish}{metalAccent}";
            if (ExistingDwarvenBrushes.Contains(ribbon)) return ("Dwarven Walls", ribbon);
        }

        var shack = ShackPattern.Match(name);
        if (shack.Success)
            return ("Shack Walls", $"Shack_Wall_Wood_{Upper(shack, "geometry")}_{Canonical(shack, "color")}");

        return null;
    }

    private static bool SetFolder(AssetRecord asset, string folder) => asset.RelativePath.Replace('\\', '/')
        .Split('/', StringSplitOptions.RemoveEmptyEntries)
        .Contains(folder, StringComparer.OrdinalIgnoreCase);

    private static string Upper(Match match, string group) => match.Groups[group].Value.ToUpperInvariant();
    private static string Canonical(Match match, string group) => Canonical(match.Groups[group].Value);
    private static string Canonical(string value) => char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
}
