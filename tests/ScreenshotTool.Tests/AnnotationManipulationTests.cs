using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotTool.Capture;
using ScreenshotTool.Editing;

namespace ScreenshotTool.Tests;

public sealed class AnnotationManipulationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArrowEndpointDrag_FixesOppositeEnd_AndCommitsOneUndoStep(bool moveEnd) => Sta.Run(() =>
    {
        var document = Document();
        var arrow = Arrow(new Point(120, 80), new Point(220, 180));
        document.AddAnnotation(arrow);
        var start = moveEnd ? new Point(225, 185) : new Point(125, 85);
        var drag = new AnnotationDrag(document, document.Annotations[0], start, moveEnd ? AnnotationHandleKind.ArrowEnd : AnnotationHandleKind.ArrowStart);
        drag.Update(start + new Vector(10, 10));
        drag.Update(start + new Vector(30, -20));
        Assert.Equal(new Point(120, 80), document.Annotations[0].Points[0]);
        Assert.Equal(new Point(220, 180), document.Annotations[0].Points[1]);
        Assert.Equal(moveEnd ? new Point(120, 80) : new Point(150, 60), drag.Preview.Points[0]);
        Assert.Equal(moveEnd ? new Point(250, 160) : new Point(220, 180), drag.Preview.Points[1]);
        drag.Commit();
        drag.Commit();
        Assert.Equal(arrow.Id, document.Annotations[0].Id);
        document.Undo();
        Assert.Equal(new Point(120, 80), Assert.Single(document.Annotations).Points[0]);
        Assert.Equal(new Point(220, 180), document.Annotations[0].Points[1]);
        document.Redo();
        Assert.Equal(moveEnd ? new Point(250, 160) : new Point(220, 180), document.Annotations[0].Points[1]);
    });

    [Theory]
    [InlineData(AnnotationKind.Rectangle, (int)AnnotationHandleKind.TopLeft, 30, 40, 135, 125, 110, 100, 25, 25)]
    [InlineData(AnnotationKind.Rectangle, (int)AnnotationHandleKind.Right, 110, 70, 10, 70, 10, 40, 20, 60)]
    [InlineData(AnnotationKind.Ellipse, (int)AnnotationHandleKind.BottomRight, 110, 100, 20, 30, 20, 30, 10, 10)]
    [InlineData(AnnotationKind.Ellipse, (int)AnnotationHandleKind.Top, 70, 40, 70, 120, 30, 100, 80, 20)]
    public void FrameResize_NormalizesAfterCrossingTheFixedOppositeEdge(AnnotationKind kind, int handle,
        double startX, double startY, double endX, double endY, double x, double y, double width, double height) => Sta.Run(() =>
    {
        var document = Document();
        var annotation = new Annotation { Kind = kind, Bounds = new Rect(30, 40, 80, 60), StrokeWidth = 5 };
        document.AddAnnotation(annotation);
        var drag = new AnnotationDrag(document, annotation, new Point(startX, startY), (AnnotationHandleKind)handle);
        drag.Update(new Point(endX, endY));
        drag.Commit();
        Assert.Equal(new Rect(x, y, width, height), document.Annotations[0].Bounds);
        Assert.Equal(5, document.Annotations[0].StrokeWidth);
        document.Undo();
        Assert.Equal(new Rect(30, 40, 80, 60), Assert.Single(document.Annotations).Bounds);
    });

    [Fact]
    public void Handles_RemainEasyToHitWhenZoomedOut_AndChooseNearestOverlap()
    {
        var arrow = Arrow(new Point(40, 50), new Point(44, 50));
        Assert.Equal(AnnotationHandleKind.ArrowEnd, AnnotationHandles.HitTest(arrow, new Point(43.5, 50), 1));
        Assert.Equal(AnnotationHandleKind.ArrowEnd, AnnotationHandles.HitTest(arrow, new Point(124, 50), 0.1));
        Assert.Null(AnnotationHandles.HitTest(arrow, new Point(144, 50), 0.1));
        Assert.Equal(50, AnnotationHandles.DisplayRadius(0.1));
        var frame = new Annotation { Kind = AnnotationKind.Rectangle, Bounds = new Rect(10, 20, 4, 4) };
        Assert.Equal(8, AnnotationHandles.GetHandles(frame).Count);
        Assert.Equal(AnnotationHandleKind.BottomRight, AnnotationHandles.HitTest(frame, new Point(13.8, 23.8), 1));
    }

    [Fact]
    public void ArrowEndpointChange_IsSavedEvenWhenLegacyPointsKeepTheSameBounds() => Sta.Run(() =>
    {
        var document = Document();
        var arrow = Arrow(new Point(20, 20), new Point(60, 60)) with
        {
            Points = new[] { new Point(20, 20), new Point(0, 0), new Point(80, 80), new Point(60, 60) }
        };
        document.AddAnnotation(arrow);
        var drag = new AnnotationDrag(document, arrow, new Point(20, 20), AnnotationHandleKind.ArrowStart);
        drag.Update(new Point(25, 30));
        Assert.Equal(arrow.GetBounds(), drag.Preview.GetBounds());
        drag.Commit();
        Assert.Equal(new Point(25, 30), document.Annotations[0].Points[0]);
    });

    [Fact]
    public void DragBackToOriginal_DoesNotAddAnUndoStep() => Sta.Run(() =>
    {
        var document = Document();
        var arrow = Arrow(new Point(30, 40), new Point(100, 120)) with { Bounds = new Rect() };
        document.AddAnnotation(arrow);
        var drag = new AnnotationDrag(document, arrow, new Point(30, 40), AnnotationHandleKind.ArrowStart);
        drag.Update(new Point(20, 10));
        drag.Update(new Point(30, 40));
        drag.Commit();
        document.Undo();
        Assert.Empty(document.Annotations);
    });

    [Theory]
    [InlineData(false, AnnotationKind.Arrow)]
    [InlineData(true, AnnotationKind.Arrow)]
    [InlineData(false, AnnotationKind.Rectangle)]
    [InlineData(true, AnnotationKind.Ellipse)]
    public void EditorPointerRoute_UsesSelectionCoordinates_AndFinalReleasePosition(bool isPinEdit, AnnotationKind kind) => Sta.Run(() =>
    {
        var original = Document();
        var annotation = kind == AnnotationKind.Arrow ? Arrow(new Point(120, 80), new Point(220, 180))
            : new Annotation { Kind = kind, Bounds = new Rect(120, 80, 100, 100) };
        original.AddAnnotation(annotation);
        var document = isPinEdit ? original.Clone() : original;
        var view = Layout(new EditorView(document, isPinEdit));
        Assert.True(view.BeginPointer(new Point(70, 80)));
        view.EndPointer(new Point(70, 80));
        Assert.True(view.BeginPointer(new Point(20, 30)));
        view.EndPointer(new Point(50, 10));
        var changed = document.Annotations[0];
        if (kind == AnnotationKind.Arrow)
        {
            Assert.Equal(new Point(150, 60), changed.Points[0]);
            Assert.Equal(new Point(220, 180), changed.Points[1]);
        }
        else Assert.Equal(new Rect(150, 60, 70, 120), changed.Bounds);
        if (isPinEdit) Assert.Equal(new Rect(120, 80, 100, 100), original.Annotations[0].GetBounds());
        document.Undo();
        Assert.Equal(new Rect(120, 80, 100, 100), Assert.Single(document.Annotations).GetBounds());
    });

    [Fact]
    public void EditorPointerRoute_PreservesBodyMoveAndContinuousArrowDrawing() => Sta.Run(() =>
    {
        var document = Document();
        document.AddAnnotation(Arrow(new Point(120, 80), new Point(220, 180)));
        var view = Layout(new EditorView(document));
        view.BeginPointer(new Point(70, 80));
        view.EndPointer(new Point(90, 100));
        Assert.Equal(new Point(140, 100), document.Annotations[0].Points[0]);
        Assert.Equal(new Point(240, 200), document.Annotations[0].Points[1]);
        var arrowTool = Visuals<ToggleButton>(view).Single(button => Equals(button.Content, "箭头"));
        arrowTool.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        view.BeginPointer(new Point(30, 30));
        view.EndPointer(new Point(80, 40));
        view.BeginPointer(new Point(30, 70));
        view.EndPointer(new Point(80, 90));
        Assert.Equal(3, document.Annotations.Count);
        Assert.True(arrowTool.IsChecked);
    });

    [Fact]
    public void EditorHandles_AppearOnSelectedArrow_AndStayOutOfExport() => Sta.Run(() =>
    {
        var document = new CaptureDocument(PixelFixture.Solid(200, 120, Colors.White), new PixelRect(0, 0, 200, 120));
        document.AddAnnotation(Arrow(new Point(40, 40), new Point(140, 60)));
        var exportedBefore = DocumentRenderer.Render(document);
        var view = Layout(new EditorView(document));
        view.BeginPointer(new Point(90, 50));
        view.EndPointer(new Point(90, 50));
        view.UpdateLayout();
        var viewport = Visuals<Canvas>(view).Single();
        var editorImage = new RenderTargetBitmap(200, 120, 96, 96, PixelFormats.Pbgra32);
        editorImage.Render(viewport);
        var controlPoint = PixelFixture.At(editorImage, 40, 45);
        Assert.True(controlPoint.B > controlPoint.R, $"Expected blue handle outline, got {controlPoint}.");
        Assert.Equal(Colors.White, PixelFixture.At(DocumentRenderer.Render(document), 40, 45));
        Assert.Equal(Pixels(exportedBefore), Pixels(DocumentRenderer.Render(document)));
    });

    [Fact]
    public void EditorHandles_UseActualViewboxScaleForDrawingAndHitTargets() => Sta.Run(() =>
    {
        var document = new CaptureDocument(PixelFixture.Solid(2000, 1200, Colors.White), new PixelRect(500, 200, 1000, 700));
        document.AddAnnotation(Arrow(new Point(700, 400), new Point(1200, 700)));
        var view = new EditorView(document);
        view.Measure(new Size(600, 480));
        view.Arrange(new Rect(0, 0, 600, 480));
        view.UpdateLayout();
        var viewport = Visuals<Canvas>(view).Single();
        var transform = viewport.TransformToAncestor(view);
        var scale = (transform.Transform(new Point(1, 0)) - transform.Transform(new Point())).Length;
        Assert.InRange(scale, 0.01, 0.5);
        view.BeginPointer(new Point(450, 350));
        view.EndPointer(new Point(450, 350));
        view.UpdateLayout();
        var image = new RenderTargetBitmap(1000, 700, 96, 96, PixelFormats.Pbgra32);
        image.Render(viewport);
        var ring = PixelFixture.At(image, 200, 200 + (int)Math.Round(5 / scale));
        Assert.True(ring.B > ring.R, $"Expected blue handle at scale {scale}, got {ring}.");
        var grab = new Point(200 + 6 / scale, 200);
        view.BeginPointer(grab);
        view.EndPointer(grab + new Vector(40, 20));
        Assert.Equal(new Point(740, 420), document.Annotations[0].Points[0]);
        Assert.Equal(new Point(1200, 700), document.Annotations[0].Points[1]);
    });

    private static CaptureDocument Document() => new(PixelFixture.Solid(500, 400, Colors.White), new PixelRect(100, 50, 300, 300));
    private static Annotation Arrow(Point start, Point end) => new() { Kind = AnnotationKind.Arrow, Bounds = new Rect(start, end), Points = new[] { start, end } };
    private static EditorView Layout(EditorView view)
    {
        view.Measure(new Size(900, 700));
        view.Arrange(new Rect(0, 0, 900, 700));
        view.UpdateLayout();
        return view;
    }
    private static IEnumerable<T> Visuals<T>(DependencyObject element) where T : DependencyObject
    {
        if (element is T match) yield return match;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
            foreach (var child in Visuals<T>(VisualTreeHelper.GetChild(element, index))) yield return child;
    }
    private static byte[] Pixels(BitmapSource image)
    {
        var pixels = new byte[image.PixelWidth * image.PixelHeight * 4];
        image.CopyPixels(pixels, image.PixelWidth * 4, 0);
        return pixels;
    }
}
