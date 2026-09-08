namespace Mediance.Core.Media;

public readonly record struct RgbColor(byte Red, byte Green, byte Blue);

public interface IArtworkPaletteService
{
    Task<RgbColor?> ExtractAsync(ArtworkData artwork, CancellationToken token = default);
}

public static class ArtworkPalette
{
    public static RgbColor? SelectDominant(ReadOnlySpan<byte> bgraPixels)
    {
        if (bgraPixels.Length < 4 || bgraPixels.Length % 4 != 0) return null;

        var counts = new int[4096];
        var red = new int[4096];
        var green = new int[4096];
        var blue = new int[4096];
        for (var offset = 0; offset < bgraPixels.Length; offset += 4)
        {
            if (bgraPixels[offset + 3] < 32) continue;
            var b = bgraPixels[offset];
            var g = bgraPixels[offset + 1];
            var r = bgraPixels[offset + 2];
            var bucket = (r >> 4 << 8) | (g >> 4 << 4) | (b >> 4);
            counts[bucket]++;
            red[bucket] += r;
            green[bucket] += g;
            blue[bucket] += b;
        }

        var winner = -1;
        var bestScore = double.MinValue;
        for (var bucket = 0; bucket < counts.Length; bucket++)
        {
            var count = counts[bucket];
            if (count == 0) continue;
            var r = red[bucket] / (double)count;
            var g = green[bucket] / (double)count;
            var b = blue[bucket] / (double)count;
            var maximum = Math.Max(r, Math.Max(g, b));
            var minimum = Math.Min(r, Math.Min(g, b));
            var saturation = maximum <= 0 ? 0 : (maximum - minimum) / maximum;
            var brightness = (r + g + b) / (3 * 255);
            var midtone = Math.Clamp(1 - Math.Abs(brightness - 0.55) * 1.5, 0, 1);
            var score = count * (0.72 + 0.28 * saturation) * (0.72 + 0.28 * midtone);
            if (score <= bestScore) continue;
            bestScore = score;
            winner = bucket;
        }

        return winner < 0 ? null : new(
            (byte)(red[winner] / counts[winner]),
            (byte)(green[winner] / counts[winner]),
            (byte)(blue[winner] / counts[winner]));
    }
}
