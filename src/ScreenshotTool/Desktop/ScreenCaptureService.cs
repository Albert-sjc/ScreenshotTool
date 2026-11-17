using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotTool.Capture;

namespace ScreenshotTool.Desktop;

public interface IScreenCapture
{
    ScreenSnapshot Capture();
}

public sealed class ScreenCaptureService : IScreenCapture
{
    public ScreenSnapshot Capture()
    {
        using var dpi = NativeDpiScope.Enter();
        var origin = new PixelPoint(GetSystemMetrics(76), GetSystemMetrics(77));
        var width = GetSystemMetrics(78);
        var height = GetSystemMetrics(79);
        if (width <= 0 || height <= 0) throw new InvalidOperationException("当前桌面不可用，无法截图。");
        var stride = checked(width * 4);
        var bytes = checked(stride * height);
        var screen = GetDC(0);
        if (screen == 0) throw CaptureError();
        nint memory = 0, bitmap = 0, previous = 0;
        try
        {
            memory = CreateCompatibleDC(screen);
            if (memory == 0) throw CaptureError();
            var info = new BitmapInfo { Size = (uint)Marshal.SizeOf<BitmapInfo>(), Width = width, Height = -height, Planes = 1, BitCount = 32 };
            bitmap = CreateDIBSection(screen, ref info, 0, out var pixels, 0, 0);
            if (bitmap == 0 || pixels == 0) throw CaptureError();
            previous = SelectObject(memory, bitmap);
            if (previous == 0 || previous == new nint(-1)) throw CaptureError();
            if (!BitBlt(memory, 0, 0, width, height, screen, origin.X, origin.Y, 0x40CC0020)) throw CaptureError();
            if (!GdiFlush()) throw CaptureError();
            var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, pixels, bytes, stride);
            image.Freeze();
            return new(image, origin);
        }
        finally
        {
            if (previous != 0 && previous != new nint(-1)) SelectObject(memory, previous);
            if (bitmap != 0) DeleteObject(bitmap);
            if (memory != 0) DeleteDC(memory);
            ReleaseDC(0, screen);
        }
    }

    private static Win32Exception CaptureError() => new(Marshal.GetLastWin32Error(), "屏幕捕获失败，请返回桌面后重试。");
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitCount;
        public uint Compression, SizeImage;
        public int XPelsPerMeter, YPelsPerMeter;
        public uint ColorsUsed, ColorsImportant;
    }
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint context);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateCompatibleDC(nint context);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateDIBSection(nint context, ref BitmapInfo info, uint usage, out nint pixels, nint section, uint offset);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint SelectObject(nint context, nint value);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool BitBlt(nint destination, int x, int y, int width, int height, nint source, int sourceX, int sourceY, uint operation);
    [DllImport("gdi32.dll")] private static extern bool GdiFlush();
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint context);
}
