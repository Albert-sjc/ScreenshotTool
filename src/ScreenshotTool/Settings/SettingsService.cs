using System;
using System.IO;
using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScreenshotTool.Desktop;

namespace ScreenshotTool.Settings;

public sealed class SettingsService(string settingsPath, IHotkeyRegistrar registrar)
{
    private readonly string path = Path.GetFullPath(settingsPath);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public AppSettings Current { get; private set; } = AppSettings.Default;
    public string? LastError { get; private set; }

    public AppSettings Load()
    {
        LastError = null;
        var loaded = AppSettings.Default;
        try
        {
            if (File.Exists(path))
            {
                loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions)
                    ?? throw new JsonException("设置内容为空。");
                if (Validate(loaded) is not null) throw new JsonException("设置值无效。");
            }
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or SecurityException)
        {
            loaded = AppSettings.Default;
            LastError = "无法读取设置，已恢复默认值。请打开设置重新保存。";
        }
        Current = loaded;
        var registration = registrar.TryRegister(loaded.CaptureHotkey);
        if (!registration.Success) LastError = string.Join(" ", LastError, registration.Error).Trim();
        return Current;
    }

    public RegistrationResult TryApply(AppSettings candidate)
    {
        var invalid = Validate(candidate);
        if (invalid is not null) return new(false, invalid);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var registered = false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, candidate, JsonOptions);
                stream.Flush(flushToDisk: true);
            }
            var registration = registrar.TryRegister(candidate.CaptureHotkey);
            if (!registration.Success) return registration;
            registered = true;
            File.Move(temporary, path, overwrite: true);
            Current = candidate;
            LastError = null;
            return new(true, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException)
        {
            var message = "无法保存设置，请检查设置目录的写入权限后重试。";
            if (registered)
            {
                var rollback = registrar.TryRegister(Current.CaptureHotkey);
                if (!rollback.Success) message += " 原快捷键恢复失败，请重新设置快捷键；托盘截图仍可使用。";
            }
            LastError = message;
            return new(false, message);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException) { }
        }
    }

    private static string? Validate(AppSettings candidate)
    {
        if (!candidate.CaptureHotkey.IsValid) return "截图快捷键无效，请使用包含修饰键的组合键。";
        if (!Enum.IsDefined(candidate.CompletionAction) || !Enum.IsDefined(candidate.ImageFormat)) return "请选择有效的完成动作和图片格式。";
        return null;
    }
}
