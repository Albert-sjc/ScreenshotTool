using System;
using System.IO;
using System.Linq;
using ScreenshotTool.Desktop;
using ScreenshotTool.Settings;
using ScreenshotTool.Workflow;
using Xunit;

namespace ScreenshotTool.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void MissingSettings_UsesDefaultsAndRegistersTheCaptureHotkey()
    {
        using var folder = new SettingsFolder();
        var registrar = new Registrar();
        var settings = new SettingsService(folder.File, registrar);
        var loaded = settings.Load();
        Assert.Equal(new Hotkey(3, 83), loaded.CaptureHotkey);
        Assert.Equal(CompletionAction.CopyAndPin, loaded.CompletionAction);
        Assert.Equal(ImageFormat.Png, loaded.ImageFormat);
        Assert.False(loaded.StartWithWindows);
        Assert.Null(loaded.LastSaveDirectory);
        Assert.Equal(new Hotkey(3, 83), registrar.Active);
    }

    [Fact]
    public void Reload_PreservesSuccessfulChangesIncludingSaveDirectory()
    {
        using var folder = new SettingsFolder();
        var settings = new SettingsService(folder.File, new Registrar());
        settings.Load();
        var candidate = settings.Current with { CaptureHotkey = new(6, 81), CompletionAction = CompletionAction.PinOnly,
            ImageFormat = ImageFormat.Jpeg, LastSaveDirectory = folder.Path, StartWithWindows = true };
        Assert.True(settings.TryApply(candidate).Success);
        Assert.Equal(candidate, new SettingsService(folder.File, new Registrar()).Load());
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(folder.File));
        Assert.Equal(5, json.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public void OccupiedCandidate_PreservesOldHotkeyAndFile()
    {
        using var folder = new SettingsFolder();
        var registrar = new Registrar { Unavailable = new(3, 81) };
        var settings = new SettingsService(folder.File, registrar);
        settings.Load();
        Assert.True(settings.TryApply(settings.Current).Success);
        var oldFile = File.ReadAllText(folder.File);
        Assert.False(settings.TryApply(settings.Current with { CaptureHotkey = new(3, 81) }).Success);
        Assert.Equal(new Hotkey(3, 83), settings.Current.CaptureHotkey);
        Assert.Equal(new Hotkey(3, 83), registrar.Active);
        Assert.Equal(oldFile, File.ReadAllText(folder.File));
    }

    [Fact]
    public void DiskWriteFailure_RollsBackHotkeyAndCurrentSettings()
    {
        using var folder = new SettingsFolder();
        var registrar = new Registrar();
        var settings = new SettingsService(folder.File, registrar);
        settings.Load();
        Directory.CreateDirectory(folder.File);
        Assert.False(settings.TryApply(settings.Current with { CaptureHotkey = new(6, 81) }).Success);
        Assert.Equal(new Hotkey(3, 83), settings.Current.CaptureHotkey);
        Assert.Equal(new Hotkey(3, 83), registrar.Active);
    }

    [Fact]
    public void MalformedSettings_RecoversToDefaultsWithVisibleDiagnostic()
    {
        using var folder = new SettingsFolder();
        File.WriteAllText(folder.File, "{invalid");
        var settings = new SettingsService(folder.File, new Registrar());
        Assert.Equal(AppSettings.Default, settings.Load());
        Assert.False(string.IsNullOrWhiteSpace(settings.LastError));
    }

    [Fact]
    public void InvalidPersistedHotkey_RecoversToUsableDefaults()
    {
        using var folder = new SettingsFolder();
        File.WriteAllText(folder.File, "{\"CaptureHotkey\":{\"Modifiers\":0,\"VirtualKey\":0}}");
        var settings = new SettingsService(folder.File, new Registrar());
        Assert.Equal(AppSettings.Default, settings.Load());
        Assert.NotNull(settings.LastError);
    }

    private sealed class Registrar : IHotkeyRegistrar
    {
        public Hotkey? Unavailable { get; init; }
        public Hotkey? Active { get; private set; }
        public RegistrationResult TryRegister(Hotkey hotkey)
        {
            if (hotkey == Unavailable) return new(false, "快捷键已被占用。");
            Active = hotkey;
            return new(true, null);
        }
    }

    private sealed class SettingsFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ScreenshotTool.Tests", Guid.NewGuid().ToString("N"));
        public string File => System.IO.Path.Combine(Path, "settings.json");
        public SettingsFolder() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}
