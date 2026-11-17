using ScreenshotTool.Capture;
using ScreenshotTool.Desktop;
using ScreenshotTool.Pins;
using Xunit;

namespace ScreenshotTool.Tests;

public sealed class CaptureCoordinatorTests
{
    [Fact]
    public void DesktopPreparation_CompletesBeforeSnapshotAndRunsOncePerSession() => Sta.Run(() =>
    {
        var prepared = false;
        var preparations = 0;
        var capture = new CaptureStub { BeforeCapture = () => Assert.True(prepared) };
        var coordinator = new CaptureCoordinator(capture, new PinManager(), () => { prepared = true; preparations++; });
        Assert.True(coordinator.Start());
        coordinator.Capture();
        coordinator.Capture();
        Assert.Equal(1, preparations);
        Assert.Equal(1, capture.Calls);
        coordinator.Cancel();
    });

    [Fact]
    public void DesktopPreparationFailure_DoesNotCaptureAndRestoresPins() => Sta.Run(() =>
    {
        var pins = new PinManager();
        var id = pins.Create(ScreenshotWorkflowTests.NewDocument());
        var capture = new CaptureStub();
        var coordinator = new CaptureCoordinator(capture, pins, () => throw new InvalidOperationException("桌面刷新失败"));
        Assert.True(coordinator.Start());
        Assert.Throws<InvalidOperationException>(() => coordinator.Capture());
        Assert.Equal(0, capture.Calls);
        Assert.True(pins.IsVisible(id));
        Assert.False(coordinator.IsEditing);
    });

    [Fact]
    public void WindowHideFailure_RestoresVisibilityAndReleasesSession() => Sta.Run(() =>
    {
        var pins = new PinManager();
        var id = pins.Create(ScreenshotWorkflowTests.NewDocument());
        pins.Changed += (_, _) => { if (!pins.IsVisible(id)) throw new InvalidOperationException("窗口隐藏失败"); };
        var capture = new CaptureStub();
        var coordinator = new CaptureCoordinator(capture, pins);
        Assert.Throws<InvalidOperationException>(() => coordinator.Start());
        Assert.True(pins.IsVisible(id));
        Assert.False(coordinator.IsEditing);
        Assert.Equal(0, capture.Calls);
    });

    [Fact]
    public void RepeatedShortcut_UsesOneEditingSessionAndSnapshot() => Sta.Run(() =>
    {
        var capture = new CaptureStub();
        var coordinator = new CaptureCoordinator(capture, new PinManager());
        Assert.True(coordinator.Start());
        Assert.False(coordinator.Start());
        Assert.Same(coordinator.Capture(), coordinator.Capture());
        Assert.Equal(1, capture.Calls);
        coordinator.Cancel();
        Assert.False(coordinator.IsEditing);
        Assert.True(coordinator.Start());
        coordinator.Cancel();
    });

    [Fact]
    public void Failure_RestoresPinsAndAllowsNextCapture() => Sta.Run(() =>
    {
        var pins = new PinManager();
        var id = pins.Create(ScreenshotWorkflowTests.NewDocument());
        var capture = new CaptureStub { Fail = true };
        var coordinator = new CaptureCoordinator(capture, pins);
        Assert.True(coordinator.Start());
        Assert.False(pins.IsVisible(id));
        Assert.Throws<InvalidOperationException>(() => coordinator.Capture());
        Assert.True(pins.IsVisible(id));
        Assert.False(coordinator.IsEditing);
        Assert.True(coordinator.Start());
        coordinator.Cancel();
    });

    private sealed class CaptureStub : IScreenCapture
    {
        public int Calls;
        public bool Fail;
        public Action? BeforeCapture;
        public ScreenSnapshot Capture()
        {
            Calls++;
            BeforeCapture?.Invoke();
            if (Fail) throw new InvalidOperationException("捕获失败");
            return new ScreenSnapshot(ScreenshotWorkflowTests.NewDocument().Source, new PixelPoint(0, 0));
        }
    }
}
