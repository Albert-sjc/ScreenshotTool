using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ScreenshotTool.Editing;
using ScreenshotTool.Pins;

namespace ScreenshotTool.Workflow;

public sealed class ScreenshotWorkflow
{
    private readonly IImageOutputs _outputs;
    private readonly PinManager _pins;
    private readonly SemaphoreSlim _submission = new(1, 1);
    private readonly ConditionalWeakTable<CaptureDocument, Completion> _completed = new();

    public ScreenshotWorkflow(IImageOutputs outputs, PinManager pins) { _outputs = outputs; _pins = pins; }

    public async Task<WorkflowResult> CompleteNewAsync(CaptureDocument document, CompletionAction action)
    {
        await _submission.WaitAsync();
        try
        {
            if (_completed.TryGetValue(document, out var completion)) return completion.Result;
            if (action is CompletionAction.CopyOnly or CompletionAction.CopyAndPin)
            {
                var copied = await _outputs.CopyAsync(DocumentRenderer.Render(document));
                if (!copied.Success) return new(false, Error: copied.Error ?? "复制失败，请重试。");
            }
            Guid? id = action is CompletionAction.PinOnly or CompletionAction.CopyAndPin ? _pins.Create(document) : null;
            var result = new WorkflowResult(true, id);
            _completed.Add(document, new Completion(result));
            return result;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        { return new(false, Error: error.Message); }
        finally { _submission.Release(); }
    }

    public async Task<WorkflowResult> SaveNewAsync(CaptureDocument document, string path, ImageFormat format)
    {
        await _submission.WaitAsync();
        try
        {
            if (_completed.TryGetValue(document, out var completion)) return completion.Result;
            var saved = await _outputs.SaveAsync(DocumentRenderer.Render(document), path, format);
            if (!saved.Success) return new(false, Error: saved.Error ?? "保存失败，请重试。");
            var result = new WorkflowResult(true);
            _completed.Add(document, new Completion(result));
            return result;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        { return new(false, Error: error.Message); }
        finally { _submission.Release(); }
    }

    public async Task<WorkflowResult> SavePinDraftAsync(PinEditSession edit, string path, ImageFormat format)
    {
        if (!edit.IsActive) return new(false, Error: "这次贴图编辑已经结束。");
        try
        {
            var result = await _outputs.SaveAsync(DocumentRenderer.Render(edit.Draft), path, format);
            return new(false, Error: result.Success ? null : result.Error ?? "保存失败，请重试。");
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        { return new(false, Error: error.Message); }
    }
    private sealed record Completion(WorkflowResult Result);
}
