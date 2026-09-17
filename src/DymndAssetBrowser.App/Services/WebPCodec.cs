using SkiaSharp;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DymndAssetBrowser.App.Services;

internal static class WebPCodec
{
    public static void SaveLossless(BitmapSource source, string path)
    {
        // Convert premultiplied render-target pixels with round-to-nearest. WPF's
        // generic conversion and the encoder's implicit conversion can truncate colors.
        var premultiplied = source.Format == PixelFormats.Pbgra32;
        var pixelsSource = premultiplied || source.Format == PixelFormats.Bgra32 ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        using var bitmap = new SKBitmap(new SKImageInfo(source.PixelWidth, source.PixelHeight, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        if (premultiplied)
        {
            var bytes = new byte[bitmap.ByteCount]; pixelsSource.CopyPixels(bytes, bitmap.RowBytes, 0);
            for (var i = 0; i < bytes.Length; i += 4)
            {
                var alpha = bytes[i + 3];
                for (var channel = 0; channel < 3; channel++)
                    bytes[i + channel] = alpha == 0 ? (byte)0 : (byte)Math.Min(255, (bytes[i + channel] * 255 + alpha / 2) / alpha);
            }
            System.Runtime.InteropServices.Marshal.Copy(bytes, 0, bitmap.GetPixels(), bytes.Length);
        }
        else pixelsSource.CopyPixels(System.Windows.Int32Rect.Empty, bitmap.GetPixels(), bitmap.ByteCount, bitmap.RowBytes);
        using var pixels = bitmap.PeekPixels();
        // With Lossless, quality controls compression effort only, not image quality.
        // Moderate effort keeps the first drag responsive; subsequent drags reuse the file.
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        if (!SKWebpEncoder.Encode(output, pixels, new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 50)))
            throw new IOException("Unable to encode the transformed image as lossless WebP.");
        output.Flush(flushToDisk: true);
    }

    // A bundled decoder avoids depending on optional Windows image codecs. Animated
    // WebP uses its first frame; this browser does not play or export animation.
    public static BitmapSource Load(string path, int thumbnailWidth)
    {
        using var stream = File.OpenRead(path);
        using var codec = SKCodec.Create(stream) ?? throw new IOException("Unable to decode WebP image.");
        var original = codec.Info;
        if (original.Width <= 0 || original.Height <= 0 || (long)original.Width * original.Height > 100_000_000)
            throw new IOException("WebP image dimensions exceed the supported 100-megapixel limit.");
        var dimensions = thumbnailWidth > 0 && original.Width > thumbnailWidth
            ? codec.GetScaledDimensions((float)thumbnailWidth / original.Width)
            : new SKSizeI(original.Width, original.Height);
        // Keep straight-alpha color values until WPF actually needs to composite them.
        // This avoids a needless premultiply/unpremultiply round trip for flips and 90-degree turns.
        var info = new SKImageInfo(dimensions.Width, dimensions.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        if (codec.GetPixels(info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new IOException("The WebP image is incomplete or unsupported.");
        var result = BitmapSource.Create(info.Width, info.Height, 96, 96, PixelFormats.Bgra32, null, bitmap.Bytes, bitmap.RowBytes);
        result.Freeze(); return result;
    }
}
