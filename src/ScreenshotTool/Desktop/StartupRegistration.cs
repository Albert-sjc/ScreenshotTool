using System;
using Microsoft.Win32;

namespace ScreenshotTool.Desktop;

public static class StartupRegistration
{
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        if (enabled)
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("无法确定应用程序路径。");
            key.SetValue("ScreenshotTool", $"\"{executable}\"", RegistryValueKind.String);
        }
        else key.DeleteValue("ScreenshotTool", throwOnMissingValue: false);
    }
}
