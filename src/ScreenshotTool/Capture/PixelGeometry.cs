using System;
using System.Windows.Media.Imaging;

namespace ScreenshotTool.Capture;

public readonly record struct PixelPoint(int X, int Y);

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public PixelRect Intersect(PixelRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        return new(left, top, Math.Max(0, Math.Min(Right, other.Right) - left), Math.Max(0, Math.Min(Bottom, other.Bottom) - top));
    }
}

public sealed record ScreenSnapshot(BitmapSource Pixels, PixelPoint Origin)
{
    public PixelRect Bounds => new(Origin.X, Origin.Y, Pixels.PixelWidth, Pixels.PixelHeight);
}
