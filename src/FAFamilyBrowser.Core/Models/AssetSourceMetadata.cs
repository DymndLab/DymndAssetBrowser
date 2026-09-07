using System.Text.RegularExpressions;

namespace FAFamilyBrowser.Core.Models;

/// <summary>
/// Objective source metadata used by the Browser's advanced filters. Unlike parsed
/// Family and Variant fields, these values do not change meaning between asset types.
/// </summary>
public static partial class AssetSourceMetadata
{
    public const string LibraryRoot = "Library Root";
    public const string UnspecifiedVariant = "Unspecified";

    public static string SourceSet(AssetRecord asset)
    {
        var normalized = asset.RelativePath.Replace('\\', '/').Trim('/');
        var separator = normalized.LastIndexOf('/');
        if (separator < 0) return LibraryRoot;

        var parentPath = normalized[..separator];
        var parentSeparator = parentPath.LastIndexOf('/');
        var folder = parentSeparator < 0 ? parentPath : parentPath[(parentSeparator + 1)..];
        return Humanize(folder);
    }

    public static string FilenameVariant(AssetRecord asset)
    {
        var tokens = Path.GetFileNameWithoutExtension(asset.FileName)
            .Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = tokens.Length - 1; index >= 0; index--)
            if (FilenameVariantToken().IsMatch(tokens[index])) return tokens[index].ToUpperInvariant();
        return UnspecifiedVariant;
    }

    private static string Humanize(string value)
    {
        var expanded = value.TrimStart('!').Replace('_', ' ').Replace('-', ' ');
        return Whitespace().Replace(expanded, " ").Trim();
    }

    [GeneratedRegex("^(?:[A-Z]|[A-Z]{1,3}\\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FilenameVariantToken();

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
