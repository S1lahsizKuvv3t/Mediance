using System.ComponentModel;
using System.Runtime.InteropServices;
using Mediance.Core.Windowing;

namespace Mediance.Windows.Windowing;

/// <summary>Documented Win32/DWM operations scoped to one Mediance window.</summary>
public static class NativeWindowFeatures
{
    public static double DpiScale(nint window) => Math.Max(96, GetDpiForWindow(window)) / 96d;

    public static void ConfigureAppearance(nint window)
    {
        SetAttribute(window, 33, 2); // DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND.
        SetAttribute(window, 20, 1); // DWMWA_USE_IMMERSIVE_DARK_MODE.
        SetAttribute(window, 34, unchecked((int)0xFFFFFFFE)); // DWMWA_BORDER_COLOR, DWMWA_COLOR_NONE.
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
            info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top));
    }

    public static bool IsTopmost(nint window) => (GetWindowLongPtr(window, -20).ToInt64() & 0x8) != 0;

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
}

public sealed record MonitorWorkArea(string Id, PixelRect WorkArea);
