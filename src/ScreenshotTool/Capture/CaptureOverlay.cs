using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ScreenshotTool.Desktop;

namespace ScreenshotTool.Capture;

public enum CaptureSelectionAction { Edit, Complete, Save }

/// <summary>Chooses a region of a frozen desktop, retaining global physical pixel coordinates.</summary>
public sealed class CaptureOverlay : Window
{
    private readonly ScreenSnapshot snapshot;
    private readonly MonitorService monitors = new();
    private readonly WindowPicker windows = new();
    private readonly SelectionSurface surface;
    private readonly Canvas chrome = new();
    private readonly Border toolbar;
    private PixelRect? selection;
    private PixelRect? hovered;
    private PixelRect? pressedWindow;
    private PixelRect originalSelection;
    private PixelRect instructionMonitor;
    private PixelPoint pressedPixel;
    private Point pressedPosition;
    private DragPart dragPart;
    private bool pressed;
    private bool moved;
    private HwndSource? source;

    public PixelRect? SelectedRegion => selection;
    public CaptureSelectionAction RequestedAction { get; private set; } = CaptureSelectionAction.Edit;

    public CaptureOverlay(ScreenSnapshot snapshot, PixelRect? initialSelection = null)
    {
        this.snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.Bounds.IsEmpty) throw new ArgumentException("截图内容为空。", nameof(snapshot));
        instructionMonitor = monitors.At(CurrentPointer()).Bounds.Intersect(snapshot.Bounds);
        if (initialSelection is { } initial && !initial.Intersect(snapshot.Bounds).IsEmpty)
            selection = initial.Intersect(snapshot.Bounds);

        Title = "选择截图区域";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        ShowInTaskbar = true;
        Topmost = true;
        Background = Brushes.Black;
        Width = snapshot.Pixels.PixelWidth;
        Height = snapshot.Pixels.PixelHeight;
        Left = snapshot.Origin.X;
        Top = snapshot.Origin.Y;
        UseLayoutRounding = true;

        surface = new SelectionSurface(this) { Focusable = true, SnapsToDevicePixels = true, Cursor = Cursors.Cross };
        RenderOptions.SetBitmapScalingMode(surface, BitmapScalingMode.NearestNeighbor);
        RenderOptions.SetEdgeMode(surface, EdgeMode.Aliased);
        var layout = new Grid();
        layout.Children.Add(surface);
        layout.Children.Add(chrome);
        chrome.Background = null;
        toolbar = CreateToolbar();
        chrome.Children.Add(toolbar);
        Content = layout;

        surface.MouseLeftButtonDown += PointerDown;
        surface.MouseMove += PointerMove;
        surface.MouseLeftButtonUp += PointerUp;
        surface.MouseRightButtonDown += (_, args) =>
        {
            if (selection is null) Cancel();
            else { selection = null; UpdateHover(CurrentPointer()); Refresh(); }
            args.Handled = true;
        };
        PreviewKeyDown += HandleKeys;
        SourceInitialized += (_, _) =>
        {
            source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)
                ?? throw new InvalidOperationException("无法创建截图窗口。");
            source.AddHook(WindowMessage);
            PositionNativeWindow();
        };
        Loaded += (_, _) =>
        {
            PositionNativeWindow();
            surface.Focus();
            UpdateHover(CurrentPointer());
            Refresh();
        };
        surface.SizeChanged += (_, _) => Refresh();
        Closed += (_, _) => source?.RemoveHook(WindowMessage);
    }

    private Border CreateToolbar()
    {
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        AddButton("标注", "双击选区也可进入标注", () => Choose(CaptureSelectionAction.Edit));
        AddButton("完成", "Enter · 按设置的完成动作输出", () => Choose(CaptureSelectionAction.Complete));
        AddButton("保存", "Ctrl+S · 选择保存位置", () => Choose(CaptureSelectionAction.Save));
        AddButton("全屏", "F · 选择鼠标所在显示器", SelectMonitor);
        AddButton("取消", "Esc", Cancel);
        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(28, 31, 36)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(84, 94, 108)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5),
            Padding = new Thickness(4), Child = actions, Visibility = Visibility.Collapsed
        };

        void AddButton(string text, string hint, Action action)
        {
            var button = new Button
            {
                Content = text, ToolTip = hint, Padding = new Thickness(12, 7, 12, 7),
                Margin = new Thickness(2), MinWidth = 52, FontSize = 13
            };
            button.Click += (_, _) => action();
            actions.Children.Add(button);
        }
    }

    private void PositionNativeWindow()
    {
        if (source is null) return;
        using var dpi = NativeDpiScope.Enter();
        var bounds = snapshot.Bounds;
        if (!SetWindowPos(source.Handle, new nint(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0200))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法显示截图选区。");
    }

    private nint WindowMessage(nint handle, int message, nint wParam, nint lParam, ref bool handled)
    {
        // WPF updates its DIP scale first. Restore the original physical desktop
        // bounds afterwards instead of adopting one monitor's suggested bounds.
        if (message == 0x02E0)
            Dispatcher.BeginInvoke(new Action(() => { if (IsVisible) PositionNativeWindow(); }));
        return 0;
    }

    private PixelPoint CurrentPointer() => monitors.Pointer;

    private PixelPoint ToPixel(Point position) => SelectionModel.FromDisplay(
        position.X, position.Y, snapshot.Bounds, surface.ActualWidth, surface.ActualHeight);

    private Rect ToDisplay(PixelRect rectangle)
    {
        var scaleX = surface.ActualWidth / snapshot.Bounds.Width;
        var scaleY = surface.ActualHeight / snapshot.Bounds.Height;
        return new((rectangle.X - snapshot.Origin.X) * scaleX, (rectangle.Y - snapshot.Origin.Y) * scaleY,
            rectangle.Width * scaleX, rectangle.Height * scaleY);
    }

    private static bool Contains(PixelRect rectangle, PixelPoint point) =>
        point.X >= rectangle.X && point.X <= rectangle.Right && point.Y >= rectangle.Y && point.Y <= rectangle.Bottom;

    private void UpdateHover(PixelPoint point)
    {
        if (selection is not null) return;
        var candidate = windows.TryPick(point)?.Intersect(snapshot.Bounds);
        hovered = candidate is { IsEmpty: false } ? candidate : null;
    }

    private void PointerDown(object sender, MouseButtonEventArgs args)
    {
        surface.Focus();
        var position = args.GetPosition(surface);
        var point = ToPixel(position);
        if (args.ClickCount == 2 && selection is { } existing && Contains(existing, point))
        {
            Choose(CaptureSelectionAction.Edit);
            args.Handled = true;
            return;
        }

        pressed = true;
        moved = false;
        pressedPixel = point;
        pressedPosition = position;
        originalSelection = selection.GetValueOrDefault();
        dragPart = selection is { } region ? HitPart(region, position) : DragPart.New;
        pressedWindow = dragPart == DragPart.New ? windows.TryPick(point)?.Intersect(snapshot.Bounds) : hovered;
        surface.CaptureMouse();
        toolbar.Visibility = Visibility.Collapsed;
        args.Handled = true;
    }

    private void PointerMove(object sender, MouseEventArgs args)
    {
        var position = args.GetPosition(surface);
        var point = ToPixel(position);
        if (!Contains(instructionMonitor, point))
            instructionMonitor = monitors.At(point).Bounds.Intersect(snapshot.Bounds);
        if (pressed)
        {
            moved |= Math.Abs(position.X - pressedPosition.X) >= 3 || Math.Abs(position.Y - pressedPosition.Y) >= 3;
            if (moved)
            {
                selection = dragPart switch
                {
                    DragPart.New => SelectionModel.FromDrag(pressedPixel, point).Intersect(snapshot.Bounds),
                    DragPart.Move => MoveSelection(point),
                    _ => ResizeSelection(point)
                };
                hovered = null;
                surface.InvalidateVisual();
            }
        }
        else
        {
            UpdateHover(point);
            surface.Cursor = selection is { } region ? PartCursor(HitPart(region, position)) : Cursors.Cross;
            surface.InvalidateVisual();
        }
        args.Handled = true;
    }

    private void PointerUp(object sender, MouseButtonEventArgs args)
    {
        if (!pressed) return;
        if (!moved && dragPart == DragPart.New) selection = pressedWindow;
        if (selection is { IsEmpty: true }) selection = null;
        pressed = false;
        surface.ReleaseMouseCapture();
        UpdateHover(ToPixel(args.GetPosition(surface)));
        Refresh();
        args.Handled = true;
    }

    private PixelRect MoveSelection(PixelPoint point)
    {
        var bounds = snapshot.Bounds;
        return originalSelection with
        {
            X = Math.Clamp(originalSelection.X + point.X - pressedPixel.X, bounds.X, bounds.Right - originalSelection.Width),
            Y = Math.Clamp(originalSelection.Y + point.Y - pressedPixel.Y, bounds.Y, bounds.Bottom - originalSelection.Height)
        };
    }

    private PixelRect ResizeSelection(PixelPoint point)
    {
        var bounds = snapshot.Bounds;
        var deltaX = point.X - pressedPixel.X;
        var deltaY = point.Y - pressedPixel.Y;
        var left = Math.Clamp(originalSelection.X + (dragPart.HasFlag(DragPart.Left) ? deltaX : 0), bounds.X, bounds.Right);
        var right = Math.Clamp(originalSelection.Right + (dragPart.HasFlag(DragPart.Right) ? deltaX : 0), bounds.X, bounds.Right);
        var top = Math.Clamp(originalSelection.Y + (dragPart.HasFlag(DragPart.Top) ? deltaY : 0), bounds.Y, bounds.Bottom);
        var bottom = Math.Clamp(originalSelection.Bottom + (dragPart.HasFlag(DragPart.Bottom) ? deltaY : 0), bounds.Y, bounds.Bottom);
        return SelectionModel.FromDrag(new(left, top), new(right, bottom));
    }

    private DragPart HitPart(PixelRect rectangle, Point point)
    {
        var display = ToDisplay(rectangle);
        var expanded = display;
        expanded.Inflate(7, 7);
        if (!expanded.Contains(point)) return DragPart.New;
        var part = DragPart.None;
        if (Math.Abs(point.X - display.Left) <= 7) part |= DragPart.Left;
        else if (Math.Abs(point.X - display.Right) <= 7) part |= DragPart.Right;
        if (Math.Abs(point.Y - display.Top) <= 7) part |= DragPart.Top;
        else if (Math.Abs(point.Y - display.Bottom) <= 7) part |= DragPart.Bottom;
        return part == DragPart.None ? DragPart.Move : part;
    }

    private static Cursor PartCursor(DragPart part)
    {
        if (part == DragPart.New) return Cursors.Cross;
        if (part == DragPart.Move) return Cursors.SizeAll;
        var horizontal = part.HasFlag(DragPart.Left) || part.HasFlag(DragPart.Right);
        var vertical = part.HasFlag(DragPart.Top) || part.HasFlag(DragPart.Bottom);
        if (horizontal && vertical)
            return part == (DragPart.Left | DragPart.Top) || part == (DragPart.Right | DragPart.Bottom) ? Cursors.SizeNWSE : Cursors.SizeNESW;
        return horizontal ? Cursors.SizeWE : Cursors.SizeNS;
    }

    private void HandleKeys(object sender, KeyEventArgs args)
    {
        if (args.Key == Key.Escape) Cancel();
        else if (args.Key == Key.Enter) Choose(CaptureSelectionAction.Complete);
        else if (args.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) Choose(CaptureSelectionAction.Save);
        else if (args.Key == Key.F && Keyboard.Modifiers == ModifierKeys.None) SelectMonitor();
        else return;
        args.Handled = true;
    }

    private void SelectMonitor()
    {
        selection = monitors.At(CurrentPointer()).Bounds.Intersect(snapshot.Bounds);
        hovered = null;
        Refresh();
    }

    private void Choose(CaptureSelectionAction action)
    {
        var chosen = selection ?? hovered;
        if (chosen is null || chosen.Value.IsEmpty) return;
        selection = chosen.Value.Intersect(snapshot.Bounds);
        RequestedAction = action;
        DialogResult = true;
    }

    private void Cancel() => DialogResult = false;

    private void Refresh()
    {
        surface.InvalidateVisual();
        toolbar.Visibility = selection is { IsEmpty: false } && !pressed ? Visibility.Visible : Visibility.Collapsed;
        if (toolbar.Visibility != Visibility.Visible || surface.ActualWidth <= 0 || surface.ActualHeight <= 0) return;
        toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var region = selection!.Value;
        var selectedDisplay = ToDisplay(region);
        var monitor = ToDisplay(monitors.At(new(region.Right - 1, region.Bottom - 1)).Bounds.Intersect(snapshot.Bounds));
        var width = toolbar.DesiredSize.Width;
        var height = toolbar.DesiredSize.Height;
        var x = Math.Clamp(selectedDisplay.Right - width, monitor.Left + 8, Math.Max(monitor.Left + 8, monitor.Right - width - 8));
        var y = selectedDisplay.Bottom + 10;
        if (y + height > monitor.Bottom - 8) y = selectedDisplay.Top - height - 10;
        y = Math.Clamp(y, monitor.Top + 8, Math.Max(monitor.Top + 8, monitor.Bottom - height - 8));
        Canvas.SetLeft(toolbar, x);
        Canvas.SetTop(toolbar, y);
    }

    [Flags]
    private enum DragPart { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8, Move = 16, New = 32 }

    private sealed class SelectionSurface(CaptureOverlay owner) : FrameworkElement
    {
        private readonly Brush shade = new SolidColorBrush(Color.FromArgb(135, 0, 0, 0));
        private readonly Brush accent = new SolidColorBrush(Color.FromRgb(66, 183, 255));

        protected override void OnRender(DrawingContext drawing)
        {
            var desktop = new Rect(0, 0, ActualWidth, ActualHeight);
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            drawing.DrawImage(owner.snapshot.Pixels, desktop);
            var active = owner.selection ?? owner.hovered;
            if (active is { IsEmpty: false } rectangle)
            {
                var region = owner.ToDisplay(rectangle);
                drawing.DrawGeometry(shade, null, new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(desktop), new RectangleGeometry(region)));
                var border = new Pen(accent, 1);
                drawing.DrawRectangle(null, border, region);
                if (owner.selection is not null)
                {
                    foreach (var x in new[] { region.Left, region.Left + region.Width / 2, region.Right })
                        foreach (var y in new[] { region.Top, region.Top + region.Height / 2, region.Bottom })
                            if (x != region.Left + region.Width / 2 || y != region.Top + region.Height / 2)
                                drawing.DrawRectangle(Brushes.White, border, new Rect(x - 3, y - 3, 6, 6));
                }
                Label(drawing, $"{rectangle.Width} × {rectangle.Height} 像素", new Point(Math.Clamp(region.Left, 0, Math.Max(0, ActualWidth - 180)), Math.Max(0, region.Top - 30)));
            }
            else drawing.DrawRectangle(shade, null, desktop);

            var monitor = owner.ToDisplay(owner.instructionMonitor);
            Label(drawing, "拖动框选 · 单击选窗口 · F 全屏 · Enter 完成 · Esc 取消", new Point(monitor.Left + 18, monitor.Top + 18));
        }

        private void Label(DrawingContext drawing, string text, Point position)
        {
            var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei UI"), 13, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(225, 25, 28, 33)), null,
                new Rect(position, new Size(formatted.Width + 16, formatted.Height + 10)), 3, 3);
            drawing.DrawText(formatted, new Point(position.X + 8, position.Y + 5));
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
}
