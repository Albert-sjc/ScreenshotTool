using ScreenshotTool.Capture;
using Xunit;

namespace ScreenshotTool.Tests;

public class SelectionModelTests
{
    [Theory]
    [InlineData(3584, 1280, 1456, 120, 1776, 280)]
    [InlineData(2986.666666666667, 1066.666666666667, 1213.333333333333, 100, 1480, 233.333333333333)]
    [InlineData(2240, 800, 910, 75, 1110, 175)]
    public void DisplayAt125150200Percent_MapsCrossSeamSelectionToSamePhysicalPixels(
        double displayWidth, double displayHeight, double x1, double y1, double x2, double y2)
    {
        var bounds = new PixelRect(-1920, -200, 4480, 1600);
        var start = SelectionModel.FromDisplay(x1, y1, bounds, displayWidth, displayHeight);
        var end = SelectionModel.FromDisplay(x2, y2, bounds, displayWidth, displayHeight);
        Assert.Equal(new PixelRect(-100, -50, 400, 200), SelectionModel.FromDrag(start, end));
    }

    [Fact]
    public void NegativeOrigin_MapsToSnapshotPixels()
    {
        var result = SelectionModel.ToSnapshot(new PixelRect(-1800, 100, 400, 200), new PixelPoint(-1920, 0));
        Assert.Equal(new PixelRect(120, 100, 400, 200), result);
    }

    [Theory]
    [InlineData(300, 200, 100, 50, 100, 50, 200, 150)]
    [InlineData(-160, -80, 240, 120, -160, -80, 400, 200)]
    [InlineData(42, 17, 42, 17, 42, 17, 0, 0)]
    public void Drag_NormalizesPhysicalBounds(int x1, int y1, int x2, int y2, int x, int y, int width, int height)
    {
        Assert.Equal(new PixelRect(x, y, width, height), SelectionModel.FromDrag(new(x1, y1), new(x2, y2)));
    }

    [Fact]
    public void Intersect_ClipsASelectionAcrossTheSnapshotEdge()
    {
        Assert.Equal(new PixelRect(0, 10, 30, 20), new PixelRect(-10, 10, 40, 20).Intersect(new(0, 0, 100, 100)));
        Assert.True(new PixelRect(150, 150, 10, 10).Intersect(new(0, 0, 100, 100)).IsEmpty);
    }
}
