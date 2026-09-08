using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Mediance.Windows.Windowing;

public sealed record TrayMenuLabels(string Show, string Hide, string Settings, string Exit);

public sealed class SystemTrayIcon : IDisposable
{
    private const uint WmApp = 0x8000;
    private const uint CallbackMessage = WmApp + 0x4D;
    private const uint WmNull = 0x0000;
    private const uint WmContextMenu = 0x007B;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmRButtonUp = 0x0205;
    private const uint NinSelect = 0x0400;
    private const uint NinKeySelect = 0x0401;
    private const uint NimAdd = 0;
    private const uint NimDelete = 2;
    private const uint NimSetVersion = 4;
    private const uint NifMessage = 1;
    private const uint NifIcon = 2;
    private const uint NifTip = 4;
    private const uint NotifyIconVersion4 = 4;
    private const uint MfString = 0;
    private const uint MfSeparator = 0x0800;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCommand = 0x0100;
    private const uint TpmNoNotify = 0x0080;
    private const uint ShowCommand = 1;
    private const uint SettingsCommand = 2;
    private const uint ExitCommand = 3;
    private const uint IconId = 1;
    private static long s_nextSubclassId = 0x54524159;

    private readonly nint _window;
    private readonly string _tooltip;
    private readonly TrayMenuLabels _labels;
    private readonly Func<bool> _isWindowVisible;
    private readonly nuint _subclassId;
    private readonly SubclassProc _callback;
    private readonly uint _taskbarCreated;
    private readonly nint _icon;
    private readonly bool _ownsIcon;
    private bool _added;
    private bool _disposed;

    public SystemTrayIcon(nint window, string tooltip, TrayMenuLabels labels, Func<bool> isWindowVisible, string? iconPath = null)
    {
        if (window == 0) throw new ArgumentException("A valid window handle is required.", nameof(window));
        _window = window;
        _tooltip = tooltip;
        _labels = labels;
        _isWindowVisible = isWindowVisible;
        _subclassId = (nuint)Interlocked.Increment(ref s_nextSubclassId);
        _callback = WindowProc;
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        if (_taskbarCreated == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the Explorer restart message.");
        _icon = !string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath)
            ? LoadImage(0, iconPath, 1, 0, 0, 0x0010)
            : 0;
        _ownsIcon = _icon != 0;
        if (_icon == 0) _icon = LoadIcon(0, (nint)32512);
        if (_icon == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not load the tray icon.");
        if (!SetWindowSubclass(_window, _callback, _subclassId, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not attach the tray window handler.");
        try { AddIcon(); }
        catch
        {
            RemoveWindowSubclass(_window, _callback, _subclassId);
            throw;
        }
    }

    public event EventHandler? ToggleRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;
    public bool IsAdded => _added;

    private void AddIcon()
    {
        var data = CreateData(NifMessage | NifIcon | NifTip);
        if (!Shell_NotifyIcon(NimAdd, ref data))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not add the tray icon.");
        _added = true;
        var version = CreateData(0);
        version.VersionOrTimeout = NotifyIconVersion4;
        Shell_NotifyIcon(NimSetVersion, ref version);
    }

    private NotifyIconData CreateData(uint flags) => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(),
        Window = _window,
        Id = IconId,
        Flags = flags,
        CallbackMessage = CallbackMessage,
        Icon = _icon,
        Tip = _tooltip.Length > 127 ? _tooltip[..127] : _tooltip,
        Info = "",
        InfoTitle = ""
    };

    private nint WindowProc(nint window, uint message, nint wParam, nint lParam, nuint subclassId, nuint data)
    {
        if (message == _taskbarCreated && !_disposed)
        {
            _added = false;
            try { AddIcon(); } catch { }
        }
        else if (message == CallbackMessage && !_disposed)
        {
            var notification = (uint)((long)lParam & 0xFFFF);
            if (notification is WmLButtonUp or NinSelect or NinKeySelect)
                ToggleRequested?.Invoke(this, EventArgs.Empty);
            else if (notification is WmContextMenu or WmRButtonUp)
                ShowMenu(wParam);
        }
        return DefSubclassProc(window, message, wParam, lParam);
    }

    private void ShowMenu(nint packedPosition)
    {
        var menu = CreatePopupMenu();
        if (menu == 0) return;
        try
        {
            var first = _isWindowVisible() ? _labels.Hide : _labels.Show;
            if (!AppendMenu(menu, MfString, ShowCommand, first) ||
                !AppendMenu(menu, MfString, SettingsCommand, _labels.Settings) ||
                !AppendMenu(menu, MfSeparator, 0, "") ||
                !AppendMenu(menu, MfString, ExitCommand, _labels.Exit)) return;
            var packed = unchecked((long)packedPosition);
            var x = (int)(short)(packed & 0xFFFF);
            var y = (int)(short)((packed >> 16) & 0xFFFF);
            if (GetCursorPos(out var cursor)) { x = cursor.X; y = cursor.Y; }
            SetForegroundWindow(_window);
            var command = TrackPopupMenuEx(menu, TpmRightButton | TpmReturnCommand | TpmNoNotify,
                x, y, _window, 0);
            if (command == ShowCommand) ToggleRequested?.Invoke(this, EventArgs.Empty);
            else if (command == SettingsCommand) SettingsRequested?.Invoke(this, EventArgs.Empty);
            else if (command == ExitCommand) ExitRequested?.Invoke(this, EventArgs.Empty);
            PostMessage(_window, WmNull, 0, 0);
        }
        finally { DestroyMenu(menu); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_added)
        {
            var data = CreateData(0);
            Shell_NotifyIcon(NimDelete, ref data);
            _added = false;
        }
        RemoveWindowSubclass(_window, _callback, _subclassId);
        if (_ownsIcon) DestroyIcon(_icon);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public nint Window;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint VersionOrTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid ItemGuid;
        public nint BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }

    private delegate nint SubclassProc(nint window, uint message, nint wParam, nint lParam,
        nuint subclassId, nuint referenceData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadIcon(nint instance, nint iconName);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadImage(nint instance, string name, uint type, int width, int height, uint load);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string value);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(nint menu, uint flags, nuint item, string text);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint window, nint parameters);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);
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
