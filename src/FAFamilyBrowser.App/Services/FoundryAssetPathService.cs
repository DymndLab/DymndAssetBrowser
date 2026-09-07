using System.Text.Json;
using FAFamilyBrowser.Core.Models;

namespace FAFamilyBrowser.App.Services;

public static class FoundryAssetPathService
{
    private static readonly string[] MirroredExtensions = [".webp", ".png", ".jpg", ".jpeg", ".avif"];

    public static string? Resolve(AssetRecord asset)
    {
        var dataDirectory = FindDataDirectory();
        return dataDirectory is null ? null : Resolve(asset, dataDirectory);
    }

    public static string? Resolve(AssetRecord asset, string dataDirectory)
    {
        var dataRoot = Path.GetFullPath(dataDirectory);
        var sourcePath = Path.GetFullPath(asset.FilePath);
        if (IsWithin(sourcePath, dataRoot) && File.Exists(sourcePath))
        {
            return ToFoundryPath(Path.GetRelativePath(dataRoot, sourcePath));
        }

        if (!asset.SourceId.Equals(LibrarySourceIds.ForgottenAdventures, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var relativePath = string.IsNullOrWhiteSpace(asset.RelativePath)
            ? Path.GetRelativePath(asset.SourceRoot, sourcePath)
            : asset.RelativePath;
        if (Path.IsPathRooted(relativePath)
            || relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(".."))
        {
            return null;
        }

        var mirrorRoot = Path.Combine(dataRoot, "fa-nexus-assets");
        var relativeWithoutExtension = Path.ChangeExtension(relativePath, null);
        foreach (var extension in MirroredExtensions.Prepend(Path.GetExtension(relativePath)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidate = Path.GetFullPath(Path.Combine(mirrorRoot, relativeWithoutExtension + extension));
            if (IsWithin(candidate, mirrorRoot) && File.Exists(candidate))
            {
                return ToFoundryPath(Path.GetRelativePath(dataRoot, candidate));
            }
        }

        return null;
    }

    private static string? FindDataDirectory()
    {
        var foundryRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FoundryVTT");
        var optionsPath = Path.Combine(foundryRoot, "Config", "options.json");
        try
        {
            if (File.Exists(optionsPath))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(optionsPath));
                if (document.RootElement.TryGetProperty("dataPath", out var value)
                    && !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    var configuredData = Path.Combine(value.GetString()!, "Data");
                    if (Directory.Exists(configuredData)) return configuredData;
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // Fall through to Foundry's standard local data location.
        }

        var defaultData = Path.Combine(foundryRoot, "Data");
        return Directory.Exists(defaultData) ? defaultData : null;
    }

    private static bool IsWithin(string path, string root)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string ToFoundryPath(string relativePath) => string.Join('/', relativePath
        .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
        .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
        .Select(Uri.EscapeDataString));
}
