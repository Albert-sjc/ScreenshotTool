using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotTool.Editing;
using ScreenshotTool.Pins;
using ScreenshotTool.Workflow;
using Xunit;

namespace ScreenshotTool.Tests;

public sealed class ImageOutputsTests
{
    [Theory]
    [InlineData(ImageFormat.Png)]
    [InlineData(ImageFormat.Jpeg)]
    public void SavedImage_RoundTripsAtOriginalPixelSize(ImageFormat format) => Sta.Run(() =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "ScreenshotTool-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, format == ImageFormat.Png ? "image.png" : "image.jpg");
        try
        {
            var image = DocumentRenderer.Render(ScreenshotWorkflowTests.NewDocument());
            Assert.True(new ImageOutputs(_ => { }).SaveAsync(image, path, format).GetAwaiter().GetResult().Success);
            using var file = File.OpenRead(path);
            var result = BitmapDecoder.Create(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            Assert.Equal(4, result.PixelWidth);
            Assert.Equal(4, result.PixelHeight);
            var decoded = new FormatConvertedBitmap(result, PixelFormats.Bgra32, null, 0);
            var pixel = new byte[4];
            decoded.CopyPixels(new Int32Rect(1, 1, 1, 1), pixel, 4, 0);
            var tolerance = format == ImageFormat.Png ? 0 : 6;
            Assert.InRange((int)pixel[0], 20 - tolerance, 20 + tolerance);
            Assert.InRange((int)pixel[1], 70 - tolerance, 70 + tolerance);
            Assert.InRange((int)pixel[2], 140 - tolerance, 140 + tolerance);
            Assert.Equal(255, pixel[3]);
        }
        finally { if (File.Exists(path)) File.Delete(path); Directory.Delete(folder); }
    });

    [Fact]
    public void SaveToDirectory_ReturnsFailureAndPreservesDirectory() => Sta.Run(() =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "ScreenshotTool-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var image = DocumentRenderer.Render(ScreenshotWorkflowTests.NewDocument());
            var result = new ImageOutputs(_ => { }).SaveAsync(image, folder, ImageFormat.Png).GetAwaiter().GetResult();
            Assert.False(result.Success);
            Assert.True(Directory.Exists(folder));
            Assert.Empty(Directory.GetFiles(folder));
        }
        finally { Directory.Delete(folder); }
    });

    [Fact]
    public void ExportEditedPinThenCancel_KeepsActualDraftFileAndOriginalAnnotations() => Sta.Run(() =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "ScreenshotTool-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "draft.png");
        try
        {
            var pins = new PinManager();
            var id = pins.Create(ScreenshotWorkflowTests.NewDocument());
            var edit = pins.BeginEdit(id);
            edit.Draft.AddAnnotation(new Annotation
            {
                Kind = AnnotationKind.Pen, Color = Colors.Red, StrokeWidth = 2,
                Bounds = new Rect(0, 1, 4, 2), Points = new[] { new Point(0, 2), new Point(4, 2) }
            });
            var workflow = new ScreenshotWorkflow(new ImageOutputs(_ => { }), pins);
            var exported = workflow.SavePinDraftAsync(edit, path, ImageFormat.Png).GetAwaiter().GetResult();
            Assert.Null(exported.Error);
            Assert.False(exported.ShouldClose);
            pins.Cancel(edit);
            Assert.True(File.Exists(path));
            Assert.Empty(pins.Get(id).Annotations);
            using var file = File.OpenRead(path);
            var frame = BitmapDecoder.Create(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            var decoded = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var pixel = new byte[4];
            decoded.CopyPixels(new Int32Rect(2, 2, 1, 1), pixel, 4, 0);
            Assert.Equal(new byte[] { 0, 0, 255, 255 }, pixel);
        }
        finally { if (File.Exists(path)) File.Delete(path); Directory.Delete(folder); }
    });
}
