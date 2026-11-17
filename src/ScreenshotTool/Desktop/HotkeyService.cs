using System;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Windows.Interop;

namespace ScreenshotTool.Desktop;

public readonly record struct Hotkey(int Modifiers, int VirtualKey)
{
    [JsonIgnore]
    public bool IsValid => Modifiers is > 0 and <= 15 && VirtualKey is > 0 and < 255
        && VirtualKey is not (0x10 or 0x11 or 0x12 or 0x5B or 0x5C or >= 0xA0 and <= 0xA5);
}
public sealed record RegistrationResult(bool Success, string? Error);

public interface IHotkeyRegistrar
{
    RegistrationResult TryRegister(Hotkey hotkey);
}

public sealed class HotkeyService : IHotkeyRegistrar, IDisposable
{
    private readonly HwndSource source;
    private int activeId;
    private Hotkey? active;
    private bool disposed;

    public HotkeyService()
    {
        source = new HwndSource(new HwndSourceParameters("ScreenshotTool.Hotkeys")
        {
            ParentWindow = new nint(-3), WindowStyle = 0, Width = 0, Height = 0
        });
        source.AddHook(HandleMessage);
    }

    public event EventHandler? Pressed;
    public RegistrationResult TryRegister(Hotkey hotkey)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        source.Dispatcher.VerifyAccess();
        if (!hotkey.IsValid) return new(false, "请使用有效的组合键，并至少按住 Ctrl、Alt、Shift 或 Win 中的一个键。");
        if (active == hotkey) return new(true, null);
        var candidateId = activeId == 0x5300 ? 0x5301 : 0x5300;
        if (!RegisterHotKey(source.Handle, candidateId, (uint)hotkey.Modifiers | 0x4000, (uint)hotkey.VirtualKey))
            return new(false, $"快捷键不可用，可能已被其他程序占用。请选择其他组合键。（错误 {Marshal.GetLastWin32Error()}）");
        if (active is not null) UnregisterHotKey(source.Handle, activeId);
        active = hotkey;
        activeId = candidateId;
        return new(true, null);
    }

    private nint HandleMessage(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam.ToInt32() == activeId && !disposed)
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }
        return 0;
    }

    public void Dispose()
    {
        if (disposed) return;
        source.Dispatcher.VerifyAccess();
        disposed = true;
        if (active is not null) UnregisterHotKey(source.Handle, activeId);
        source.RemoveHook(HandleMessage);
        source.Dispose();
        active = null;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint window, int id);
}
