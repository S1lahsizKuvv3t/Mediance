using System.ComponentModel;
using System.Runtime.InteropServices;
using Mediance.Core.Windowing;

namespace Mediance.Windows.Windowing;

/// <summary>Documented Win32/DWM operations scoped to one Mediance window.</summary>
public static class NativeWindowFeatures
{
    private const int ExtendedStyleIndex = -20;
    private const int WindowStyleIndex = -16;
    private const long AppWindowStyle = 0x00040000L;
    private const long ToolWindowStyle = 0x00000080L;
    private const long NoActivateStyle = 0x08000000L;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<nint, TaskbarPlacement> TaskbarCache = new();

    public static double DpiScale(nint window) => Math.Max(96, GetDpiForWindow(window)) / 96d;

    public static void ConfigureAppearance(nint window)
    {
        SetAttribute(window, 33, 2); // DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND.
        SetAttribute(window, 20, 1); // DWMWA_USE_IMMERSIVE_DARK_MODE.
        SetAttribute(window, 34, unchecked((int)0xFFFFFFFE)); // DWMWA_BORDER_COLOR, DWMWA_COLOR_NONE.
    }

    public static void ConfigureFloatingWindow(nint window, bool noActivate)
    {
        var styles = GetWindowLongPtr(window, ExtendedStyleIndex).ToInt64();
        var updated = (styles | ToolWindowStyle) & ~AppWindowStyle;
        if (noActivate) updated |= NoActivateStyle;
        else updated &= ~NoActivateStyle;

        Marshal.SetLastPInvokeError(0);
        var previous = SetWindowLongPtr(window, ExtendedStyleIndex, new nint(updated));
        if (previous == 0 && Marshal.GetLastPInvokeError() != 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        const uint refreshFlags = 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020;
        if (!SetWindowPos(window, 0, 0, 0, 0, 0, refreshFlags))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public static void ConfigurePopupWindow(nint window)
    {
        const long popupStyle = unchecked((long)0x80000000);
        const long captionStyle = 0x00C00000;
        const long thickFrameStyle = 0x00040000;
        const long systemMenuStyle = 0x00080000;
        const long minimizeBoxStyle = 0x00020000;
        const long maximizeBoxStyle = 0x00010000;
        var styles = GetWindowLongPtr(window, WindowStyleIndex).ToInt64();
        var updated = (styles | popupStyle) &
            ~(captionStyle | thickFrameStyle | systemMenuStyle | minimizeBoxStyle | maximizeBoxStyle);
        Marshal.SetLastPInvokeError(0);
        var previous = SetWindowLongPtr(window, WindowStyleIndex, new nint(updated));
        if (previous == 0 && Marshal.GetLastPInvokeError() != 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        SetAttribute(window, 33, 1); // DWMCP_DONOTROUND; the exact pill is provided by SetWindowRgn.
        SetAttribute(window, 34, unchecked((int)0xFFFFFFFE)); // No DWM border.
        const uint refreshFlags = 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020;
        if (!SetWindowPos(window, 0, 0, 0, 0, 0, refreshFlags))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private static void SetAttribute(nint window, uint attribute, int value) =>
        Marshal.ThrowExceptionForHR(DwmSetWindowAttribute(window, attribute, in value, sizeof(int)));

    public static PixelPoint PointerPosition()
    {
        if (!GetCursorPos(out var point)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new(point.X, point.Y);
    }

    public static PixelRect WorkAreaAt(PixelPoint position)
    {
        var monitor = MonitorFromPoint(new Point { X = position.X, Y = position.Y }, 2); // MONITOR_DEFAULTTONEAREST
        if (monitor == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        return ReadMonitor(monitor).WorkArea;
    }

    public static MonitorWorkArea MonitorAt(PixelPoint position)
    {
        var monitor = MonitorFromPoint(new Point { X = position.X, Y = position.Y }, 2);
        if (monitor == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        return ReadMonitor(monitor);
    }

    public static string MonitorIdAt(PixelPoint position)
    {
        var monitor = MonitorFromPoint(new Point { X = position.X, Y = position.Y }, 2);
        if (monitor == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        return ReadMonitor(monitor).Id;
    }

    public static IReadOnlyList<MonitorWorkArea> WorkAreasWithIds()
    {
        var areas = new List<MonitorWorkArea>();
        bool AddMonitor(nint monitor, nint deviceContext, ref Rect monitorRect, nint data)
        {
            areas.Add(ReadMonitor(monitor));
            return true;
        }
        MonitorEnumProc callback = AddMonitor;
        if (!EnumDisplayMonitors(0, 0, callback, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return areas;
    }

    public static IReadOnlyList<PixelRect> WorkAreas()
    {
        return WorkAreasWithIds().Select(x => x.WorkArea).ToArray();
    }

    private static MonitorWorkArea ReadMonitor(nint monitor)
    {
        var info = new MonitorInfoEx { Size = (uint)Marshal.SizeOf<MonitorInfoEx>(), DeviceName = string.Empty };
        if (!GetMonitorInfo(monitor, ref info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new(info.DeviceName, new(info.Work.Left, info.Work.Top,
            info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top),
            new(info.Monitor.Left, info.Monitor.Top,
                info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top));
    }

    public static bool IsForegroundFullscreenAt(PixelPoint position)
    {
        var foreground = GetForegroundWindow();
        if (foreground == 0 || !IsWindowVisible(foreground)) return false;
        _ = GetWindowThreadProcessId(foreground, out var processId);
        if (processId == (uint)Environment.ProcessId) return false;
        // Start/Search flyouts can own a transparent full-monitor window. They
        // are shell surfaces, not immersive games or full-screen media.
        var processName = ProcessName(processId);
        if (processName is "StartMenuExperienceHost" or "ShellExperienceHost" or "SearchHost" or "explorer") return false;
        var targetMonitor = MonitorFromPoint(new Point { X = position.X, Y = position.Y }, 2);
        var foregroundMonitor = MonitorFromWindow(foreground, 2);
        if (targetMonitor == 0 || targetMonitor != foregroundMonitor || !GetWindowRect(foreground, out var rect)) return false;
        var bounds = ReadMonitor(foregroundMonitor).Bounds;
        const int tolerance = 3;
        return rect.Left <= bounds.X + tolerance && rect.Top <= bounds.Y + tolerance &&
               rect.Right >= bounds.X + bounds.Width - tolerance &&
               rect.Bottom >= bounds.Y + bounds.Height - tolerance;
    }

    public static TaskbarPlacement? TaskbarAt(PixelPoint position, double fallbackDpiScale = 1)
    {
        var targetMonitor = MonitorFromPoint(new Point { X = position.X, Y = position.Y }, 2);
        if (targetMonitor == 0) return null;
        nint taskbar = 0;
        WindowEnumProc findTaskbar = (window, data) =>
        {
            if (MonitorFromWindow(window, 2) != targetMonitor) return true;
            var className = WindowClassName(window);
            if (className is not ("Shell_TrayWnd" or "Shell_SecondaryTrayWnd")) return true;
            taskbar = window;
            return false;
        };
        _ = EnumWindows(findTaskbar, 0);
        if (taskbar == 0 || !GetWindowRect(taskbar, out var taskbarRect))
        {
            // Start may temporarily hide/reparent the shell tray. The reserved
            // monitor edge still identifies the real taskbar band. Keep its last
            // tray anchor, rather than moving the capsule into desktop space.
            var monitor = ReadMonitor(targetMonitor);
            var b = monitor.Bounds;
            var w = monitor.WorkArea;
            PixelRect? band = null;
            if (w.Y + w.Height < b.Y + b.Height) band = new(b.X, w.Y + w.Height, b.Width, b.Y + b.Height - w.Y - w.Height);
            else if (w.Y > b.Y) band = new(b.X, b.Y, b.Width, w.Y - b.Y);
            else if (w.X > b.X) band = new(b.X, b.Y, w.X - b.X, b.Height);
            else if (w.X + w.Width < b.X + b.Width) band = new(w.X + w.Width, b.Y, b.X + b.Width - w.X - w.Width, b.Height);
            if (band is null) return null; // hidden taskbar: no reserved band
            TaskbarCache.TryGetValue(targetMonitor, out var last);
            return new(band.Value, last?.NotificationArea, fallbackDpiScale);
        }

        PixelRect? notificationArea = null;
        WindowEnumProc findNotificationArea = (child, data) =>
        {
            if (!IsWindowVisible(child) || !GetWindowRect(child, out var childRect)) return true;
            var className = WindowClassName(child);
            if (className is not ("TrayNotifyWnd" or "ClockButton")) return true;
            var candidate = ToPixelRect(childRect);
            if (candidate.Width <= 0 || candidate.Height <= 0) return true;
            notificationArea = notificationArea is null ? candidate : Union(notificationArea.Value, candidate);
            return true;
        };
        _ = EnumChildWindows(taskbar, findNotificationArea, 0);
        var placement = new TaskbarPlacement(ToPixelRect(taskbarRect), notificationArea, DpiScale(taskbar));
        TaskbarCache[targetMonitor] = placement;
        return placement;
    }

    private static string WindowClassName(nint window)
    {
        var buffer = new char[128];
        var length = GetClassName(window, buffer, buffer.Length);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

    private static PixelRect ToPixelRect(Rect value) =>
        new(value.Left, value.Top, value.Right - value.Left, value.Bottom - value.Top);

    private static PixelRect Union(PixelRect left, PixelRect right)
    {
        var x = Math.Min(left.X, right.X);
        var y = Math.Min(left.Y, right.Y);
        var rightEdge = Math.Max(left.X + left.Width, right.X + right.Width);
        var bottomEdge = Math.Max(left.Y + left.Height, right.Y + right.Height);
        return new(x, y, rightEdge - x, bottomEdge - y);
    }

    public static void SetRoundedRegion(nint window, int width, int height, int radius)
    {
        var diameter = Math.Max(2, radius * 2);
        var region = CreateRoundRectRgn(0, 0, width + 1, height + 1, diameter, diameter);
        if (region == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (SetWindowRgn(window, region, true) == 0)
        {
            var error = Marshal.GetLastWin32Error();
            _ = DeleteObject(region);
            throw new Win32Exception(error);
        }
        // SetWindowRgn owns the region after success.
    }

    public static bool IsTopmost(nint window) => (GetWindowLongPtr(window, ExtendedStyleIndex).ToInt64() & 0x8) != 0;
    public static bool IsToolWindow(nint window) =>
        (GetWindowLongPtr(window, ExtendedStyleIndex).ToInt64() & ToolWindowStyle) != 0;
    public static bool IsNoActivateWindow(nint window) =>
        (GetWindowLongPtr(window, ExtendedStyleIndex).ToInt64() & NoActivateStyle) != 0;
    public static nint ForegroundWindow() => GetForegroundWindow();
    public static bool IsStartMenuForeground()
    {
        _ = GetWindowThreadProcessId(GetForegroundWindow(), out var processId);
        return string.Equals(ProcessName(processId), "StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ProcessName(uint processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
        catch (Win32Exception) { return null; }
    }
    public static void RestoreForegroundWindow(nint window)
    {
        if (window != 0) _ = SetForegroundWindow(window);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public uint Size; public Rect Monitor; public Rect Work; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
    }
    private delegate bool MonitorEnumProc(nint monitor, nint deviceContext, ref Rect monitorRect, nint data);
    private delegate bool WindowEnumProc(nint window, nint data);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, uint attribute, in int value, int size);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfoEx info);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(nint deviceContext, nint clipRect,
        MonitorEnumProc callback, nint data);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hwnd, int index, nint newValue);
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(WindowEnumProc callback, nint data);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(nint parent, WindowEnumProc callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint window, [Out] char[] className, int maximumCount);
    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(nint window, nint region, [MarshalAs(UnmanagedType.Bool)] bool redraw);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint value);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
}

public sealed record MonitorWorkArea(string Id, PixelRect WorkArea, PixelRect Bounds);
public sealed record TaskbarPlacement(PixelRect Bounds, PixelRect? NotificationArea, double DpiScale = 1);
