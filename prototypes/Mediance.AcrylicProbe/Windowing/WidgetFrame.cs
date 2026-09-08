using Mediance.Windows.Windowing;
using Mediance.Core.Windowing;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace Mediance.AcrylicProbe.Windowing;

internal sealed class WidgetFrame
{
    private readonly nint _handle;
    private readonly AppWindow _window;
    private readonly OverlappedPresenter _presenter;

    internal WidgetFrame(Window window, double widthDip = 400, double heightDip = 300)
    {
        _handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        _window = window.AppWindow;
        _presenter = (OverlappedPresenter)_window.Presenter;
        _presenter.SetBorderAndTitleBar(false, false);
        _presenter.IsResizable = false;
        _presenter.IsMaximizable = false;
        _presenter.IsMinimizable = true;
        NativeWindowFeatures.ConfigureAppearance(_handle);

        var scale = NativeWindowFeatures.DpiScale(_handle);
        var area = DisplayArea.GetFromWindowId(_window.Id, DisplayAreaFallback.Nearest).WorkArea;
        var width = Math.Min((int)Math.Round(widthDip * scale), area.Width);
        var height = Math.Min((int)Math.Round(heightDip * scale), area.Height);
        _window.MoveAndResize(new RectInt32(area.X + Math.Max(0, area.Width - width - (int)(40 * scale)),
            area.Y + Math.Max(0, (area.Height - height) / 2), width, height));
    }

    internal void SetTopmost(bool enabled) => _presenter.IsAlwaysOnTop = enabled;
    internal bool IsVisible => IsWindowVisible(_handle);
    internal void Hide() => ShowWindow(_handle, 0);
    internal void ShowWithoutActivation() => ShowWindow(_handle, 4);
    internal void RestorePosition(int x, int y)
    {
        var desired = new Mediance.Core.Windowing.PixelPoint(x, y);
        var area = NativeWindowFeatures.WorkAreaAt(desired);
        var placement = WindowPlacement.Clamp(desired, new(_window.Size.Width, _window.Size.Height), area);
        _window.Move(new PointInt32(placement.X, placement.Y));
    }
    internal void RestorePosition(int x, int y, PixelRect area)
    {
        var placement = WindowPlacement.Clamp(new(x, y), new(_window.Size.Width, _window.Size.Height), area);
        _window.Move(new PointInt32(placement.X, placement.Y));
    }
    internal void ShowAndActivate(Window window)
    {
        ShowWindow(_handle, 5);
        SetForegroundWindow(_handle);
        window.Activate();
    }
    internal void ResizeContent(double widthDip, double heightDip)
    {
        var scale = NativeWindowFeatures.DpiScale(_handle);
        var area = DisplayArea.GetFromWindowId(_window.Id, DisplayAreaFallback.Nearest).WorkArea;
        var size = new SizeInt32(Math.Min((int)Math.Ceiling(widthDip * scale), area.Width - 8),
            Math.Min((int)Math.Ceiling(Math.Max(100, heightDip) * scale), area.Height - 8));
        if (_window.ClientSize.Width != size.Width || _window.ClientSize.Height != size.Height) _window.ResizeClient(size);
    }

    internal void PlaceBeside(AppWindow owner)
    {
        var area = DisplayArea.GetFromWindowId(owner.Id, DisplayAreaFallback.Nearest).WorkArea;
        var x = owner.Position.X + owner.Size.Width + 12;
        if (x + _window.Size.Width > area.X + area.Width) x = owner.Position.X - _window.Size.Width - 12;
        _window.Move(new PointInt32(Math.Clamp(x, area.X, Math.Max(area.X, area.X + area.Width - _window.Size.Width)),
            Math.Clamp(owner.Position.Y, area.Y, Math.Max(area.Y, area.Y + area.Height - _window.Size.Height))));
    }

    internal async Task VerifyAsync()
    {
        var original = _window.Position;
        SetTopmost(true);
        await Task.Delay(100);
        if (!NativeWindowFeatures.IsTopmost(_handle)) throw new InvalidOperationException("Topmost did not reach native window state.");
        SetTopmost(false);
        if (NativeWindowFeatures.IsTopmost(_handle)) throw new InvalidOperationException("Topmost did not clear.");
        _window.Move(new PointInt32(original.X + 12, original.Y + 12));
        await Task.Delay(100);
        if (_window.Position.X != original.X + 12 || _window.Position.Y != original.Y + 12)
            throw new InvalidOperationException("Native window position did not change.");
        _window.Move(original);
        ProbeLog.Write("WindowSmoke: topmost on/off and move/restore passed");
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);
}
