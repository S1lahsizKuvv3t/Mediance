namespace Mediance.Core.Windowing;

public readonly record struct PixelPoint(int X, int Y);

/// <summary>A move exists only between a matching pointer press and release.</summary>
public sealed class DragGesture
{
    private uint? _pointer;
    private PixelPoint _press;
    private PixelPoint _origin;
    public bool IsActive => _pointer is not null;
    public bool IsDragging { get; private set; }

    public void Begin(uint pointer, PixelPoint screenPosition, PixelPoint windowPosition)
    {
        _pointer = pointer;
        _press = screenPosition;
        _origin = windowPosition;
        IsDragging = false;
    }

    public PixelPoint? Move(uint pointer, bool leftButtonPressed, PixelPoint screenPosition)
    {
        if (_pointer != pointer) return null;
        if (!leftButtonPressed) { End(pointer); return null; }
        var dx = screenPosition.X - _press.X;
        var dy = screenPosition.Y - _press.Y;
        if (!IsDragging && Math.Abs((long)dx) < 4 && Math.Abs((long)dy) < 4) return null;
        IsDragging = true;
        return new(_origin.X + dx, _origin.Y + dy);
    }

    public void End(uint pointer)
    {
        if (_pointer != pointer) return;
        Cancel();
    }

    public void Cancel() { _pointer = null; IsDragging = false; }
}
