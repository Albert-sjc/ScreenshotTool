using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ScreenshotTool.Capture;
using ScreenshotTool.Desktop;

namespace ScreenshotTool.Tests;

[Collection("NativeDesktop")]
public sealed class CaptureWindowVisibilityTests
{
    [Fact]
    public void HideAndFlush_CaptureContainsTheWindowBehindTheHiddenWindow() => Sta.Run(() =>
    {
        var area = SystemParameters.WorkArea;
        var pointer = new MonitorService().Pointer;
        var left = pointer.X < area.Left + area.Width / 2 ? area.Right - 180 : area.Left + 20;
        var top = area.Top + 20;
        var background = FixtureWindow(Brushes.Blue, left, top);
        var foreground = FixtureWindow(Brushes.Red, left, top);
        foreground.WindowStyle = WindowStyle.SingleBorderWindow;
        foreground.ShowInTaskbar = true;
        try
        {
            background.Show();
            foreground.Show();
            foreground.UpdateLayout();
            var sample = foreground.PointToScreen(new Point(50, 50));
            var point = new PixelPoint((int)sample.X, (int)sample.Y);
            var handle = new WindowInteropHelper(foreground).Handle;
            WaitForPixel(point, 0, 0, 255);

            CaptureWindowVisibility.Hide(foreground);
            CaptureWindowVisibility.Flush();

            Assert.False(foreground.IsVisible);
            AssertPixel(new ScreenCaptureService().Capture(), point, 255, 0, 0);
            Assert.True(background.IsVisible);
            foreground.Show();
            Assert.True(foreground.IsVisible);
            Assert.Equal(handle, new WindowInteropHelper(foreground).Handle);
        }
        finally
        {
            CloseWithoutTransition(foreground);
            CloseWithoutTransition(background);
        }
    });

    [Fact]
    public void Hide_NullOrUnshownWindowDoesNotCreateANativeWindow() => Sta.Run(() =>
    {
        var window = new Window();
        try
        {
            CaptureWindowVisibility.Hide(null);
            CaptureWindowVisibility.Hide(window);

            Assert.False(window.IsVisible);
            Assert.Equal(0, new WindowInteropHelper(window).Handle);
        }
        finally { window.Close(); }
    });

    private static Window FixtureWindow(Brush background, double left, double top) => new()
    {
        Width = 140,
        Height = 110,
        Left = left,
        Top = top,
        WindowStartupLocation = WindowStartupLocation.Manual,
        WindowStyle = WindowStyle.None,
        ResizeMode = ResizeMode.NoResize,
        Background = background,
        ShowActivated = false,
        ShowInTaskbar = false,
        Topmost = true
    };

    private static void WaitForPixel(PixelPoint point, byte blue, byte green, byte red)
    {
        // Only setup polls: the assertion after Hide/Flush captures immediately,
        // so a lingering DWM transition cannot pass by waiting itself out.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        do
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            Marshal.ThrowExceptionForHR(DwmFlush());
            var pixel = Pixel(new ScreenCaptureService().Capture(), point);
            if (pixel[0] == blue && pixel[1] == green && pixel[2] == red) return;
            Thread.Sleep(10);
        } while (DateTime.UtcNow < deadline);
        AssertPixel(new ScreenCaptureService().Capture(), point, blue, green, red);
    }

    private static void AssertPixel(ScreenSnapshot snapshot, PixelPoint point, byte blue, byte green, byte red)
    {
        var pixel = Pixel(snapshot, point);
        Assert.Equal(new byte[] { blue, green, red }, pixel[..3]);
    }

    private static byte[] Pixel(ScreenSnapshot snapshot, PixelPoint point)
    {
        var pixel = new byte[4];
        snapshot.Pixels.CopyPixels(new Int32Rect(point.X - snapshot.Origin.X, point.Y - snapshot.Origin.Y, 1, 1), pixel, 4, 0);
        return pixel;
    }

    private static void CloseWithoutTransition(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != 0)
        {
            var disabled = 1;
            _ = DwmSetWindowAttribute(handle, 3, ref disabled, sizeof(int));
        }
        window.Close();
    }

    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
