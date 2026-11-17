using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ScreenshotTool.Desktop;

public static class CaptureWindowVisibility
{
    public static void Hide(Window? window)
    {
        if (window is null || !window.IsVisible) return;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != 0)
        {
            // Hide() can leave a fading DWM surface visible to desktop capture.
            // Disable transitions only for this application's own window.
            var disabled = 1;
            Marshal.ThrowExceptionForHR(DwmSetWindowAttribute(handle, 3, ref disabled, sizeof(int)));
        }
        window.Hide();
    }

    public static void Flush() => Marshal.ThrowExceptionForHR(DwmFlush());

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
}
