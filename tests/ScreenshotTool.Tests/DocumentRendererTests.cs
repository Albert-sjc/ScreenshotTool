using System.Windows;
using System.Windows.Media;
using ScreenshotTool.Capture;
using ScreenshotTool.Editing;
using Xunit;

namespace ScreenshotTool.Tests;

public sealed class DocumentRendererTests
{
    [Fact]
    public void Render_UsesExactSelectionPixelsRegardlessOfSourceDpi() => Sta.Run(() =>
    {
        var source = PixelFixture.Create(120, 80, (x, y) => Color.FromRgb((byte)x, (byte)y, 20), 144);
        var document = new CaptureDocument(source, new PixelRect(10, 20, 80, 40));
        var image = DocumentRenderer.Render(document);
        Assert.Equal(80, image.PixelWidth);
        Assert.Equal(40, image.PixelHeight);
        Assert.Equal(Color.FromRgb(10, 20, 20), PixelFixture.At(image, 0, 0));
        Assert.Equal(Color.FromRgb(89, 59, 20), PixelFixture.At(image, 79, 39));
        Assert.True(image.IsFrozen);
    });

    [Fact]
    public void Render_CropsAnnotationsWithoutScalingThem() => Sta.Run(() =>
    {
        var document = new CaptureDocument(PixelFixture.Solid(80, 60, Colors.White), new PixelRect(20, 10, 30, 20));
        document.AddAnnotation(new Annotation { Kind = AnnotationKind.Rectangle, Bounds = new Rect(5, 15, 60, 30), Color = Colors.Red, StrokeWidth = 4 });
        var image = DocumentRenderer.Render(document);
        Assert.Equal(Colors.Red, PixelFixture.At(image, 15, 5));
        Assert.Equal(Colors.White, PixelFixture.At(image, 15, 15));
    });

    [Fact]
    public void Render_ArrowAndEllipseAppearInExport() => Sta.Run(() =>
    {
        var document = new CaptureDocument(PixelFixture.Solid(80, 60, Colors.White), new PixelRect(0, 0, 80, 60));
        document.AddAnnotation(new Annotation { Kind = AnnotationKind.Arrow, Points = new[] { new Point(10, 10), new Point(60, 10) }, Color = Colors.Red, StrokeWidth = 4 });
        document.AddAnnotation(new Annotation { Kind = AnnotationKind.Ellipse, Bounds = new Rect(20, 25, 40, 20), Color = Colors.Blue, StrokeWidth = 4 });
        var image = DocumentRenderer.Render(document);
        Assert.Equal(Colors.Red, PixelFixture.At(image, 35, 10));
        Assert.Equal(Colors.Blue, PixelFixture.At(image, 40, 25));
        Assert.Equal(Colors.White, PixelFixture.At(image, 40, 35));
    });

    [Fact]
    public void Render_MosaicActuallyObscuresSourcePixels() => Sta.Run(() =>
    {
        var source = PixelFixture.Create(16, 16, (x, y) => (x + y) % 2 == 0 ? Colors.Black : Colors.White);
        var document = new CaptureDocument(source, new PixelRect(0, 0, 16, 16));
        document.AddAnnotation(new Annotation { Kind = AnnotationKind.Mosaic, Bounds = new Rect(4, 4, 8, 8), MosaicBlockSize = 4 });
        var image = DocumentRenderer.Render(document);
        var obscured = PixelFixture.At(image, 5, 5);
        Assert.InRange(obscured.R, (byte)126, (byte)129);
        Assert.Equal(obscured, PixelFixture.At(image, 6, 5));
        Assert.Equal(Colors.Black, PixelFixture.At(image, 0, 0));
    });

    [Fact]
    public void Render_BaseIsOpaqueEvenWhenInputHasAlpha() => Sta.Run(() =>
    {
        var document = new CaptureDocument(PixelFixture.Solid(4, 4, Color.FromArgb(0, 255, 0, 0)), new PixelRect(0, 0, 4, 4));
        var image = DocumentRenderer.Render(document);
        Assert.Equal((byte)255, PixelFixture.At(image, 1, 1).A);
    });

    [Fact]
    public void Render_PenAndMultilineChineseTextRemainVisible() => Sta.Run(() =>
    {
        var document = new CaptureDocument(PixelFixture.Solid(160, 100, Colors.White), new PixelRect(0, 0, 160, 100));
        document.AddAnnotation(new Annotation { Kind = AnnotationKind.Pen, Points = new[] { new Point(5, 5), new Point(80, 5) }, Color = Colors.Blue, StrokeWidth = 4 });
        document.AddAnnotation(new Annotation { Kind = AnnotationKind.Text, Bounds = new Rect(10, 15, 140, 80), Text = "中文\n输入", Color = Colors.Red, FontSize = 24 });
        var image = DocumentRenderer.Render(document);
        Assert.Equal(Colors.Blue, PixelFixture.At(image, 40, 5));
        var firstLineInk = 0;
        var secondLineInk = 0;
        var pixels = new byte[160 * 100 * 4];
        image.CopyPixels(pixels, 160 * 4, 0);
        for (var y = 15; y < 80; y++)
        for (var x = 10; x < 100; x++)
        {
            var offset = (y * 160 + x) * 4;
            if (pixels[offset + 2] > 180 && pixels[offset + 1] < 160 && pixels[offset] < 160)
            {
                if (y < 44) firstLineInk++;
                else secondLineInk++;
            }
        }
        Assert.True(firstLineInk > 20);
        Assert.True(secondLineInk > 20);
        Assert.Equal("中文\n输入", document.Annotations[1].Text);
    });
}
