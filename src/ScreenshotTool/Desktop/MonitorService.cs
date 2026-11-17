using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using ScreenshotTool.Capture;

namespace ScreenshotTool.Desktop;

public readonly record struct MonitorInfo(PixelRect Bounds, double ScaleX, double ScaleY);

public sealed class MonitorService
{
    public PixelPoint Pointer
    {
        get
        {
            using var dpi = NativeDpiScope.Enter();
            if (!GetCursorPos(out var point)) throw new Win32Exception(Marshal.GetLastWin32Error(), "无法读取鼠标位置。");
            return new(point.X, point.Y);
        }
    }

    public MonitorInfo At(PixelPoint pointer)
    {
        using var dpiScope = NativeDpiScope.Enter();
        var monitor = MonitorFromPoint(new NativePoint { X = pointer.X, Y = pointer.Y }, 2);
        var info = new NativeMonitorInfo { Size = Marshal.SizeOf<NativeMonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) throw new Win32Exception(Marshal.GetLastWin32Error(), "无法读取显示器信息。");
        var bounds = new PixelRect(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top);

        // A hidden window on the target monitor lets PMv2 use GetDpiForWindow,
        // avoiding DPI virtualization in the older GetDpiForMonitor API.
        var probe = CreateWindowEx(0, "STATIC", "", unchecked((int)0x80000000), bounds.X + 1, bounds.Y + 1, 1, 1, 0, 0, 0, 0);
        if (probe == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "无法读取显示缩放。");
        try
        {
            var dpi = GetDpiForWindow(probe);
            if (dpi == 0) throw new Win32Exception("无法读取显示缩放。");
            return new(bounds, dpi / 96d, dpi / 96d);
        }
        finally { DestroyWindow(probe); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeMonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(NativePoint point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetMonitorInfo(nint monitor, ref NativeMonitorInfo info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowEx(uint exStyle, string className, string windowName, int style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
}

internal readonly struct NativeDpiScope : IDisposable
{
    private readonly nint previous;
    private NativeDpiScope(nint previous) => this.previous = previous;
    public static NativeDpiScope Enter()
    {
        var previous = SetThreadDpiAwarenessContext(new nint(-4));
        if (previous == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "无法启用实际像素坐标。");
        return new(previous);
    }
    public void Dispose() => SetThreadDpiAwarenessContext(previous);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetThreadDpiAwarenessContext(nint context);
}
