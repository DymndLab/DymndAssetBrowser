namespace FAFamilyBrowser.App.Services;

public static class FaLibraryDetector
{
    private const string AssetsDirectoryName = "_Assets";
    private const string CoreSettlementsDirectoryName = "!Core_Settlements";

    public static string? ResolveRoot(string selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath)) return null;

        var normalized = Path.GetFullPath(selectedPath.Trim());
        foreach (var candidate in CandidateRoots(normalized))
        {
            if (Directory.Exists(candidate)
                && Directory.Exists(Path.Combine(candidate, CoreSettlementsDirectoryName)))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateRoots(string selectedPath)
    {
        yield return selectedPath;

        var nestedAssets = Path.Combine(selectedPath, AssetsDirectoryName);
        if (!PathEquals(nestedAssets, selectedPath)) yield return nestedAssets;

        var directAssetsChild = Directory.Exists(selectedPath)
            ? Directory.EnumerateDirectories(selectedPath, AssetsDirectoryName, SearchOption.TopDirectoryOnly).FirstOrDefault()
            : null;
        if (directAssetsChild is not null
            && !PathEquals(directAssetsChild, selectedPath)
            && !PathEquals(directAssetsChild, nestedAssets))
        {
            yield return directAssetsChild;
        }
    }

    private static bool PathEquals(string left, string right) =>
        Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Equals(Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
}
