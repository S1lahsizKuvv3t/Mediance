namespace Mediance.Core.Windowing;

public sealed record CapsulePlacement(PixelRect Bounds, double Scale);

public static class TaskbarCapsuleLayout
{
    // Shell bounds can temporarily include the Start flyout. The reserved desktop
    // edge is stable and belongs to the taskbar, regardless of that flyout.
    public static CapsulePlacement? Place(PixelRect monitor, PixelRect work, PixelRect shell,
        PixelRect? notification, double dpiScale, double desiredWidthDip)
    {
        var scale = Math.Clamp(dpiScale, 1, 4);
        var strips = new[]
        {
            new PixelRect(monitor.X, work.Y + work.Height, monitor.Width,
                monitor.Y + monitor.Height - work.Y - work.Height),
            new PixelRect(monitor.X, monitor.Y, monitor.Width, work.Y - monitor.Y),
            new PixelRect(monitor.X, monitor.Y, work.X - monitor.X, monitor.Height),
            new PixelRect(work.X + work.Width, monitor.Y,
                monitor.X + monitor.Width - work.X - work.Width, monitor.Height)
        };
        var limit = (int)Math.Ceiling(128 * scale);
        var bar = strips.Where(rect => rect.Width > 0 && rect.Height > 0 &&
                Math.Min(rect.Width, rect.Height) <= limit && Intersects(rect, shell))
            .OrderByDescending(rect => IntersectionArea(rect, shell)).FirstOrDefault();
        if (bar.Width == 0 || bar.Height == 0)
        {
            // Auto-hidden bars have no reserved strip. Use only a sane on-screen
            // shell band; never invent a floating location above the taskbar.
            if (shell.Width <= 0 || shell.Height <= 0 || Math.Min(shell.Width, shell.Height) > limit ||
                !Intersects(shell, monitor)) return null;
            bar = shell;
        }
        var gap = Math.Max(2, (int)Math.Round(4 * scale));
        var horizontal = bar.Width >= bar.Height;
        var height = Math.Min((int)Math.Round(34 * scale), bar.Height - 2 * gap);
        var width = Math.Min((int)Math.Round(Math.Clamp(desiredWidthDip, 180, 292) * scale), bar.Width - 2 * gap);
        if (height < 20 * scale || width < 100 * scale) return null;
        var tray = notification is { } area && Intersects(area, bar) ? notification : null;
        if (horizontal)
        {
            var anchor = tray?.X ?? bar.X + bar.Width - (int)Math.Round(220 * scale);
            var x = Math.Clamp(anchor - width - gap, bar.X + gap, bar.X + bar.Width - width - gap);
            return new(new(x, bar.Y + (bar.Height - height) / 2, width, height), scale);
        }
        var yAnchor = tray?.Y ?? bar.Y + bar.Height - (int)Math.Round(180 * scale);
        var y = Math.Clamp(yAnchor - height - gap, bar.Y + gap, bar.Y + bar.Height - height - gap);
        return new(new(bar.X + (bar.Width - width) / 2, y, width, height), scale);
    }

    private static bool Intersects(PixelRect a, PixelRect b) => IntersectionArea(a, b) > 0;
    private static long IntersectionArea(PixelRect a, PixelRect b) =>
        (long)Math.Max(0, Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X)) *
        Math.Max(0, Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y));
}
