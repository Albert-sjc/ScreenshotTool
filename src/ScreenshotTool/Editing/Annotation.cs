using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ScreenshotTool.Editing;

public enum AnnotationKind { Pen, Rectangle, Ellipse, Arrow, Text, Mosaic }

public sealed record Annotation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public AnnotationKind Kind { get; init; }
    public Rect Bounds { get; init; }
    public IReadOnlyList<Point> Points { get; init; } = Array.Empty<Point>();
    public Color Color { get; init; } = Colors.Red;
    public double StrokeWidth { get; init; } = 3;
    public double FontSize { get; init; } = 24;
    public string Text { get; init; } = string.Empty;
    public int MosaicBlockSize { get; init; } = 12;

    public Annotation Translate(double x, double y)
    {
        var bounds = Bounds;
        bounds.Offset(x, y);
        return this with
        {
            Bounds = bounds,
            Points = Array.AsReadOnly(Points.Select(point => new Point(point.X + x, point.Y + y)).ToArray())
        };
    }

    public Rect GetBounds()
    {
        if (Kind is AnnotationKind.Pen or AnnotationKind.Arrow && Points.Count > 0)
        {
            var left = Points.Min(point => point.X);
            var top = Points.Min(point => point.Y);
            return new Rect(left, top, Points.Max(point => point.X) - left, Points.Max(point => point.Y) - top);
        }
        return Bounds;
    }

    internal Annotation DeepCopy() => this with { Points = Array.AsReadOnly(Points.ToArray()) };
}
