using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FAFamilyBrowser.App.Services;

public sealed class ImageCacheService(string cacheRoot)
{
    private readonly string _thumbnailRoot = Path.Combine(cacheRoot, "thumbnails");
    private readonly string _rotationRoot = Path.Combine(cacheRoot, "rotated");
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _thumbnailGates =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _rotationGates =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<BitmapSource> GetThumbnailAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var cachePath = BuildCachePath(_thumbnailRoot, sourcePath, 0, false, "_thumb.png");
        var gate = _thumbnailGates.GetOrAdd(cachePath, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(cachePath))
                await Task.Run(() => CreateThumbnail(sourcePath, cachePath), cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(() => LoadBitmap(cachePath, 240), cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async Task<string> GetDragFileAsync(string sourcePath, int angle, bool flipHorizontally = false, CancellationToken cancellationToken = default)
    {
        angle = ((angle % 360) + 360) % 360;
        if (angle == 0 && !flipHorizontally)
        {
            return sourcePath;
        }

        var suffix = $"_R{angle}{(flipHorizontally ? "_FH" : string.Empty)}.png";
        var cachePath = BuildCachePath(_rotationRoot, sourcePath, angle, flipHorizontally, suffix);
        var gate = _rotationGates.GetOrAdd(cachePath, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(cachePath))
            {
                await Task.Run(() => CreateTransformedPng(sourcePath, cachePath, angle, flipHorizontally), cancellationToken);
            }

            // A packaged launcher can redirect LocalAppData writes into its package LocalCache.
            // CSP is a separate process and cannot resolve the nominal redirected path, so the
            // FileDrop payload must contain the backing path reported by the open file handle.
            return ResolvePhysicalPath(cachePath);
        }
        finally
        {
            gate.Release();
        }
    }

    private static string BuildCachePath(string root, string sourcePath, int angle, bool flipHorizontally, string suffix)
    {
        var info = new FileInfo(sourcePath);
        var fingerprint = $"{sourcePath}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{angle}|{flipHorizontally}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)))[..16];
        var safeName = Path.GetFileNameWithoutExtension(sourcePath);
        return Path.Combine(root, $"{safeName}_{hash}{suffix}");
    }

    private static void CreateThumbnail(string sourcePath, string cachePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        var source = LoadBitmap(sourcePath, 240);
        SavePng(source, cachePath);
    }

    private static void CreateTransformedPng(string sourcePath, string cachePath, int angle, bool flipHorizontally)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        var source = LoadBitmap(sourcePath);
        var transforms = new TransformGroup();
        transforms.Children.Add(new RotateTransform(angle));
        if (flipHorizontally) transforms.Children.Add(new ScaleTransform(-1, 1));
        var transformed = new TransformedBitmap(source, transforms);
        transformed.Freeze();
        var temporaryPath = cachePath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            SavePng(transformed, temporaryPath);
            File.Move(temporaryPath, cachePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static BitmapSource LoadBitmap(string path, int decodePixelWidth = 0)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        if (decodePixelWidth > 0)
        {
            image.DecodePixelWidth = decodePixelWidth;
        }
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static void SavePng(BitmapSource source, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
        stream.Flush(flushToDisk: true);
    }

    private static string ResolvePhysicalPath(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var buffer = new StringBuilder(512);
        var requiredLength = GetFinalPathNameByHandle(stream.SafeFileHandle, buffer, (uint)buffer.Capacity, 0);
        if (requiredLength == 0)
        {
            throw new IOException($"Could not resolve the physical cache path for '{path}'.",
                new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
        }

        if (requiredLength >= buffer.Capacity)
        {
            buffer.EnsureCapacity(checked((int)requiredLength + 1));
            requiredLength = GetFinalPathNameByHandle(stream.SafeFileHandle, buffer, (uint)buffer.Capacity, 0);
            if (requiredLength == 0)
            {
                throw new IOException($"Could not resolve the physical cache path for '{path}'.",
                    new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
            }
        }

        const string extendedPathPrefix = @"\\?\";
        const string extendedUncPrefix = @"\\?\UNC\";
        var resolved = buffer.ToString();
        if (resolved.StartsWith(extendedUncPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return @"\\" + resolved[extendedUncPrefix.Length..];
        }

        return resolved.StartsWith(extendedPathPrefix, StringComparison.OrdinalIgnoreCase)
            ? resolved[extendedPathPrefix.Length..]
            : resolved;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle hFile,
        [Out] StringBuilder lpszFilePath,
        uint cchFilePath,
        uint dwFlags);
}
