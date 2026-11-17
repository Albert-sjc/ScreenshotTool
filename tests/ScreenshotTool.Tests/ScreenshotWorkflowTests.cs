using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotTool.Capture;
using ScreenshotTool.Editing;
using ScreenshotTool.Pins;
using ScreenshotTool.Workflow;
using Xunit;

namespace ScreenshotTool.Tests;

public sealed class ScreenshotWorkflowTests
{
    [Theory]
    [InlineData(CompletionAction.CopyAndPin, 1, 1)]
    [InlineData(CompletionAction.CopyOnly, 1, 0)]
    [InlineData(CompletionAction.PinOnly, 0, 1)]
    public void Complete_PerformsOnlyConfiguredOutputs(CompletionAction action, int copies, int pins) => Sta.Run(() =>
    {
        var output = new RecordingOutputs();
        var manager = new PinManager();
        var workflow = new ScreenshotWorkflow(output, manager);
        var result = workflow.CompleteNewAsync(NewDocument(), action).GetAwaiter().GetResult();
        Assert.True(result.ShouldClose);
        Assert.Equal(copies, output.Copies.Count);
        Assert.Equal(pins, manager.Count);
        Assert.Empty(output.Saves);
    });

    [Fact]
    public void SaveNew_OnlySavesAndEndsCapture() => Sta.Run(() =>
    {
        var output = new RecordingOutputs();
        var pins = new PinManager();
        var workflow = new ScreenshotWorkflow(output, pins);
        var result = workflow.SaveNewAsync(NewDocument(), "picture.png", ImageFormat.Png).GetAwaiter().GetResult();
        Assert.True(result.ShouldClose);
        Assert.Single(output.Saves);
        Assert.Empty(output.Copies);
        Assert.Equal(0, pins.Count);
    });

    [Fact]
    public void ClipboardFailure_RetryDoesNotLoseEditingOrDuplicatePin() => Sta.Run(() =>
    {
        var output = new RecordingOutputs { CopyFails = true };
        var pins = new PinManager();
        var workflow = new ScreenshotWorkflow(output, pins);
        var document = NewDocument();
        var failed = workflow.CompleteNewAsync(document, CompletionAction.CopyAndPin).GetAwaiter().GetResult();
        Assert.False(failed.ShouldClose);
        Assert.NotNull(failed.Error);
        Assert.Equal(0, pins.Count);
        output.CopyFails = false;
        var success = workflow.CompleteNewAsync(document, CompletionAction.CopyAndPin).GetAwaiter().GetResult();
        var duplicate = workflow.CompleteNewAsync(document, CompletionAction.CopyAndPin).GetAwaiter().GetResult();
        Assert.True(success.ShouldClose);
        Assert.Equal(success.CreatedPinId, duplicate.CreatedPinId);
        Assert.Single(output.Copies);
        Assert.Equal(1, pins.Count);
    });

    [Fact]
    public void FailedSave_LeavesDocumentAvailableForRetry() => Sta.Run(() =>
    {
        var output = new RecordingOutputs { SaveFails = true };
        var pins = new PinManager();
        var workflow = new ScreenshotWorkflow(output, pins);
        var document = NewDocument();
        Assert.False(workflow.SaveNewAsync(document, "picture.png", ImageFormat.Png).GetAwaiter().GetResult().ShouldClose);
        output.SaveFails = false;
        Assert.True(workflow.SaveNewAsync(document, "picture.png", ImageFormat.Png).GetAwaiter().GetResult().ShouldClose);
        Assert.Single(output.Saves);
        Assert.Empty(output.Copies);
        Assert.Equal(0, pins.Count);
    });

    [Fact]
    public void ExportPinDraft_DoesNotCommitCloseOrCopy() => Sta.Run(() =>
    {
        var output = new RecordingOutputs();
        var pins = new PinManager();
        var id = pins.Create(NewDocument());
        var edit = pins.BeginEdit(id);
        edit.Draft.SetSelection(new PixelRect(1, 1, 2, 2));
        var workflow = new ScreenshotWorkflow(output, pins);
        var result = workflow.SavePinDraftAsync(edit, "draft.png", ImageFormat.Png).GetAwaiter().GetResult();
        Assert.False(result.ShouldClose);
        Assert.Null(result.Error);
        Assert.Equal(new PixelRect(0, 0, 4, 4), pins.Get(id).Selection);
        Assert.Single(output.Saves);
        Assert.Empty(output.Copies);
        pins.Cancel(edit);
        Assert.Equal(new PixelRect(0, 0, 4, 4), pins.Get(id).Selection);
        Assert.Single(output.Saves);
    });

    internal static CaptureDocument NewDocument()
    {
        var bytes = new byte[4 * 4 * 4];
        for (var i = 0; i < bytes.Length; i += 4) { bytes[i] = 20; bytes[i + 1] = 70; bytes[i + 2] = 140; bytes[i + 3] = 255; }
        var image = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgra32, null, bytes, 16);
        image.Freeze();
        return new CaptureDocument(image, new PixelRect(0, 0, 4, 4));
    }

    private sealed class RecordingOutputs : IImageOutputs
    {
        public bool CopyFails { get; set; }
        public bool SaveFails { get; set; }
        public List<BitmapSource> Copies { get; } = new();
        public List<string> Saves { get; } = new();
        public Task<OutputResult> CopyAsync(BitmapSource image)
        {
            if (CopyFails) return Task.FromResult(OutputResult.Fail("剪贴板被占用"));
            Copies.Add(image);
            return Task.FromResult(OutputResult.Ok());
        }
        public Task<OutputResult> SaveAsync(BitmapSource image, string path, ImageFormat format)
        {
            if (SaveFails) return Task.FromResult(OutputResult.Fail("无法保存"));
            Saves.Add(path);
            return Task.FromResult(OutputResult.Ok());
        }
    }
}
