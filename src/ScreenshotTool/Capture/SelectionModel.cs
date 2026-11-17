using System;

namespace ScreenshotTool.Capture;

public static class SelectionModel
{
    public static PixelPoint FromDisplay(double x, double y, PixelRect bounds, double displayWidth, double displayHeight) => new(
        Math.Clamp(bounds.X + (int)Math.Round(x * bounds.Width / Math.Max(1, displayWidth)), bounds.X, bounds.Right),
        Math.Clamp(bounds.Y + (int)Math.Round(y * bounds.Height / Math.Max(1, displayHeight)), bounds.Y, bounds.Bottom));
    public static PixelRect FromDrag(PixelPoint start, PixelPoint end) => new(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
    public static PixelRect ToSnapshot(PixelRect global, PixelPoint origin) => new(global.X - origin.X, global.Y - origin.Y, global.Width, global.Height);
}
