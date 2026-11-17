using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ScreenshotTool.Editing;

internal enum AnnotationHandleKind { ArrowStart, ArrowEnd, TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left }
internal readonly record struct AnnotationHandle(AnnotationHandleKind Kind, Point Position);

internal static class AnnotationHandles
{
    public static IReadOnlyList<AnnotationHandle> GetHandles(Annotation annotation)
    {
        if (annotation.Kind == AnnotationKind.Arrow && annotation.Points.Count >= 2)
            return new[]
            {
                new AnnotationHandle(AnnotationHandleKind.ArrowStart, annotation.Points[0]),
                new AnnotationHandle(AnnotationHandleKind.ArrowEnd, annotation.Points[annotation.Points.Count - 1])
            };
        if (annotation.Kind is not (AnnotationKind.Rectangle or AnnotationKind.Ellipse)) return Array.Empty<AnnotationHandle>();
        var bounds = annotation.Bounds;
        var centerX = bounds.X + bounds.Width / 2;
        var centerY = bounds.Y + bounds.Height / 2;
        return new[]
        {
            new AnnotationHandle(AnnotationHandleKind.TopLeft, bounds.TopLeft),
            new AnnotationHandle(AnnotationHandleKind.TopRight, bounds.TopRight),
            new AnnotationHandle(AnnotationHandleKind.BottomRight, bounds.BottomRight),
            new AnnotationHandle(AnnotationHandleKind.BottomLeft, bounds.BottomLeft),
            new AnnotationHandle(AnnotationHandleKind.Top, new Point(centerX, bounds.Top)),
            new AnnotationHandle(AnnotationHandleKind.Right, new Point(bounds.Right, centerY)),
            new AnnotationHandle(AnnotationHandleKind.Bottom, new Point(centerX, bounds.Bottom)),
            new AnnotationHandle(AnnotationHandleKind.Left, new Point(bounds.Left, centerY))
        };
    }

    public static AnnotationHandleKind? HitTest(Annotation annotation, Point point, double viewportScale)
    {
        var radius = 9 / viewportScale;
        var closestDistance = radius * radius;
        AnnotationHandleKind? closest = null;
        foreach (var handle in GetHandles(annotation))
        {
            var distance = (point - handle.Position).LengthSquared;
            if (distance > closestDistance) continue;
            closest = handle.Kind;
            closestDistance = distance;
        }
        return closest;
    }

    public static double DisplayRadius(double viewportScale) => 5 / viewportScale;
}

internal sealed class AnnotationDrag
{
    private readonly CaptureDocument _document;
    private readonly Point _start;
    private readonly AnnotationHandleKind? _handle;
    private bool _committed;

    public AnnotationDrag(CaptureDocument document, Annotation original, Point start, AnnotationHandleKind? handle = null)
    {
        _document = document;
        _start = start;
        _handle = handle;
        Original = original.DeepCopy();
        Preview = Original;
    }
    public Annotation Original { get; }
    public Annotation Preview { get; private set; }

    public void Update(Point point)
    {
        if (_committed) return;
        var delta = point - _start;
        if (delta.LengthSquared == 0)
        {
            Preview = Original;
            return;
        }
        if (_handle is null)
        {
            Preview = Original.Translate(delta.X, delta.Y);
            return;
        }
        if (_handle is AnnotationHandleKind.ArrowStart or AnnotationHandleKind.ArrowEnd)
        {
            var points = Original.Points.ToArray();
            var index = _handle == AnnotationHandleKind.ArrowStart ? 0 : points.Length - 1;
            points[index] += delta;
            Preview = Original with { Points = Array.AsReadOnly(points), Bounds = new Rect(points[0], points[points.Length - 1]) };
            return;
        }
        var bounds = Original.Bounds;
        var left = bounds.Left;
        var top = bounds.Top;
        var right = bounds.Right;
        var bottom = bounds.Bottom;
        if (_handle is AnnotationHandleKind.TopLeft or AnnotationHandleKind.Left or AnnotationHandleKind.BottomLeft) left += delta.X;
        if (_handle is AnnotationHandleKind.TopRight or AnnotationHandleKind.Right or AnnotationHandleKind.BottomRight) right += delta.X;
        if (_handle is AnnotationHandleKind.TopLeft or AnnotationHandleKind.Top or AnnotationHandleKind.TopRight) top += delta.Y;
        if (_handle is AnnotationHandleKind.BottomLeft or AnnotationHandleKind.Bottom or AnnotationHandleKind.BottomRight) bottom += delta.Y;
        Preview = Original with { Bounds = new Rect(new Point(left, top), new Point(right, bottom)) };
    }

    public void Commit()
    {
        if (_committed) return;
        _committed = true;
        if (Original.Bounds != Preview.Bounds || !Original.Points.SequenceEqual(Preview.Points))
            _document.ReplaceAnnotation(Original.Id, Preview);
    }
}
