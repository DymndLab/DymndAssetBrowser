using System.Windows;
using DymndAssetBrowser.App.Services;

namespace DymndAssetBrowser.App;

public partial class CacheUtilitiesWindow : Window
{
    private readonly ImageCacheService _cache;
    private bool _busy;
    public CacheUtilitiesWindow(ImageCacheService cache)
    {
        _cache = cache; InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
        Closing += (_, e) => { if (_busy) e.Cancel = true; };
    }
    private static string Format(ImageCacheUsage usage) => $"{usage.Count:N0} files · " +
        (usage.Bytes >= 1024L * 1024 * 1024 ? $"{usage.Bytes / (1024d * 1024 * 1024):N2} GiB" : $"{usage.Bytes / (1024d * 1024):N1} MiB");
    private async Task UpdateCountsAsync()
    {
        var thumbnails = await _cache.GetUsageAsync(ImageCacheKind.Thumbnails);
        var transformed = await _cache.GetUsageAsync(ImageCacheKind.Transformed);
        ThumbnailUsage.Text = Format(thumbnails); TransformedUsage.Text = Format(transformed);
        ClearThumbnails.IsEnabled = thumbnails.Count > 0; ClearTransformed.IsEnabled = transformed.Count > 0;
    }
    private void Busy(bool value) { _busy = value; Actions.IsEnabled = !value; }
    private async Task RefreshAsync()
    {
        if (_busy) return;
        Busy(true); Status.Text = "Counting generated files…";
        try { await UpdateCountsAsync(); Status.Text = ""; }
        catch (Exception ex) { Status.Text = "Could not read cache: " + ex.Message; }
        finally { Busy(false); }
    }
    private async Task ClearAsync(ImageCacheKind kind)
    {
        if (_busy) return;
        var label = kind == ImageCacheKind.Thumbnails ? "thumbnails" : "transformed images";
        var warning = kind == ImageCacheKind.Transformed
            ? "Avoid clearing cache while a file is being imported from the browser.\n\nFile Objects in your CSP projects that link to cached images may stop working. Imported images will be unaffected.\n\n" : "";
        if (MessageBox.Show(this, warning + $"Delete cached {label}?",
            "Clear cache", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        Busy(true); Status.Text = "Clearing generated files…";
        try
        {
            var result = await _cache.ClearAsync(kind);
            await UpdateCountsAsync();
            Status.Text = $"Deleted {result.Deleted:N0} files; {result.Failed:N0} could not be removed. Deleted cache files can be regenerated from your originals.";
        }
        catch (Exception ex) { Status.Text = "Could not clear cache: " + ex.Message; }
        finally { Busy(false); }
    }
    private async void ClearThumbnails_Click(object sender, RoutedEventArgs e) => await ClearAsync(ImageCacheKind.Thumbnails);
    private async void ClearTransformed_Click(object sender, RoutedEventArgs e) => await ClearAsync(ImageCacheKind.Transformed);
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
}
