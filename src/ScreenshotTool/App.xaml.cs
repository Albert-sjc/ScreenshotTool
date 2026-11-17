using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using ScreenshotTool.Capture;
using ScreenshotTool.Desktop;
using ScreenshotTool.Editing;
using ScreenshotTool.Pins;
using ScreenshotTool.Settings;
using ScreenshotTool.Workflow;

namespace ScreenshotTool;

public partial class App : Application
{
    private const string IpcTitle = "ScreenshotTool.Instance.Window";
    private const int ShutdownMessage = 0x8000 + 37;
    private readonly PinManager _pins = new();
    private readonly Dictionary<Guid, PinnedWindow> _pinWindows = new();
    private readonly ImageOutputs _outputs = new();
    private SingleInstance? _instance;
    private HwndSource? _ipc;
    private HotkeyService? _hotkey;
    private TrayController? _tray;
    private SettingsService _settings = null!;
    private ScreenshotWorkflow _workflow = null!;
    private CaptureCoordinator _capture = null!;
    private SettingsWindow? _settingsWindow;
    private Window? _activeEditor;
    private Window? _pinListWindow;
    private ListBox? _pinList;
    private bool _syncingPins;
    private bool _quitting;
    private bool _startupEnabled;
    private string _dataDirectory = "";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--shutdown", StringComparer.OrdinalIgnoreCase))
        {
            RequestExistingShutdown();
            Shutdown();
            return;
        }
        _instance = new SingleInstance();
        if (!_instance.TryAcquire())
        {
            _instance.SignalExisting();
            Shutdown();
            return;
        }
        _instance.ExistingInstanceSignaled += (_, _) => Dispatcher.BeginInvoke(new Action(ShowSettings));
        _ipc = new HwndSource(new HwndSourceParameters(IpcTitle) { Width = 0, Height = 0, WindowStyle = unchecked((int)0x80000000) });
        _ipc.AddHook(IpcHook);
        _dataDirectory = ResolveDataDirectory(e.Args);
        Directory.CreateDirectory(_dataDirectory);
        var settingsPath = Path.Combine(_dataDirectory, "settings.json");
        var firstRun = !File.Exists(settingsPath);
        _hotkey = new HotkeyService();
        _settings = new SettingsService(settingsPath, _hotkey);
        _settings.Load();
        _startupEnabled = _settings.Current.StartWithWindows;
        _workflow = new ScreenshotWorkflow(_outputs, _pins);
        _capture = new CaptureCoordinator(new ScreenCaptureService(), _pins, CaptureWindowVisibility.Flush);
        _pins.Changed += PinsChanged;
        _tray = new TrayController(StartCapture, ShowSettings, ShowPinList, Quit);
        _hotkey.Pressed += (_, _) =>
        {
            if (_settingsWindow?.IsRecordingHotkey == true) return;
            StartCapture();
        };
        DispatcherUnhandledException += (_, args) =>
        {
            ReportError("操作未能完成", args.Exception);
            args.Handled = true;
        };
        if (_settings.LastError is { } error) _tray.Notify(error);
        else if (firstRun) _settings.TryApply(_settings.Current);
        if (e.Args.Contains("--capture", StringComparer.OrdinalIgnoreCase)) Dispatcher.BeginInvoke(new Action(StartCapture));
        else if (firstRun || e.Args.Contains("--settings", StringComparer.OrdinalIgnoreCase)) ShowSettings();
    }

    private async void StartCapture()
    {
        if (_quitting || _capture is null || _capture.IsEditing) return;
        try
        {
            if (!_capture.Start()) return;
            CaptureWindowVisibility.Hide(_settingsWindow);
            CaptureWindowVisibility.Hide(_pinListWindow);
            await Dispatcher.Yield(DispatcherPriority.Render);
            var snapshot = _capture.Capture();
            var overlay = new CaptureOverlay(snapshot);
            _activeEditor = overlay;
            if (overlay.ShowDialog() != true || overlay.SelectedRegion is not { } global) return;
            var document = new CaptureDocument(snapshot.Pixels, SelectionModel.ToSnapshot(global, snapshot.Origin), snapshot.Origin);
            if (overlay.RequestedAction == CaptureSelectionAction.Complete)
            {
                var result = await _workflow.CompleteNewAsync(document, _settings.Current.CompletionAction);
                if (result.ShouldClose) return;
                _tray?.Notify(result.Error ?? "请重试。");
            }
            else if (overlay.RequestedAction == CaptureSelectionAction.Save)
            {
                var saved = await SaveDocument(document, null, null);
                if (saved?.ShouldClose == true) return;
            }
            ShowEditor(document, null);
        }
        catch (Exception error) { ReportError("无法开始截图", error); }
        finally
        {
            _activeEditor = null;
            _capture.Cancel();
        }
    }

    private void ShowEditor(CaptureDocument document, PinEditSession? edit)
    {
        var window = new EditorWindow(document, edit is not null, document.SourceOrigin);
        _activeEditor = window;
        window.ConfirmRequested += async (_, _) =>
        {
            window.SetBusy(true);
            try
            {
                if (edit is not null)
                {
                    _pins.Commit(edit);
                    window.DialogResult = true;
                }
                else
                {
                    var result = await _workflow.CompleteNewAsync(document, _settings.Current.CompletionAction);
                    if (result.ShouldClose) window.DialogResult = true;
                    else window.SetStatus(result.Error ?? "操作失败，请重试。", true);
                }
            }
            catch (Exception error) { window.SetStatus(error.Message, true); }
            finally { if (window.IsVisible) window.SetBusy(false); }
        };
        window.SaveRequested += async (_, _) =>
        {
            window.SetBusy(true);
            try
            {
                var result = await SaveDocument(document, edit, window);
                if (result is null) return;
                if (result.ShouldClose) window.DialogResult = true;
                else window.SetStatus(result.Error ?? "已导出草稿，可以继续编辑。", result.Error is not null);
            }
            catch (Exception error) { window.SetStatus(error.Message, true); }
            finally { if (window.IsVisible) window.SetBusy(false); }
        };
        window.ShowDialog();
        if (edit is { IsActive: true }) _pins.Cancel(edit);
    }

    private async Task<WorkflowResult?> SaveDocument(CaptureDocument document, PinEditSession? edit, Window? owner)
    {
        var choice = ChooseSavePath(owner);
        if (choice is null) return null;
        var (path, format) = choice.Value;
        var result = edit is null ? await _workflow.SaveNewAsync(document, path, format)
            : await _workflow.SavePinDraftAsync(edit, path, format);
        if (result.Error is null) RememberDirectory(path);
        else if (owner is null) _tray?.Notify(result.Error);
        return result;
    }

    private (string Path, ImageFormat Format)? ChooseSavePath(Window? owner)
    {
        var format = _settings.Current.ImageFormat;
        var dialog = new SaveFileDialog
        {
            Title = "保存截图", Filter = "PNG 图片 (*.png)|*.png|JPEG 图片 (*.jpg;*.jpeg)|*.jpg;*.jpeg",
            FilterIndex = format == ImageFormat.Jpeg ? 2 : 1,
            FileName = "截图_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"),
            DefaultExt = format == ImageFormat.Jpeg ? ".jpg" : ".png",
            AddExtension = true, OverwritePrompt = true,
        };
        if (_settings.Current.LastSaveDirectory is { } folder && Directory.Exists(folder)) dialog.InitialDirectory = folder;
        if ((owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) != true) return null;
        var extension = Path.GetExtension(dialog.FileName);
        format = extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            ? ImageFormat.Jpeg : ImageFormat.Png;
        return (dialog.FileName, format);
    }

    private void RememberDirectory(string path)
    {
        var result = _settings.TryApply(_settings.Current with { LastSaveDirectory = Path.GetDirectoryName(Path.GetFullPath(path)) });
        if (!result.Success) _tray?.Notify("图片已保存，但无法记住保存目录：" + result.Error);
    }

    private void PinsChanged(object? sender, EventArgs e) => SyncPinWindows();
    private void SyncPinWindows()
    {
        if (_syncingPins || _quitting) return;
        _syncingPins = true;
        try
        {
            var records = _pins.Pins;
            foreach (var id in _pinWindows.Keys.Where(id => records.All(pin => pin.Id != id)).ToArray())
            {
                var obsolete = _pinWindows[id];
                _pinWindows.Remove(id);
                obsolete.Close();
            }
            foreach (var pin in records)
            {
                if (!_pinWindows.TryGetValue(pin.Id, out var window))
                {
                    var id = pin.Id;
                    window = new PinnedWindow(id, pin.Document);
                    window.Closed += (_, _) => { _pinWindows.Remove(id); _pins.Close(id); };
                    window.EditRequested += (_, _) => EditPin(id);
                    window.CopyRequested += async (_, _) =>
                    {
                        try
                        {
                            var copied = await _outputs.CopyAsync(DocumentRenderer.Render(_pins.Get(id)));
                            if (!copied.Success) _tray?.Notify(copied.Error ?? "复制失败。");
                        }
                        catch (Exception error) { ReportError("无法复制贴图", error); }
                    };
                    window.SaveRequested += async (_, _) => await SavePin(id);
                    _pinWindows.Add(id, window);
                }
                window.Refresh(pin.Document);
                if (pin.IsVisible && !window.IsVisible) window.Show();
                else if (!pin.IsVisible && window.IsVisible) CaptureWindowVisibility.Hide(window);
            }
            if (_pinList is not null) _pinList.ItemsSource = _pins.Pins.Select((pin, i) => new PinListItem(pin.Id, $"贴图 {i + 1} · {pin.Document.Selection.Width} × {pin.Document.Selection.Height}")).ToArray();
        }
        finally { _syncingPins = false; }
    }

    private void EditPin(Guid id)
    {
        if (_quitting || !_capture.Start(false)) return;
        PinEditSession? edit = null;
        try
        {
            edit = _pins.BeginEdit(id);
            ShowEditor(edit.Draft, edit);
        }
        catch (Exception error) { ReportError("无法编辑贴图", error); }
        finally
        {
            if (edit is { IsActive: true }) _pins.Cancel(edit);
            _activeEditor = null;
            _capture.Cancel();
        }
    }

    private async Task SavePin(Guid id)
    {
        try
        {
            _pinWindows.TryGetValue(id, out var owner);
            var choice = ChooseSavePath(owner);
            if (choice is null) return;
            var saved = await _outputs.SaveAsync(DocumentRenderer.Render(_pins.Get(id)), choice.Value.Path, choice.Value.Format);
            if (saved.Success) RememberDirectory(choice.Value.Path);
            else _tray?.Notify(saved.Error ?? "保存失败。");
        }
        catch (Exception error) { ReportError("无法保存贴图", error); }
    }

    private void ShowSettings()
    {
        if (_quitting) return;
        if (_capture?.IsEditing == true) { _activeEditor?.Activate(); return; }
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(_settings);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.SettingsChanged += (_, _) =>
            {
                try { StartupRegistration.SetEnabled(_settings.Current.StartWithWindows); _startupEnabled = _settings.Current.StartWithWindows; }
                catch (Exception error)
                {
                    _settings.TryApply(_settings.Current with { StartWithWindows = _startupEnabled });
                    ReportError("无法修改开机启动设置", error);
                }
            };
        }
        _settingsWindow.Show();
        _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
    }

    private void ShowPinList()
    {
        if (_quitting) return;
        if (_pinListWindow is not null) { _pinListWindow.Show(); _pinListWindow.Activate(); return; }
        _pinListWindow = new Window { Title = "当前贴图", Width = 380, Height = 380, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        var panel = new DockPanel { Margin = new Thickness(18) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var show = new Button { Content = "显示选中贴图", Padding = new Thickness(12, 7, 12, 7) };
        var close = new Button { Content = "关闭选中贴图", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(8, 0, 0, 0) };
        buttons.Children.Add(show); buttons.Children.Add(close); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
        var note = new TextBlock { Text = "仅显示本次运行中仍打开的贴图。", Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(note, Dock.Top); panel.Children.Add(note);
        _pinList = new ListBox { DisplayMemberPath = nameof(PinListItem.Title) };
        show.Click += (_, _) => { if (_pinList?.SelectedItem is PinListItem item && _pinWindows.TryGetValue(item.Id, out var window)) { window.Show(); window.Activate(); } };
        close.Click += (_, _) => { if (_pinList?.SelectedItem is PinListItem item) _pins.Close(item.Id); };
        panel.Children.Add(_pinList); _pinListWindow.Content = panel;
        _pinListWindow.Closed += (_, _) => { _pinListWindow = null; _pinList = null; };
        SyncPinWindows();
        _pinListWindow.Show();
    }

    private void ReportError(string title, Exception error)
    {
        try { File.AppendAllText(Path.Combine(_dataDirectory, "errors.log"), DateTimeOffset.Now + " " + title + Environment.NewLine + error + Environment.NewLine); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        if (_tray is not null) _tray.Notify(title + "：" + error.Message);
        else MessageBox.Show(title + "：" + error.Message, "ScreenshotTool", MessageBoxButton.OK, MessageBoxImage.Error);
    }
    private void Quit() { if (_quitting) return; _quitting = true; Shutdown(); }
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e) { e.Cancel = false; Quit(); base.OnSessionEnding(e); }
    protected override void OnExit(ExitEventArgs e)
    {
        _quitting = true;
        _pins.Changed -= PinsChanged;
        _capture?.Cancel();
        _tray?.Dispose();
        _hotkey?.Dispose();
        _ipc?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }
    private IntPtr IpcHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == ShutdownMessage || message == 0x0010) { handled = true; Dispatcher.BeginInvoke(new Action(Quit)); }
        return IntPtr.Zero;
    }
    private static string ResolveDataDirectory(string[] args)
    {
        var index = Array.IndexOf(args, "--data-dir");
        return index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1])
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenshotTool");
    }
    private static void RequestExistingShutdown()
    {
        var window = FindWindow(null, IpcTitle);
        if (window == IntPtr.Zero) return;
        GetWindowThreadProcessId(window, out var processId);
        PostMessage(window, ShutdownMessage, IntPtr.Zero, IntPtr.Zero);
        try { using var process = Process.GetProcessById((int)processId); process.WaitForExit(10000); }
        catch (ArgumentException) { }
    }
    private sealed record PinListItem(Guid Id, string Title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string title);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
}
