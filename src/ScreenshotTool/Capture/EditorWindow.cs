using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ScreenshotTool.Desktop;
using ScreenshotTool.Editing;

namespace ScreenshotTool.Capture;

public sealed class EditorWindow : Window
{
    private readonly EditorView _editor;
    private readonly TextBlock _status;
    private readonly PixelPoint _sourceOrigin;
    private readonly string _defaultStatus;
    private bool _busy;
    private PixelRect _initialWindowBounds;

    public EditorWindow(CaptureDocument document, bool isPinEdit, PixelPoint sourceOrigin)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _sourceOrigin = sourceOrigin;
        _defaultStatus = isPinEdit ? "更新贴图保留原有标注；导出草稿后可继续编辑。" : "标注完成后点击“完成”，也可直接保存图片。";
        Title = isPinEdit ? "编辑贴图 · ScreenshotTool" : "编辑截图 · ScreenshotTool";
        FontFamily = new FontFamily("Microsoft YaHei UI");
        Background = new SolidColorBrush(Color.FromRgb(17, 24, 39));
        Foreground = Brushes.White;
        WindowStartupLocation = WindowStartupLocation.Manual;
        ResizeMode = ResizeMode.CanResize;
        Topmost = true;
        SetInitialBounds();

        var layout = new DockPanel();
        _status = new TextBlock
        {
            Text = _defaultStatus, Foreground = new SolidColorBrush(Color.FromRgb(170, 183, 202)),
            FontSize = 11, Padding = new Thickness(16, 7, 16, 9), TextWrapping = TextWrapping.Wrap
        };
        DockPanel.SetDock(_status, Dock.Bottom);
        layout.Children.Add(_status);
        _editor = new EditorView(document, isPinEdit);
        layout.Children.Add(_editor);
        Content = layout;

        _editor.ConfirmRequested += (_, _) => { if (!_busy) ConfirmRequested?.Invoke(this, EventArgs.Empty); };
        _editor.SaveRequested += (_, _) => { if (!_busy) SaveRequested?.Invoke(this, EventArgs.Empty); };
        _editor.CancelRequested += (_, _) =>
        {
            if (_busy) return;
            try { DialogResult = false; }
            catch (InvalidOperationException) { Close(); }
        };
        _editor.ReselectRequested += (_, _) => Reselect();
        Loaded += (_, _) =>
        {
            using var dpi = NativeDpiScope.Enter();
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowPos(handle, 0, _initialWindowBounds.X, _initialWindowBounds.Y,
                _initialWindowBounds.Width, _initialWindowBounds.Height, 0x0014);
            Activate();
        };
    }

    public CaptureDocument Document { get; }
    public event EventHandler? ConfirmRequested;
    public event EventHandler? SaveRequested;

    public void SetStatus(string message, bool isError = false)
    {
        _status.Text = string.IsNullOrWhiteSpace(message) ? _defaultStatus : message;
        _status.Foreground = new SolidColorBrush(isError ? Color.FromRgb(255, 156, 156) : Color.FromRgb(170, 183, 202));
    }

    public void SetBusy(bool busy)
    {
        _busy = busy;
        _editor.IsEnabled = !busy;
        Cursor = busy ? Cursors.Wait : Cursors.Arrow;
        if (busy) SetStatus("正在处理，请稍候…");
        else if (_status.Text == "正在处理，请稍候…") SetStatus(string.Empty);
    }

    private void SetInitialBounds()
    {
        var monitors = new MonitorService();
        var pointer = monitors.Pointer;
        var monitor = monitors.At(pointer);
        System.Drawing.Rectangle work;
        using (NativeDpiScope.Enter())
            work = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(pointer.X, pointer.Y)).WorkingArea;
        var availableWidth = work.Width / monitor.ScaleX;
        var availableHeight = work.Height / monitor.ScaleY;
        MinWidth = Math.Min(640, availableWidth);
        MinHeight = Math.Min(420, availableHeight);
        Width = Math.Min(availableWidth, Math.Max(820, Document.Selection.Width / monitor.ScaleX + 48));
        Height = Math.Min(availableHeight, Math.Max(560, Document.Selection.Height / monitor.ScaleY + 250));
        var pixelWidth = Math.Min(work.Width, (int)Math.Round(Width * monitor.ScaleX));
        var pixelHeight = Math.Min(work.Height, (int)Math.Round(Height * monitor.ScaleY));
        _initialWindowBounds = new PixelRect(work.Left + (work.Width - pixelWidth) / 2, work.Top + (work.Height - pixelHeight) / 2, pixelWidth, pixelHeight);
        Left = _initialWindowBounds.X / monitor.ScaleX;
        Top = _initialWindowBounds.Y / monitor.ScaleY;
    }

    private void Reselect()
    {
        if (_busy) return;
        var handle = new WindowInteropHelper(this).Handle;
        var selection = Document.Selection;
        var global = new PixelRect(selection.X + _sourceOrigin.X, selection.Y + _sourceOrigin.Y, selection.Width, selection.Height);
        var action = CaptureSelectionAction.Edit;
        var changed = false;
        try
        {
            // Hiding a modal WPF Window through Hide() ends its ShowDialog loop.
            // Hide only its native surface so this editor session remains modal.
            ShowWindow(handle, 0);
            var overlay = new CaptureOverlay(new ScreenSnapshot(Document.Source, _sourceOrigin), global) { Owner = this };
            if (overlay.ShowDialog() == true && overlay.SelectedRegion is PixelRect region)
            {
                Document.SetSelection(new PixelRect(region.X - _sourceOrigin.X, region.Y - _sourceOrigin.Y, region.Width, region.Height));
                action = overlay.RequestedAction;
                changed = true;
            }
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or ArgumentException)
        {
            SetStatus($"调整选区失败：{error.Message}", true);
        }
        finally
        {
            ShowWindow(handle, 5);
            Activate();
        }
        if (changed && action == CaptureSelectionAction.Complete) ConfirmRequested?.Invoke(this, EventArgs.Empty);
        if (changed && action == CaptureSelectionAction.Save) SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
}
