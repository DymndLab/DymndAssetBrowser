namespace FAFamilyBrowser.Core.Models;

public static class AssetIdentity
{
    public static string Create(string sourceId, string filePath) =>
        $"{sourceId.Trim().ToLowerInvariant()}|{NormalizePath(filePath).ToLowerInvariant()}";

    public static string NormalizePath(string path) => Path.GetFullPath(path)
        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
}
