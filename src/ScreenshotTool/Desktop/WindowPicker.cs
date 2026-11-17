using System;
using System.Runtime.InteropServices;
using ScreenshotTool.Capture;

namespace ScreenshotTool.Desktop;

public sealed class WindowPicker
{
    public PixelRect? TryPick(PixelPoint point)
    {
        using var dpiScope = NativeDpiScope.Enter();
        PixelRect? result = null;
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window) || IsIconic(window) || window == GetShellWindow()) return true;
            GetWindowThreadProcessId(window, out var processId);
            if (processId == Environment.ProcessId) return true;
            if (DwmGetWindowAttribute(window, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            if (DwmGetWindowAttribute(window, 9, out NativeRect rect, Marshal.SizeOf<NativeRect>()) != 0 && !GetWindowRect(window, out rect)) return true;
            if (rect.Right <= rect.Left || rect.Bottom <= rect.Top) return true;
            if (point.X < rect.Left || point.X >= rect.Right || point.Y < rect.Top || point.Y >= rect.Bottom) return true;
            result = new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            return false;
        }, 0);
        return result;
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    private delegate bool EnumWindowProc(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowProc callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out NativeRect rect);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, int attribute, out NativeRect value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);
}
