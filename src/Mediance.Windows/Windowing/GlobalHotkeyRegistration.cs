using System.ComponentModel;
using System.Runtime.InteropServices;
using Mediance.Core.Settings;

namespace Mediance.Windows.Windowing;

public sealed class GlobalHotkeyRegistration : IDisposable
{
    private const uint WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private static int s_nextId = 0x4D00;
    private readonly nint _windowHandle;
    private readonly int _hotkeyId;
    private readonly nuint _subclassId;
    private readonly SubclassProc _callback;
    private bool _registered;
    private bool _disposed;

    public GlobalHotkeyRegistration(nint windowHandle)
    {
        if (windowHandle == 0) throw new ArgumentException("A valid window handle is required.", nameof(windowHandle));
        _windowHandle = windowHandle;
        _hotkeyId = Interlocked.Increment(ref s_nextId);
        _subclassId = (nuint)_hotkeyId;
        _callback = WindowProc;
        if (!SetWindowSubclass(_windowHandle, _callback, _subclassId, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not attach the hotkey window handler.");
    }

    public event EventHandler? Pressed;
    public int LastError { get; private set; }
    public bool IsRegistered => _registered;

    public bool TryRegister(HotkeyGesture gesture)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Unregister();
        LastError = 0;
        if (!gesture.IsValid) return false;
        _registered = RegisterHotKey(_windowHandle, _hotkeyId,
            (uint)gesture.Modifiers | ModNoRepeat, gesture.VirtualKey);
        if (!_registered) LastError = Marshal.GetLastWin32Error();
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered) return;
        UnregisterHotKey(_windowHandle, _hotkeyId);
        _registered = false;
    }

    private nint WindowProc(nint window, uint message, nint wParam, nint lParam, nuint subclassId, nuint data)
    {
        if (message == WmHotkey && wParam == _hotkeyId) Pressed?.Invoke(this, EventArgs.Empty);
        return DefSubclassProc(window, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unregister();
        RemoveWindowSubclass(_windowHandle, _callback, _subclassId);
    }

    private delegate nint SubclassProc(nint window, uint message, nint wParam, nint lParam,
        nuint subclassId, nuint referenceData);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint window, int id);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint window, SubclassProc callback,
        nuint subclassId, nuint referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint window, SubclassProc callback, nuint subclassId);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint window, uint message, nint wParam, nint lParam);
}
