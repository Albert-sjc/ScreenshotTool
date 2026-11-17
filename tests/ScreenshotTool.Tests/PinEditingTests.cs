using System;
using ScreenshotTool.Capture;
using ScreenshotTool.Pins;
using ScreenshotTool.Editing;
using Xunit;

namespace ScreenshotTool.Tests;

public sealed class PinEditingTests
{
    [Fact]
    public void NegativeDesktopOrigin_SurvivesPinCreationAndRepeatedEdits() => Sta.Run(() =>
    {
        var original = ScreenshotWorkflowTests.NewDocument();
        var document = new CaptureDocument(original.Source, original.Selection, new PixelPoint(-1920, -200));
        var manager = new PinManager();
        var id = manager.Create(document);
        Assert.Equal(new PixelPoint(-1920, -200), manager.Get(id).SourceOrigin);
        var edit = manager.BeginEdit(id);
        Assert.Equal(new PixelPoint(-1920, -200), edit.Draft.SourceOrigin);
        manager.Commit(edit);
        Assert.Equal(new PixelPoint(-1920, -200), manager.BeginEdit(id).Draft.SourceOrigin);
    });

    [Fact]
    public void Commit_UpdatesSamePin_AndCancelDiscardsDraft() => Sta.Run(() =>
    {
        var manager = new PinManager();
        var id = manager.Create(ScreenshotWorkflowTests.NewDocument());
        var cancelled = manager.BeginEdit(id);
        cancelled.Draft.SetSelection(new PixelRect(1, 1, 2, 2));
        manager.Cancel(cancelled);
        Assert.Equal(new PixelRect(0, 0, 4, 4), manager.Get(id).Selection);
        var accepted = manager.BeginEdit(id);
        accepted.Draft.SetSelection(new PixelRect(1, 1, 2, 2));
        manager.Commit(accepted);
        Assert.Equal(1, manager.Count);
        Assert.Equal(new PixelRect(1, 1, 2, 2), manager.Get(id).Selection);
        Assert.Throws<InvalidOperationException>(() => manager.Commit(accepted));
    });

    [Fact]
    public void HideScope_RestoresVisibilityWithoutRecreatingClosedPins() => Sta.Run(() =>
    {
        var manager = new PinManager();
        var first = manager.Create(ScreenshotWorkflowTests.NewDocument());
        var second = manager.Create(ScreenshotWorkflowTests.NewDocument());
        using (manager.HideForCapture())
        {
            Assert.False(manager.IsVisible(first));
            manager.Close(second);
        }
        Assert.True(manager.IsVisible(first));
        Assert.Equal(1, manager.Count);
    });

    [Fact]
    public void CaptureFailure_FinallyRestoresExistingPins() => Sta.Run(() =>
    {
        var manager = new PinManager();
        var id = manager.Create(ScreenshotWorkflowTests.NewDocument());
        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using var hidden = manager.HideForCapture();
            throw new InvalidOperationException("捕获失败");
        }));
        Assert.True(manager.IsVisible(id));
    });
}
