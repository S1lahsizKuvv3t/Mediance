using Mediance.Core.Audio;
using Mediance.Core.Media;
using Xunit;

namespace Mediance.Core.Tests;

public sealed class MediaEnhancementTests
{
    [Fact]
    public void ProgressClampsAndMapsBackToTimeline()
    {
        var start = TimeSpan.FromSeconds(10);
        var end = TimeSpan.FromSeconds(110);
        Assert.Equal(0.25, PlaybackProgress.Fraction(TimeSpan.FromSeconds(35), start, end), 6);
        Assert.Equal(TimeSpan.FromSeconds(85), PlaybackProgress.PositionAt(0.75, start, end));
        Assert.Equal(start, PlaybackProgress.PositionAt(-3, start, end));
        Assert.Equal(end, PlaybackProgress.PositionAt(4, start, end));
    }

    [Fact]
    public void DominantArtworkColorIgnoresTransparentPixels()
    {
        byte[] pixels =
        [
            200, 40, 20, 255,
            205, 42, 22, 255,
            198, 38, 18, 255,
            0, 255, 0, 0
        ];
        var color = ArtworkPalette.SelectDominant(pixels);
        Assert.NotNull(color);
        Assert.InRange(color.Value.Red, 18, 22);
        Assert.InRange(color.Value.Green, 38, 42);
        Assert.InRange(color.Value.Blue, 198, 205);
    }

    [Fact]
    public void VolumeResultNormalizesForHud()
    {
        Assert.Equal(100, ApplicationVolumeResult.Success(2, false).Percent);
        Assert.Equal(0, ApplicationVolumeResult.Success(-1, true).Percent);
    }
}
