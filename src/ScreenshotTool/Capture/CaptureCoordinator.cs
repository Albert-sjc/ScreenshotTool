using System;
using ScreenshotTool.Desktop;
using ScreenshotTool.Pins;

namespace ScreenshotTool.Capture;

public sealed class CaptureCoordinator
{
    private readonly IScreenCapture _capture;
    private readonly PinManager _pins;
    private readonly Action _prepareDesktop;
    private IDisposable? _hiddenPins;
    private ScreenSnapshot? _snapshot;
    public CaptureCoordinator(IScreenCapture capture, PinManager pins, Action? prepareDesktop = null)
    {
        _capture = capture;
        _pins = pins;
        _prepareDesktop = prepareDesktop ?? (() => { });
    }
    public bool IsEditing { get; private set; }
    public bool Start(bool hidePins = true)
    {
        if (IsEditing) return false;
        try
        {
            IsEditing = true;
            if (hidePins) _hiddenPins = _pins.HideForCapture();
            return true;
        }
        catch { Cancel(); throw; }
    }
    public ScreenSnapshot Capture()
    {
        if (!IsEditing) throw new InvalidOperationException("请先开始截图。");
        try
        {
            if (_snapshot is null)
            {
                _prepareDesktop();
                _snapshot = _capture.Capture();
            }
            return _snapshot;
        }
        catch { Cancel(); throw; }
    }
    public void Cancel()
    {
        _snapshot = null;
        _hiddenPins?.Dispose();
        _hiddenPins = null;
        IsEditing = false;
    }
}
