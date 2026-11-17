using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenshotTool.Editing;

public static class DocumentRenderer
{
    public static BitmapSource Render(CaptureDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var selection = document.Selection;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.PushClip(new RectangleGeometry(new Rect(0, 0, selection.Width, selection.Height)));
            context.PushTransform(new TranslateTransform(-selection.X, -selection.Y));
            DrawBase(context, document.Source);
            foreach (var annotation in document.Annotations) DrawAnnotation(context, document.Source, annotation);
            context.Pop();
            context.Pop();
        }
        var result = new RenderTargetBitmap(selection.Width, selection.Height, 96, 96, PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }

    internal static void DrawBase(DrawingContext context, BitmapSource source)
    {
        var bounds = new Rect(0, 0, source.PixelWidth, source.PixelHeight);
        context.DrawRectangle(Brushes.Black, null, bounds);
        context.DrawImage(source, bounds);
    }

    internal static FormattedText FormatText(Annotation annotation)
    {
        var text = new FormattedText(annotation.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Microsoft YaHei UI"), annotation.FontSize, new SolidColorBrush(annotation.Color), 1);
        text.MaxTextWidth = Math.Max(1, annotation.Bounds.Width);
        return text;
    }

    internal static void DrawAnnotation(DrawingContext context, BitmapSource source, Annotation annotation)
    {
        var brush = new SolidColorBrush(annotation.Color);
        var pen = new Pen(brush, annotation.StrokeWidth)
        {
            StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round
        };
        var bounds = annotation.Bounds;
        switch (annotation.Kind)
        {
            case AnnotationKind.Pen:
                if (annotation.Points.Count == 1)
                    context.DrawEllipse(brush, null, annotation.Points[0], annotation.StrokeWidth / 2, annotation.StrokeWidth / 2);
                else if (annotation.Points.Count > 1)
                {
                    var geometry = new StreamGeometry();
                    using (var path = geometry.Open())
                    {
                        path.BeginFigure(annotation.Points[0], false, false);
                        for (var index = 1; index < annotation.Points.Count; index++) path.LineTo(annotation.Points[index], true, false);
                    }
                    context.DrawGeometry(null, pen, geometry);
                }
                break;
            case AnnotationKind.Rectangle:
                context.DrawRectangle(null, pen, bounds);
                break;
            case AnnotationKind.Ellipse:
                context.DrawEllipse(null, pen, new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2), bounds.Width / 2, bounds.Height / 2);
                break;
            case AnnotationKind.Arrow:
                if (annotation.Points.Count < 2) break;
                var start = annotation.Points[0];
                var end = annotation.Points[annotation.Points.Count - 1];
                var direction = end - start;
                if (direction.Length < 0.01) break;
                var headLength = Math.Min(direction.Length, Math.Max(10, annotation.StrokeWidth * 4));
                direction.Normalize();
                var perpendicular = new Vector(-direction.Y, direction.X);
                context.DrawLine(pen, start, end);
                var arrow = new StreamGeometry();
                using (var path = arrow.Open())
                {
                    path.BeginFigure(end, true, true);
                    path.LineTo(end - direction * headLength + perpendicular * (headLength * 0.45), true, false);
                    path.LineTo(end - direction * headLength - perpendicular * (headLength * 0.45), true, false);
                }
                context.DrawGeometry(brush, null, arrow);
                break;
            case AnnotationKind.Text:
                if (annotation.Text.Length > 0) context.DrawText(FormatText(annotation), bounds.TopLeft);
                break;
            case AnnotationKind.Mosaic:
                DrawMosaic(context, source, annotation);
                break;
        }
    }

    private static void DrawMosaic(DrawingContext context, BitmapSource source, Annotation annotation)
    {
        var bounds = Rect.Intersect(annotation.Bounds, new Rect(0, 0, source.PixelWidth, source.PixelHeight));
        if (bounds.IsEmpty || bounds.Width < 0.01 || bounds.Height < 0.01) return;
        var left = (int)Math.Floor(bounds.Left);
        var top = (int)Math.Floor(bounds.Top);
        var width = (int)Math.Ceiling(bounds.Right) - left;
        var height = (int)Math.Ceiling(bounds.Bottom) - top;
        var stride = checked(width * 4);
        var pixels = new byte[checked(stride * height)];
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        converted.CopyPixels(new Int32Rect(left, top, width, height), pixels, stride, 0);
        var block = annotation.MosaicBlockSize;
        for (var y = 0; y < height; y += block)
        for (var x = 0; x < width; x += block)
        {
            var blockWidth = Math.Min(block, width - x);
            var blockHeight = Math.Min(block, height - y);
            long red = 0, green = 0, blue = 0;
            for (var dy = 0; dy < blockHeight; dy++)
            for (var dx = 0; dx < blockWidth; dx++)
            {
                var offset = (y + dy) * stride + (x + dx) * 4;
                var alpha = pixels[offset + 3];
                blue += pixels[offset] * alpha / 255;
                green += pixels[offset + 1] * alpha / 255;
                red += pixels[offset + 2] * alpha / 255;
            }
            var count = blockWidth * blockHeight;
            for (var dy = 0; dy < blockHeight; dy++)
            for (var dx = 0; dx < blockWidth; dx++)
            {
                var offset = (y + dy) * stride + (x + dx) * 4;
                pixels[offset] = (byte)(blue / count);
                pixels[offset + 1] = (byte)(green / count);
                pixels[offset + 2] = (byte)(red / count);
                pixels[offset + 3] = 255;
            }
        }
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        image.Freeze();
        context.PushClip(new RectangleGeometry(bounds));
        context.DrawImage(image, new Rect(left, top, width, height));
        context.Pop();
    }
}
