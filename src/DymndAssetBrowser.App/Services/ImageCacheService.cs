using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DymndAssetBrowser.App.Services;

public sealed partial class ImageCacheService(string cacheRoot)
{
    private readonly string _thumbnailRoot = Path.GetFullPath(Path.Combine(cacheRoot, "thumbnails"));
    private readonly string _rotationRoot = Path.GetFullPath(Path.Combine(cacheRoot, "rotated"));
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _thumbnailGates =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _rotationGates =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<BitmapSource> GetThumbnailAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var cachePath = BuildCachePath(_thumbnailRoot, sourcePath, 0, false, "_thumb.png");
        ValidateCacheFile(_thumbnailRoot, cachePath);
        var gate = _thumbnailGates.GetOrAdd(cachePath, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            ValidateCacheFile(_thumbnailRoot, cachePath);
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

        var extension = Path.GetExtension(sourcePath).Equals(".webp", StringComparison.OrdinalIgnoreCase) ? ".webp" : ".png";
        var suffix = $"_R{angle}{(flipHorizontally ? "_FH" : string.Empty)}{extension}";
        var cachePath = BuildCachePath(_rotationRoot, sourcePath, angle, flipHorizontally, suffix);
        ValidateCacheFile(_rotationRoot, cachePath);
        var gate = _rotationGates.GetOrAdd(cachePath, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            ValidateCacheFile(_rotationRoot, cachePath);
            if (!File.Exists(cachePath))
            {
                await Task.Run(() => CreateTransformedImage(sourcePath, cachePath, angle, flipHorizontally), cancellationToken);
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
        ValidateCacheFile(Path.GetDirectoryName(cachePath)!, cachePath);
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        ValidateCacheFile(Path.GetDirectoryName(cachePath)!, cachePath);
        var source = LoadBitmap(sourcePath, 240);
        SaveAtomicImage(source, cachePath);
    }

    private static void CreateTransformedImage(string sourcePath, string cachePath, int angle, bool flipHorizontally)
    {
        ValidateCacheFile(Path.GetDirectoryName(cachePath)!, cachePath);
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        ValidateCacheFile(Path.GetDirectoryName(cachePath)!, cachePath);
        var source = LoadBitmap(sourcePath);
        var transforms = new TransformGroup();
        transforms.Children.Add(new RotateTransform(angle));
        if (flipHorizontally) transforms.Children.Add(new ScaleTransform(-1, 1));
        BitmapSource transformed;
        if (angle % 90 == 0) transformed = new TransformedBitmap(source, transforms);
        else
        {
            var matrix = transforms.Value;
            var bounds = System.Windows.Rect.Transform(new System.Windows.Rect(0, 0, source.PixelWidth, source.PixelHeight), matrix);
            matrix.Translate(-bounds.Left, -bounds.Top);
            var visual = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
            using (var drawing = visual.RenderOpen())
            {
                drawing.PushTransform(new MatrixTransform(matrix));
                drawing.DrawImage(source, new System.Windows.Rect(0, 0, source.PixelWidth, source.PixelHeight));
                drawing.Pop();
            }
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            transformed = bitmap;
        }
        transformed.Freeze();
        SaveAtomicImage(transformed, cachePath);
    }

    private static void SaveAtomicImage(BitmapSource source, string cachePath)
    {
        var temporaryPath = cachePath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            ValidateCacheFile(Path.GetDirectoryName(cachePath)!, temporaryPath);
            if (Path.GetExtension(cachePath).Equals(".webp", StringComparison.OrdinalIgnoreCase))
                WebPCodec.SaveLossless(source, temporaryPath);
            else
                SavePng(source, temporaryPath);
            ValidateCacheFile(Path.GetDirectoryName(cachePath)!, cachePath);
            File.Move(temporaryPath, cachePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                ValidateCacheFile(Path.GetDirectoryName(cachePath)!, temporaryPath);
                File.Delete(temporaryPath);
            }
        }
    }

    private static BitmapSource LoadBitmap(string path, int decodePixelWidth = 0)
    {
        if (Path.GetExtension(path).Equals(".webp", StringComparison.OrdinalIgnoreCase))
            return WebPCodec.Load(path, decodePixelWidth);
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
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
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
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle hFile,
        [Out] StringBuilder lpszFilePath,
        uint cchFilePath,
        uint dwFlags);
}
