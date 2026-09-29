using Mediance.Core.Windowing;
using Xunit;

namespace Mediance.Core.Tests;

public sealed class TaskbarCapsuleTests
{
    [Theory]
    [InlineData(1920, 1080, 1)]
    [InlineData(2560, 1440, 1)]
    [InlineData(2560, 1440, 1.25)]
    [InlineData(3840, 2160, 1.5)]
    public void CentersInsideTaskbarAtTargetDpi(int width, int height, double scale)
    {
        var barHeight = (int)(48 * scale);
        var bar = new PixelRect(0, height - barHeight, width, barHeight);
        var tray = new PixelRect(width - (int)(220 * scale), bar.Y, (int)(220 * scale), barHeight);
        var placement = TaskbarCapsuleLayout.Place(new(0, 0, width, height),
            new(0, 0, width, height - barHeight), bar, tray, scale, 240)!;
        Assert.Equal((int)(240 * scale), placement.Bounds.Width);
        Assert.InRange(Math.Abs(placement.Bounds.Y + placement.Bounds.Height / 2 - (bar.Y + bar.Height / 2)), 0, 1);
        Assert.True(placement.Bounds.X + placement.Bounds.Width < tray.X);
        Assert.True(placement.Bounds.Y >= bar.Y && placement.Bounds.Y + placement.Bounds.Height <= height);
    }

    [Fact]
    public void StartFlyoutCannotMoveCapsuleAboveReservedTaskbar()
    {
        var monitor = new PixelRect(0, 0, 1920, 1080);
        var work = new PixelRect(0, 0, 1920, 1032);
        var normal = TaskbarCapsuleLayout.Place(monitor, work, new(0, 1032, 1920, 48), new(1700, 1032, 220, 48), 1, 240);
        var expanded = TaskbarCapsuleLayout.Place(monitor, work, new(0, 200, 1920, 880), new(1700, 200, 220, 880), 1, 240);
        Assert.Equal(normal, expanded);
    }

    [Fact]
    public void HandlesNegativeMonitorCoordinatesAndMissingTray()
    {
        var placement = TaskbarCapsuleLayout.Place(new(-1920, 0, 1920, 1080), new(-1920, 0, 1920, 1032),
            new(-1920, 1032, 1920, 48), null, 1, 240)!;
        Assert.Equal(new PixelRect(-464, 1039, 240, 34), placement.Bounds);
    }

    [Fact]
    public void DoesNotInventDesktopPositionForMissingOrMalformedTaskbar()
    {
        Assert.Null(TaskbarCapsuleLayout.Place(new(0, 0, 1920, 1080), new(0, 0, 1920, 1080),
            new(0, 0, 1920, 1080), null, 1, 240));
        Assert.Null(TaskbarCapsuleLayout.Place(new(0, 0, 1920, 1080), new(0, 0, 1920, 1080), default, null, 1, 240));
    }
}
