using System.Windows.Media.Imaging;
using FAFamilyBrowser.App.Infrastructure;
using FAFamilyBrowser.App.Services;
using FAFamilyBrowser.Core.Models;

namespace FAFamilyBrowser.App.ViewModels;

public sealed class AssetTileViewModel(AssetRecord asset, string? displayName = null, string? details = null, string? wallSetId = null) : ViewModelBase
{
    private int _rotationAngle;
    private bool _isFlippedHorizontally;
    private bool _isSelected;
    private BitmapSource? _thumbnail;
    private CancellationTokenSource? _thumbnailCancellation;
    private int _thumbnailGeneration;

    public AssetRecord Asset { get; private set; } = asset;
    public BitmapSource? Thumbnail { get => _thumbnail; private set => SetProperty(ref _thumbnail, value); }
    public string DisplayName => displayName ?? Path.GetFileNameWithoutExtension(Asset.FileName);
    public string Details => details ?? string.Join("  ·  ", new[] { Asset.PartVariant, Asset.EncodedSize }.Where(value => !string.IsNullOrWhiteSpace(value)));
    public string? WallSetId { get; } = wallSetId;

    public int RotationAngle
    {
        get => _rotationAngle;
        private set
        {
            if (SetProperty(ref _rotationAngle, value))
            {
                OnPropertyChanged(nameof(RotationLabel));
                OnPropertyChanged(nameof(TransformLabel));
            }
        }
    }

    public string RotationLabel => $"{RotationAngle}°";
    public string TransformLabel => IsFlippedHorizontally ? $"{RotationLabel}  ·  FLIP" : RotationLabel;
    public double FlipScaleX => IsFlippedHorizontally ? -1d : 1d;

    public bool IsFlippedHorizontally
    {
        get => _isFlippedHorizontally;
        private set
        {
            if (SetProperty(ref _isFlippedHorizontally, value))
            {
                OnPropertyChanged(nameof(FlipScaleX));
                OnPropertyChanged(nameof(TransformLabel));
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public void Rotate(int delta) => RotationAngle = ((RotationAngle + delta) % 360 + 360) % 360;
    public void FlipHorizontally() => IsFlippedHorizontally = !IsFlippedHorizontally;

    public async Task EnsureThumbnailAsync(ImageCacheService cache)
    {
        if (Thumbnail is not null || _thumbnailCancellation is not null) return;
        var cancellation = new CancellationTokenSource();
        _thumbnailCancellation = cancellation;
        var generation = ++_thumbnailGeneration;
        try
        {
            var thumbnail = await cache.GetThumbnailAsync(Asset.FilePath, cancellation.Token);
            if (!cancellation.IsCancellationRequested && generation == _thumbnailGeneration) Thumbnail = thumbnail;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch { }
        finally
        {
            if (ReferenceEquals(_thumbnailCancellation, cancellation)) _thumbnailCancellation = null;
            cancellation.Dispose();
        }
    }

    public void ReleaseThumbnail()
    {
        _thumbnailGeneration++;
        _thumbnailCancellation?.Cancel();
        Thumbnail = null;
    }

    public void UpdateAsset(AssetRecord asset)
    {
        if (ReferenceEquals(Asset, asset)) return;
        Asset = asset;
        OnPropertyChanged(nameof(Asset));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Details));
    }
}
