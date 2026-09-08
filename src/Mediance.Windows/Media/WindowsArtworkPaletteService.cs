using Mediance.Core.Media;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Mediance.Windows.Media;

public sealed class WindowsArtworkPaletteService : IArtworkPaletteService
{
    public async Task<RgbColor?> ExtractAsync(ArtworkData artwork, CancellationToken token = default)
    {
        if (artwork.Bytes.Length == 0) return null;
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(artwork.Bytes);
            await writer.StoreAsync().AsTask(token);
        }

        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(token);
        const uint longestEdge = 32;
        var scale = Math.Min(1d, longestEdge / (double)Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        var transform = new BitmapTransform
        {
            ScaledWidth = Math.Max(1, (uint)Math.Round(decoder.PixelWidth * scale)),
            ScaledHeight = Math.Max(1, (uint)Math.Round(decoder.PixelHeight * scale)),
            InterpolationMode = BitmapInterpolationMode.Fant
        };
        var pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight,
            transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb).AsTask(token);
        return ArtworkPalette.SelectDominant(pixels.DetachPixelData());
    }
}
