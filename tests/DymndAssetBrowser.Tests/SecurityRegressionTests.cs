using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using DymndAssetBrowser.App.Services;
using DymndAssetBrowser.App.ViewModels;
using DymndAssetBrowser.Core.Indexing;
using DymndAssetBrowser.Core.Models;
using DymndAssetBrowser.Core.Parsing;
using DymndAssetBrowser.Core.Persistence;
using SkiaSharp;

internal static class SecurityRegressionTests
{
    public static void Run(Action<bool, string> check) => RunAsync(check).GetAwaiter().GetResult();
    private static async Task<bool> Rejects(Func<Task> action)
    { try { await action(); return false; } catch (IOException) { return true; } }
    private static void Junction(string link, string target)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-Command");
        start.ArgumentList.Add("New-Item -ItemType Junction -Path '" + link.Replace("'", "''") + "' -Target '" + target.Replace("'", "''") + "' | Out-Null");
        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEnd(); process.WaitForExit();
        if (process.ExitCode != 0) throw new Exception("Could not create isolated junction fixture: " + error);
    }
    private static async Task RunAsync(Action<bool, string> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "DymndSecurityTests-" + Guid.NewGuid().ToString("N"));
        var links = new List<string>(); Directory.CreateDirectory(root);
        try
        {
            var library = Path.Combine(root, "library"); Directory.CreateDirectory(library);
            var webp = Path.Combine(library, "Transparent_A.webp");
            using (var bitmap = new SKBitmap(40, 20))
            {
                bitmap.Erase(SKColors.Transparent); bitmap.SetPixel(10, 10, SKColors.Red);
                bitmap.SetPixel(5, 6, new SKColor(50, 150, 250, 128));
                bitmap.SetPixel(18, 2, new SKColor(210, 75, 30, 64));
                using var image = SKImage.FromBitmap(bitmap);
                using var encoded = image.Encode(SKEncodedImageFormat.Webp, 100);
                using var output = File.Create(webp); encoded.SaveTo(output);
            }
            var originalHash = SHA256.HashData(File.ReadAllBytes(webp));
            var cacheRoot = Path.Combine(root, "cache"); var cache = new ImageCacheService(cacheRoot);
            var thumb = await cache.GetThumbnailAsync(webp);
            check(thumb.PixelWidth == 240 && thumb.PixelHeight == 120, "WebP thumbnail decodes without optional Windows codecs");
            check(await cache.GetDragFileAsync(webp, 0) == webp && await cache.GetDragFileAsync(webp, 360) == webp,
                "Untransformed WebP drags pass through the original file, including normalized zero rotation");
            check(!Directory.Exists(Path.Combine(cacheRoot, "rotated")), "Untransformed WebP drag creates no transformed cache file");
            var png = Path.Combine(root, "Reference.png");
            using (var decoded = DecodeStraightAlpha(webp))
            using (var image = SKImage.FromBitmap(decoded))
            using (var encoded = image.Encode(SKEncodedImageFormat.Png, 100))
            using (var output = File.Create(png)) encoded.SaveTo(output);
            var expectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var angle in Enumerable.Range(0, 8).Select(i => i * 45))
            foreach (var flip in new[] { false, true })
            {
                if (angle == 0 && !flip) continue;
                var transformed = await cache.GetDragFileAsync(webp, angle, flip);
                var reference = await cache.GetDragFileAsync(png, angle, flip);
                expectedFiles.Add(transformed); expectedFiles.Add(reference);
                var expected = ExpectedTransform(webp, angle, flip);
                var actualPixels = DecodePixels(transformed, premultiplied: false);
                var referencePixels = DecodePixels(reference);
                var expectedPixels = new byte[expected.PixelWidth * expected.PixelHeight * 4];
                expected.CopyPixels(expectedPixels, expected.PixelWidth * 4, 0);
                // Compare straight RGBA to isolate encoding from a decoder's own
                // premultiplication rounding; lossless WebP stores straight colors.
                if (expected.Format == PixelFormats.Pbgra32)
                    for (var i = 0; i < expectedPixels.Length; i++)
                        if (i % 4 != 3)
                        {
                            var alpha = expectedPixels[i / 4 * 4 + 3];
                            expectedPixels[i] = alpha == 0 ? (byte)0 : (byte)Math.Min(255, Math.Round(expectedPixels[i] * 255d / alpha, MidpointRounding.AwayFromZero));
                        }
                check(Path.GetExtension(transformed) == ".webp" && System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(transformed)).Contains("VP8L"),
                    $"WebP {angle} degrees / flip {flip} uses the lossless VP8L bitstream");
                check(actualPixels.Width == referencePixels.Width && actualPixels.Height == referencePixels.Height
                    && actualPixels.Pixels.Length == expectedPixels.Length
                    && actualPixels.Pixels.Select((value, i) => i % 4 != 3 && expectedPixels[i / 4 * 4 + 3] == 0 || value == expectedPixels[i]).All(equal => equal),
                    $"Lossless WebP {angle} degrees / flip {flip} preserves transformed colors and alpha exactly, with PNG-equivalent bounds");
                var timestamp = File.GetLastWriteTimeUtc(transformed);
                check(transformed == await cache.GetDragFileAsync(webp, angle + 360, flip) && File.GetLastWriteTimeUtc(transformed) == timestamp,
                    $"WebP {angle} degrees / flip {flip} reuses its existing normalized cache file");
            }
            var generatedBytes = expectedFiles.Sum(path => new FileInfo(path).Length);
            var usage = await cache.GetUsageAsync(ImageCacheKind.Transformed);
            check(usage.Count == expectedFiles.Count && usage.Bytes == generatedBytes, "Cache utility counts mixed PNG and lossless WebP transformed files accurately");
            var unrelated = Path.Combine(cacheRoot, "rotated", "my-original.webp"); File.Copy(webp, unrelated);
            var nested = Path.Combine(cacheRoot, "rotated", "nested"); Directory.CreateDirectory(nested);
            File.Copy(webp, Path.Combine(nested, "asset_0123456789ABCDEF_R90.webp"));
            var lockedPath = expectedFiles.First(path => path.EndsWith(".webp"));
            using (var held = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var cleared = await cache.ClearAsync(ImageCacheKind.Transformed);
                check(cleared.Deleted == expectedFiles.Count - 1 && cleared.Failed == 1 && File.Exists(lockedPath), "Cache cleanup preserves and reports a locked WebP");
            }
            check((await cache.ClearAsync(ImageCacheKind.Transformed)).Deleted == 1 && File.Exists(unrelated)
                && Directory.GetFiles(nested).Length == 1 && (await cache.GetUsageAsync(ImageCacheKind.Thumbnails)).Count == 1,
                "Cache cleanup handles WebP safely while preserving unrelated files, descendants and thumbnails");
            check(File.Exists(await cache.GetDragFileAsync(webp, 45, true)), "Cleared lossless WebP cache regenerates on demand");
            check(originalHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(webp))), "WebP thumbnail and drag never modify source bytes");
            var outside = Path.Combine(root, "outside"); Directory.CreateDirectory(outside); File.Copy(png, Path.Combine(outside, "outside.png"));
            var sourceLink = Path.Combine(library, "linked"); Junction(sourceLink, outside); links.Add(sourceLink);
            var cycle = Path.Combine(library, "cycle"); Junction(cycle, library); links.Add(cycle);
            File.WriteAllText(Path.Combine(library, "video.webm"), "not supported");
            var source = new AssetLibrarySource { Id = "test", Name = "test", RootPath = library, ParserProfile = LibraryParserProfiles.Generic };
            var index = await new AssetLibraryIndexer().ScanLibraryAsync(source);
            check(index.Count == 1 && index[0].FileName == "Transparent_A.webp", "Indexer accepts WebP, skips linked files/trees and cycles, and excludes video");
            check(await Rejects(async () => { await new AssetLibraryIndexer().ScanLibraryAsync(source with { RootPath = sourceLink }); }), "Linked library root is rejected with an actionable error");
            foreach (var category in new[] { "thumbnails", "rotated" })
            {
                var linkedCacheRoot = Path.Combine(root, "cache-" + category); Directory.CreateDirectory(linkedCacheRoot);
                var linked = Path.Combine(linkedCacheRoot, category); Junction(linked, outside); links.Add(linked);
                var guarded = new ImageCacheService(linkedCacheRoot);
                check(await Rejects(async () => { if (category == "thumbnails") await guarded.GetThumbnailAsync(webp); else await guarded.GetDragFileAsync(webp, 90); }), "Cache generation rejects linked " + category);
                check(await Rejects(async () => { await guarded.ClearAsync(category == "thumbnails" ? ImageCacheKind.Thumbnails : ImageCacheKind.Transformed); }), "Cache cleanup rejects linked " + category);
            }
            var ancestorLink = Path.Combine(root, "cache-parent-link"); Junction(ancestorLink, outside); links.Add(ancestorLink);
            check(await Rejects(async () => { await new ImageCacheService(Path.Combine(ancestorLink, "new-cache")).GetThumbnailAsync(webp); }), "Cache generation rejects linked ancestors before creating child directories");
            check(Directory.GetFiles(outside).Length == 1 && Directory.GetDirectories(outside).Length == 0, "Rejected cache writes leave the external target untouched");

            var stateRoot = Path.Combine(root, "state; with 'quotes'");
            var store = new ApplicationStateStore(stateRoot);
            var asset = new GenericAssetFilenameParser().Parse(library, webp) with { SourceId = "a'; DELETE FROM asset_index;--" };
            await store.SaveAsync(new ApplicationState { Libraries = [source], Assets = [asset], LastTheme = "before" });
            var first = await store.LoadAsync();
            check(first.Assets.Single().SourceId == asset.SourceId, "SQLite state path supports semicolons, spaces, and quotes without changing data");
            await store.DeleteSourceAssetsAsync("not-a'; DELETE FROM asset_index;--");
            check((await store.LoadAsync()).Assets.Count == 1, "SQL-like source IDs remain parameterized data");
            var secondStore = new ApplicationStateStore(stateRoot); var stale = await secondStore.LoadAsync();
            await store.SaveMetadataAsync(first with { LastTheme = "newer" });
            check(await Rejects(() => secondStore.SaveMetadataAsync(stale with { LastGroup = "stale" })), "Stale settings snapshot is rejected instead of overwriting newer edits");
            check((await store.LoadAsync()).LastTheme == "newer", "Conflict rejection preserves the newer saved settings");
            check(await Rejects(() => secondStore.SaveAsync(stale with { Assets = [] }))
                && (await store.LoadAsync()).Assets.Count == 1, "Stale full-state saves cannot erase the asset index");
            File.WriteAllText(store.StatePath, "{broken");
            var vm = new MainViewModel(new ApplicationStateStore(stateRoot)); await vm.InitializeAsync();
            check(!vm.IsInitialized && vm.StartupError is not null, "Corrupt settings become a recoverable startup error");
            await vm.SaveStateAsync();
            check(File.ReadAllText(store.StatePath) == "{broken", "Failed startup cannot replace damaged settings with defaults on close");
            await vm.RecoverSettingsAsync(true); await vm.InitializeAsync();
            check(vm.IsInitialized && Directory.GetFiles(stateRoot, "settings.json.recovery-*").Length == 1, "Explicit backup recovery restores startup and preserves damaged settings");
            var failedStore = new ApplicationStateStore(Path.Combine(root, "bad-state")); Directory.CreateDirectory(failedStore.AppDataRoot);
            File.WriteAllText(failedStore.StatePath, "null");
            try { await failedStore.LoadAsync(); } catch (JsonException) { }
            check(await Rejects(() => failedStore.SaveAsync(new ApplicationState())), "Invalid state blocks database and settings writes until explicit recovery");
        }
        finally
        {
            // Delete only our explicit junction entries first, never their targets.
            foreach (var link in links.AsEnumerable().Reverse()) if (Directory.Exists(link)) Directory.Delete(link);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            var resolved = Path.GetFullPath(root);
            if (!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("DymndSecurityTests-")) throw new IOException("Invalid cleanup target.");
            Directory.Delete(resolved, recursive: true);
        }
    }

    private static (int Width, int Height, byte[] Pixels) DecodePixels(string path, bool premultiplied = true)
    {
        using var stream = File.OpenRead(path);
        using var codec = SKCodec.Create(stream)!;
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, premultiplied ? SKAlphaType.Premul : SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        if (codec.GetPixels(info, bitmap.GetPixels()) != SKCodecResult.Success) throw new IOException("Test image decoding failed");
        return (info.Width, info.Height, bitmap.Bytes);
    }

    private static SKBitmap DecodeStraightAlpha(string path)
    {
        using var stream = File.OpenRead(path);
        using var codec = SKCodec.Create(stream)!;
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var bitmap = new SKBitmap(info);
        if (codec.GetPixels(info, bitmap.GetPixels()) != SKCodecResult.Success) { bitmap.Dispose(); throw new IOException("Test reference decoding failed"); }
        return bitmap;
    }

    private static BitmapSource ExpectedTransform(string path, int angle, bool flip)
    {
        using var decoded = DecodeStraightAlpha(path);
        var source = BitmapSource.Create(decoded.Width, decoded.Height, 96, 96, PixelFormats.Bgra32, null, decoded.Bytes, decoded.RowBytes);
        var transform = new TransformGroup(); transform.Children.Add(new RotateTransform(angle));
        if (flip) transform.Children.Add(new ScaleTransform(-1, 1));
        if (angle % 90 == 0) return new TransformedBitmap(source, transform);
        var matrix = transform.Value;
        var bounds = System.Windows.Rect.Transform(new System.Windows.Rect(0, 0, source.PixelWidth, source.PixelHeight), matrix);
        matrix.Translate(-bounds.Left, -bounds.Top);
        var visual = new DrawingVisual(); RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var drawing = visual.RenderOpen())
        {
            drawing.PushTransform(new MatrixTransform(matrix));
            drawing.DrawImage(source, new System.Windows.Rect(0, 0, source.PixelWidth, source.PixelHeight)); drawing.Pop();
        }
        var expected = new RenderTargetBitmap((int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height), 96, 96, PixelFormats.Pbgra32);
        expected.Render(visual); return expected;
    }
}
