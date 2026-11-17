using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotTool.Capture;
using ScreenshotTool.Editing;
using Xunit;

namespace ScreenshotTool.Tests;

public sealed class CaptureDocumentTests
{
    [Fact]
    public void UndoRedo_RestoresModifiedText() => Sta.Run(() =>
    {
        var document = new CaptureDocument(PixelFixture.Solid(60, 40, Colors.White), new PixelRect(0, 0, 60, 40));
        var text = new Annotation { Kind = AnnotationKind.Text, Bounds = new Rect(3, 4, 40, 25), Text = "原文" };
        document.AddAnnotation(text);
        document.ReplaceAnnotation(text.Id, text with { Text = "修改" });

        document.Undo();
        Assert.Equal("原文", Assert.Single(document.Annotations).Text);
        document.Redo();
        Assert.Equal("修改", Assert.Single(document.Annotations).Text);
        Assert.Equal(text.Id, document.Annotations[0].Id);
    });

    [Fact]
    public void MovingAndRemoving_PreservesGeometryAcrossUndo() => Sta.Run(() =>
    {
        var document = new CaptureDocument(PixelFixture.Solid(60, 40, Colors.White), new PixelRect(0, 0, 60, 40));
        var pen = new Annotation { Kind = AnnotationKind.Pen, Points = new[] { new Point(2, 3), new Point(9, 10) } };
        document.AddAnnotation(pen);
        document.ReplaceAnnotation(pen.Id, document.Annotations[0].Translate(10, 5));
        Assert.Equal(new Point(12, 8), document.Annotations[0].Points[0]);
        document.RemoveAnnotation(pen.Id);
        Assert.Empty(document.Annotations);
        document.Undo();
        Assert.Equal(new Point(12, 8), Assert.Single(document.Annotations).Points[0]);
        document.Undo();
        Assert.Equal(new Point(2, 3), Assert.Single(document.Annotations).Points[0]);
    });

    [Fact]
    public void Clone_EditDoesNotChangeOriginalOrItsHistory() => Sta.Run(() =>
    {
        var document = new CaptureDocument(PixelFixture.Solid(60, 40, Colors.White), new PixelRect(0, 0, 60, 40));
        var text = new Annotation { Kind = AnnotationKind.Text, Bounds = new Rect(2, 3, 40, 25), Text = "原文" };
        document.AddAnnotation(text);
        var draft = document.Clone();
        draft.ReplaceAnnotation(text.Id, draft.Annotations[0] with { Text = "修改" });
        draft.SetSelection(new PixelRect(1, 2, 20, 15));

        Assert.Equal("原文", document.Annotations[0].Text);
        Assert.Equal(text.Id, draft.Annotations[0].Id);
        Assert.Equal(new PixelRect(0, 0, 60, 40), document.Selection);
        document.Undo();
        Assert.Empty(document.Annotations);
        Assert.Equal("修改", draft.Annotations[0].Text);
    });

    [Fact]
    public void AddingPen_CopiesCallerOwnedPoints() => Sta.Run(() =>
    {
        var points = new[] { new Point(2, 3), new Point(9, 10) };
        var document = new CaptureDocument(PixelFixture.Solid(20, 20, Colors.White), new PixelRect(0, 0, 20, 20));
        document.AddAnnotation(new Annotation { Kind = AnnotationKind.Pen, Points = points });
        points[0] = new Point(99, 99);
        Assert.Equal(new Point(2, 3), document.Annotations[0].Points[0]);
    });

    [Fact]
    public void ChangingCrop_DoesNotScaleAnnotation_AndNewEditClearsRedo() => Sta.Run(() =>
    {
        var document = new CaptureDocument(PixelFixture.Solid(60, 40, Colors.White), new PixelRect(0, 0, 60, 40));
        var rectangle = new Annotation { Kind = AnnotationKind.Rectangle, Bounds = new Rect(10, 5, 20, 12) };
        document.AddAnnotation(rectangle);
        document.SetSelection(new PixelRect(10, 10, 20, 20));
        Assert.Equal(new Rect(10, 5, 20, 12), document.Annotations[0].Bounds);
        document.Undo();
        Assert.True(document.CanRedo);
        document.RemoveAnnotation(rectangle.Id);
        Assert.False(document.CanRedo);
    });

    [Fact]
    public void InvalidSelection_IsRejectedWithoutChangingHistory() => Sta.Run(() =>
    {
        var document = new CaptureDocument(PixelFixture.Solid(20, 20, Colors.White), new PixelRect(0, 0, 20, 20));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.SetSelection(new PixelRect(-1, 0, 10, 10)));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.SetSelection(new PixelRect(1, 1, 20, 20)));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.SetSelection(new PixelRect(0, 0, 0, 10)));
        Assert.False(document.CanUndo);
        Assert.Equal(new PixelRect(0, 0, 20, 20), document.Selection);
    });

    [Fact]
    public void DocumentSnapshot_DoesNotTrackLaterChangesToMutableSource() => Sta.Run(() =>
    {
        var source = new WriteableBitmap(PixelFixture.Solid(4, 4, Colors.White));
        var document = new CaptureDocument(source, new PixelRect(0, 0, 4, 4));
        source.WritePixels(new Int32Rect(0, 0, 1, 1), new byte[] { 0, 0, 255, 255 }, 4, 0);
        Assert.Equal(Colors.White, PixelFixture.At(document.Source, 0, 0));
        Assert.True(document.Source.IsFrozen);
    });
}

internal static class PixelFixture
{
    public static BitmapSource Solid(int width, int height, Color color) => Create(width, height, (_, _) => color);

    public static BitmapSource Create(int width, int height, Func<int, int, Color> colorAt, double dpi = 96)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var color = colorAt(x, y);
            var offset = (y * width + x) * 4;
            pixels[offset] = color.B;
            pixels[offset + 1] = color.G;
            pixels[offset + 2] = color.R;
            pixels[offset + 3] = color.A;
        }
        var image = BitmapSource.Create(width, height, dpi, dpi, PixelFormats.Bgra32, null, pixels, width * 4);
        image.Freeze();
        return image;
    }

    public static Color At(BitmapSource image, int x, int y)
    {
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var pixel = new byte[4];
        converted.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }
}
