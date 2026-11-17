using ScreenshotTool.Desktop;
using ScreenshotTool.Workflow;

namespace ScreenshotTool.Settings;

public sealed record AppSettings(Hotkey CaptureHotkey, CompletionAction CompletionAction, ImageFormat ImageFormat,
    string? LastSaveDirectory, bool StartWithWindows)
{
    public static AppSettings Default => new(new(3, 83), CompletionAction.CopyAndPin, ImageFormat.Png, null, false);
}
