using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotTool.Capture;
using ScreenshotTool.Desktop;
using ScreenshotTool.Editing;

namespace ScreenshotTool.Pins;

public sealed class PinnedWindow : Window
{
    private readonly Image image;
    private readonly Border toolbar;
    private readonly TextBlock zoomLabel;
    private CaptureDocument? document;
    private BitmapSource rendered;
    private double zoom = 1;
    private bool loaded;

    public Guid Id { get; }
    public event EventHandler? EditRequested;
    public event EventHandler? CopyRequested;
    public event EventHandler? SaveRequested;

    public PinnedWindow(Guid id, CaptureDocument document)
    {
        Id = id;
        this.document = document;
        rendered = DocumentRenderer.Render(document);
        Title = "截图贴图";
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Background = Brushes.White;
        FontFamily = new FontFamily("Microsoft YaHei UI");
        Width = 240;
        Height = 180;
        UseLayoutRounding = true;

        var root = new Grid();
        Content = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(70, 78, 90)), BorderThickness = new Thickness(1), Child = root };
        image = new Image { Source = rendered, Stretch = Stretch.Uniform, Cursor = Cursors.SizeAll, Focusable = true,
            ToolTip = "拖动移动 · 滚轮缩放 · 双击恢复原始大小 · 右键更多操作" };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        root.Children.Add(image);
        image.MouseLeftButtonDown += (_, e) =>
        {
            image.Focus();
            e.Handled = true;
            if (e.ClickCount == 2) { SetZoom(1, constrainToWorkArea: true); return; }
            try { DragMove(); }
            catch (InvalidOperationException) { }
        };
        image.MouseWheel += (_, e) =>
        {
            e.Handled = true;
            SetZoom(zoom * (e.Delta > 0 ? 1.1 : 1 / 1.1), constrainToWorkArea: true);
        };

        var actions = new WrapPanel { Margin = new Thickness(4) };
        AddButton(actions, "编辑", "再次编辑原有标注", () => EditRequested?.Invoke(this, EventArgs.Empty));
        AddButton(actions, "复制", "复制原始像素图片（Ctrl+C）", () => CopyRequested?.Invoke(this, EventArgs.Empty));
        AddButton(actions, "保存", "保存图片（Ctrl+S）", () => SaveRequested?.Invoke(this, EventArgs.Empty));
        AddButton(actions, "关闭", "关闭贴图（Esc）", Close);
        zoomLabel = new TextBlock { Foreground = Brushes.White, Margin = new Thickness(8, 6, 6, 4), VerticalAlignment = VerticalAlignment.Center, FontSize = 11 };
        actions.Children.Add(zoomLabel);
        toolbar = new Border { Background = new SolidColorBrush(Color.FromArgb(235, 34, 40, 51)), Child = actions,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Visibility = Visibility.Hidden, MaxWidth = 330 };
        root.Children.Add(toolbar);
        MouseEnter += (_, _) => toolbar.Visibility = Visibility.Visible;
        MouseLeave += (_, _) => { if (!IsKeyboardFocusWithin) toolbar.Visibility = Visibility.Hidden; };
        GotKeyboardFocus += (_, _) => toolbar.Visibility = Visibility.Visible;
        LostKeyboardFocus += (_, _) => { if (!IsMouseOver) toolbar.Visibility = Visibility.Hidden; };
        PreviewKeyDown += OnKeyDown;
        ContextMenu = BuildContextMenu();
        Loaded += (_, _) => InitializePlacement();
        Closed += (_, _) => { image.Source = null; this.document = null; };
    }

    public void Refresh(CaptureDocument document)
    {
        if (ReferenceEquals(this.document, document)) return;
        var next = DocumentRenderer.Render(document);
        this.document = document;
        rendered = next;
        image.Source = next;
        if (loaded) SetZoom(zoom, constrainToWorkArea: false);
    }

    private static void AddButton(Panel panel, string text, string tooltip, Action action)
    {
        var button = new Button { Content = text, ToolTip = tooltip, Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(2), MinWidth = 42 };
        button.Click += (_, _) => action();
        panel.Children.Add(button);
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();
        AddMenu(menu, "编辑原有标注", () => EditRequested?.Invoke(this, EventArgs.Empty));
        AddMenu(menu, "复制图片    Ctrl+C", () => CopyRequested?.Invoke(this, EventArgs.Empty));
        AddMenu(menu, "保存图片    Ctrl+S", () => SaveRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new Separator());
        AddMenu(menu, "恢复原始大小（适合屏幕）", () => SetZoom(1, constrainToWorkArea: true));
        AddMenu(menu, "适合屏幕", () => SetZoom(double.MaxValue, constrainToWorkArea: true));
        var opacity = new Slider { Minimum = 20, Maximum = 100, Value = 100, Width = 170, TickFrequency = 10, SmallChange = 5,
            ToolTip = "贴图不透明度；复制与保存不受影响" };
        System.Windows.Automation.AutomationProperties.SetName(opacity, "贴图不透明度");
        opacity.ValueChanged += (_, e) => Opacity = e.NewValue / 100;
        var opacityPanel = new StackPanel { Margin = new Thickness(8, 4, 8, 4) };
        opacityPanel.Children.Add(new TextBlock { Text = "不透明度", Margin = new Thickness(0, 0, 0, 5) });
        opacityPanel.Children.Add(opacity);
        menu.Items.Add(new MenuItem { Header = opacityPanel, StaysOpenOnClick = true });
        menu.Items.Add(new Separator());
        AddMenu(menu, "关闭贴图    Esc", Close);
        return menu;
    }

    private static void AddMenu(ContextMenu menu, string text, Action action)
    {
        var item = new MenuItem { Header = text };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { e.Handled = true; Close(); }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
        { e.Handled = true; CopyRequested?.Invoke(this, EventArgs.Empty); }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        { e.Handled = true; SaveRequested?.Invoke(this, EventArgs.Empty); }
    }

    private void InitializePlacement()
    {
        if (loaded) return;
        loaded = true;
        var pointer = new MonitorService().Pointer;
        var work = WorkingArea(pointer);
        zoom = Math.Min(1, Math.Min(720d / rendered.PixelWidth, 720d / rendered.PixelHeight));
        zoom = Math.Min(zoom, FitZoom(work));
        var width = Math.Max(20, (int)Math.Round(rendered.PixelWidth * zoom) + 2);
        var height = Math.Max(20, (int)Math.Round(rendered.PixelHeight * zoom) + 2);
        MovePhysical(Math.Clamp(pointer.X + 16, work.Left + 8, Math.Max(work.Left + 8, work.Right - width - 8)),
            Math.Clamp(pointer.Y + 16, work.Top + 8, Math.Max(work.Top + 8, work.Bottom - height - 8)), width, height);
        UpdateZoomLabel();
    }

    private void SetZoom(double requested, bool constrainToWorkArea)
    {
        if (!loaded) return;
        var handle = new WindowInteropHelper(this).Handle;
        using var dpi = NativeDpiScope.Enter();
        if (!GetWindowRect(handle, out var current)) return;
        var work = WorkingArea(new(current.Left, current.Top));
        zoom = constrainToWorkArea ? Math.Clamp(requested, Math.Min(0.05, FitZoom(work)), Math.Min(6, FitZoom(work))) : requested;
        var width = Math.Max(20, (int)Math.Round(rendered.PixelWidth * zoom) + 2);
        var height = Math.Max(20, (int)Math.Round(rendered.PixelHeight * zoom) + 2);
        var x = constrainToWorkArea ? Math.Clamp(current.Left, work.Left + 8, Math.Max(work.Left + 8, work.Right - width - 8)) : current.Left;
        var y = constrainToWorkArea ? Math.Clamp(current.Top, work.Top + 8, Math.Max(work.Top + 8, work.Bottom - height - 8)) : current.Top;
        MovePhysical(x, y, width, height);
        UpdateZoomLabel();
    }

    private double FitZoom(NativeRect work) => Math.Max(0.001, Math.Min((work.Right - work.Left - 20d) / rendered.PixelWidth, (work.Bottom - work.Top - 20d) / rendered.PixelHeight));
    private void UpdateZoomLabel() => zoomLabel.Text = $"{zoom:P0}";

    private void MovePhysical(int x, int y, int width, int height)
    {
        using var dpi = NativeDpiScope.Enter();
        SetWindowPos(new WindowInteropHelper(this).Handle, new nint(-1), x, y, width, height, 0x0010);
    }

    private static NativeRect WorkingArea(PixelPoint point)
    {
        using var dpi = NativeDpiScope.Enter();
        var info = new MonitorData { Size = Marshal.SizeOf<MonitorData>() };
        if (!GetMonitorInfo(MonitorFromPoint(new NativePoint { X = point.X, Y = point.Y }, 2), ref info))
            throw new InvalidOperationException("无法读取显示器可用区域。");
        return info.Work;
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorData { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(NativePoint point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorData data);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
}
