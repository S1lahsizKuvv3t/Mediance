namespace Mediance.Core.Windowing;

public readonly record struct PixelSize(int Width, int Height);
public readonly record struct PixelRect(int X, int Y, int Width, int Height);

public static class EdgeSnap
{
    public static PixelPoint Apply(PixelPoint position, PixelSize window, PixelRect workArea,
        int threshold = 18)
    {
        if (window.Width <= 0 || window.Height <= 0 || workArea.Width <= 0 || workArea.Height <= 0)
            return position;
        threshold = Math.Max(0, threshold);
        var right = workArea.X + workArea.Width;
        var bottom = workArea.Y + workArea.Height;
        var x = Math.Abs((long)position.X - workArea.X) <= threshold
            ? workArea.X
            : Math.Abs((long)position.X + window.Width - right) <= threshold
                ? right - window.Width
                : position.X;
        var y = Math.Abs((long)position.Y - workArea.Y) <= threshold
            ? workArea.Y
            : Math.Abs((long)position.Y + window.Height - bottom) <= threshold
                ? bottom - window.Height
                : position.Y;
        return new(x, y);
    }
}

public static class WindowPlacement
{
    public static PixelPoint Clamp(PixelPoint position, PixelSize window, PixelRect workArea)
    {
        if (window.Width <= 0 || window.Height <= 0 || workArea.Width <= 0 || workArea.Height <= 0)
            return position;
        var maxX = Math.Max(workArea.X, workArea.X + workArea.Width - window.Width);
        var maxY = Math.Max(workArea.Y, workArea.Y + workArea.Height - window.Height);
        return new(Math.Clamp(position.X, workArea.X, maxX), Math.Clamp(position.Y, workArea.Y, maxY));
    }
}
