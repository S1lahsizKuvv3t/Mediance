using Mediance.Core.Windowing;
using Mediance.Windows.Windowing;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Mediance.AcrylicProbe.Windowing;

internal sealed class WindowDragBehavior : IDisposable
{
    private readonly UIElement _handle;
    private readonly AppWindow _window;
    private readonly Func<bool> _enabled;
    private readonly DragGesture _gesture = new();
    internal WindowDragBehavior(UIElement handle, AppWindow window, Func<bool>? enabled = null)
    {
        _handle = handle;
        _window = window;
        _enabled = enabled ?? (() => true);
        handle.PointerPressed += Pressed;
        handle.PointerMoved += Moved;
        handle.PointerReleased += Released;
        handle.PointerCanceled += Released;
        handle.PointerCaptureLost += LostCapture;
    }
    internal event EventHandler? Completed;

    private void Pressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_enabled() || IsInteractive(e.OriginalSource as DependencyObject) ||
            !e.GetCurrentPoint(_handle).Properties.IsLeftButtonPressed || !_handle.CapturePointer(e.Pointer)) return;
        _gesture.Begin(e.Pointer.PointerId, NativeWindowFeatures.PointerPosition(), new(_window.Position.X, _window.Position.Y));
        e.Handled = true;
    }

    private void Moved(object sender, PointerRoutedEventArgs e)
    {
        if (!_gesture.IsActive) return;
        if (!_enabled())
        {
            _gesture.Cancel();
            _handle.ReleasePointerCapture(e.Pointer);
            return;
        }
        var down = e.GetCurrentPoint(_handle).Properties.IsLeftButtonPressed;
        var pointer = NativeWindowFeatures.PointerPosition();
        var destination = _gesture.Move(e.Pointer.PointerId, down, pointer);
        if (destination is { } p)
        {
            var area = NativeWindowFeatures.WorkAreaAt(pointer);
            var snapped = EdgeSnap.Apply(p, new(_window.Size.Width, _window.Size.Height),
                area);
            _window.Move(new PointInt32(snapped.X, snapped.Y));
        }
        if (!down) _handle.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void Released(object sender, PointerRoutedEventArgs e)
    {
        var completed = _gesture.IsActive;
        _gesture.End(e.Pointer.PointerId);
        _handle.ReleasePointerCapture(e.Pointer);
        if (completed) Completed?.Invoke(this, EventArgs.Empty);
    }
    private void LostCapture(object sender, PointerRoutedEventArgs e)
    {
        var completed = _gesture.IsActive;
        _gesture.End(e.Pointer.PointerId);
        if (completed) Completed?.Invoke(this, EventArgs.Empty);
    }

    private bool IsInteractive(DependencyObject? source)
    {
        for (var current = source; current is not null && !ReferenceEquals(current, _handle);
             current = VisualTreeHelper.GetParent(current))
            if (current is Control) return true;
        return false;
    }

    public void Dispose()
    {
        _gesture.Cancel();
        _handle.ReleasePointerCaptures();
        _handle.PointerPressed -= Pressed;
        _handle.PointerMoved -= Moved;
        _handle.PointerReleased -= Released;
        _handle.PointerCanceled -= Released;
        _handle.PointerCaptureLost -= LostCapture;
    }
}
