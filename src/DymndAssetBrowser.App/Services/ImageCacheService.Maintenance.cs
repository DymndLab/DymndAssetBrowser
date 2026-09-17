using System.IO;
using System.Text.RegularExpressions;

namespace DymndAssetBrowser.App.Services;

public enum ImageCacheKind { Thumbnails, Transformed }
public sealed record ImageCacheUsage(long Count, long Bytes);
public sealed record ImageCacheClearResult(int Deleted, int Failed);

public sealed partial class ImageCacheService
{
    private static void ValidateCacheFile(string root, string path)
    {
        root = Path.GetFullPath(root); path = Path.GetFullPath(path);
        CheckNoLinks(root);
        if (!string.Equals(Path.GetDirectoryName(path), root, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Refusing a file outside its cache directory.");
        // GetAttributes also recognizes dangling links that File.Exists may miss.
        try { if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Cache files cannot be links: " + path); }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
    }
    private string MaintenanceRoot(ImageCacheKind kind) => kind switch
    {
        ImageCacheKind.Thumbnails => Path.GetFullPath(_thumbnailRoot),
        ImageCacheKind.Transformed => Path.GetFullPath(_rotationRoot),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    private static void CheckNoLinks(string root)
    {
        for (var directory = new DirectoryInfo(root); directory is not null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Cache maintenance refuses linked folders: " + directory.FullName);
    }
    private static bool IsGenerated(FileInfo file, ImageCacheKind kind) =>
        (file.Attributes & FileAttributes.ReparsePoint) == 0 && Regex.IsMatch(file.Name,
            kind == ImageCacheKind.Thumbnails ? @"_[0-9A-F]{16}_thumb\.png$" : @"_[0-9A-F]{16}_R[0-9]{1,3}(_FH)?\.(png|webp)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public Task<ImageCacheUsage> GetUsageAsync(ImageCacheKind kind) => Task.Run(() =>
    {
        var root = MaintenanceRoot(kind); CheckNoLinks(root);
        long count = 0, bytes = 0;
        if (Directory.Exists(root)) foreach (var file in new DirectoryInfo(root).EnumerateFiles("*", SearchOption.TopDirectoryOnly))
        {
            try { if (IsGenerated(file, kind)) { bytes += file.Length; count++; } }
            catch (FileNotFoundException) { }
        }
        return new ImageCacheUsage(count, bytes);
    });
    public Task<ImageCacheClearResult> ClearAsync(ImageCacheKind kind) => Task.Run(async () =>
    {
        var root = MaintenanceRoot(kind); CheckNoLinks(root);
        if (!Directory.Exists(root)) return new ImageCacheClearResult(0, 0);
        var files = new DirectoryInfo(root).GetFiles("*", SearchOption.TopDirectoryOnly);
        var gates = kind == ImageCacheKind.Thumbnails ? _thumbnailGates : _rotationGates;
        int deleted = 0, failed = 0;
        foreach (var file in files)
        {
            var gate = gates.GetOrAdd(file.FullName, static _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync();
            try
            {
                CheckNoLinks(root);
                file.Refresh();
                if (!file.Exists || !IsGenerated(file, kind)) continue;
                if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(file.FullName)), root, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Refusing a path outside the cache category.");
                File.Delete(file.FullName); deleted++;
            }
            catch (IOException) { failed++; }
            catch (UnauthorizedAccessException) { failed++; }
            finally { gate.Release(); }
        }
        return new ImageCacheClearResult(deleted, failed);
    });
}
