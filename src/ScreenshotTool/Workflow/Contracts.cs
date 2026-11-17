using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ScreenshotTool.Workflow;

public enum CompletionAction { CopyOnly, PinOnly, CopyAndPin }
public enum ImageFormat { Png, Jpeg }
public sealed record OutputResult(bool Success, string? Error = null)
{
    public static OutputResult Ok() => new(true);
    public static OutputResult Fail(string error) => new(false, error);
}
public sealed record WorkflowResult(bool ShouldClose, Guid? CreatedPinId = null, string? Error = null);
public interface IImageOutputs
{
    Task<OutputResult> CopyAsync(BitmapSource image);
    Task<OutputResult> SaveAsync(BitmapSource image, string path, ImageFormat format);
}
