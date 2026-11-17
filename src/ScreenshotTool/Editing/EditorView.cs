using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

using WpfButton = System.Windows.Controls.Button;

namespace ScreenshotTool.Editing;

public sealed class EditorView : UserControl
{
    private readonly Canvas _viewport;
    private readonly DocumentSurface _surface;
    private readonly TextBlock _status;
    private readonly WpfButton _undoButton;
    private readonly WpfButton _redoButton;
    private readonly Dictionary<EditorTool, ToggleButton> _toolButtons = new();
    private readonly Color[] _colors = { Colors.Red, Color.FromRgb(255, 177, 0), Color.FromRgb(35, 197, 99), Color.FromRgb(36, 130, 255), Colors.White, Colors.Black };
    private EditorTool _tool = EditorTool.Select;
    private Color _color = Colors.Red;
    private double _strokeWidth = 3;
    private double _fontSize = 24;
    private Guid? _selectedId;
    private Point? _dragStart;
    private AnnotationDrag? _annotationDrag;
    private Annotation? _preview;
    private readonly List<Point> _penPoints = new();
    private TextBox? _textBox;
    private Annotation? _editingText;
    private bool _imeComposing;
    private bool _listening;
    private bool _syncingStyles;
    private Action<Annotation>? _showStyles;

    public EditorView(CaptureDocument document, bool isPinEdit = false)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Background = Brush("#111827");
        Foreground = Brush("#E5EAF2");
        FontFamily = new FontFamily("Microsoft YaHei UI");
        Focusable = true;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;

        var toolbar = new StackPanel { Margin = new Thickness(16, 12, 16, 10) };
        root.Children.Add(toolbar);
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        heading.Children.Add(new TextBlock { Text = isPinEdit ? "编辑贴图" : "编辑截图", FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var headingHint = new TextBlock { Text = "原始像素 · 独立标注", Foreground = Brush("#94A3B8"), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        heading.Children.Add(headingHint);
        toolbar.Children.Add(heading);

        var tools = new WrapPanel();
        foreach (var (tool, label) in new[]
        {
            (EditorTool.Select, "↖ 选择"), (EditorTool.Pen, "画笔"), (EditorTool.Rectangle, "矩形"),
            (EditorTool.Ellipse, "椭圆"), (EditorTool.Arrow, "箭头"), (EditorTool.Text, "文字"), (EditorTool.Mosaic, "马赛克")
        })
        {
            var button = new ToggleButton { Content = label, Height = 32, Padding = new Thickness(13, 3, 13, 3), Margin = new Thickness(0, 0, 6, 6), Foreground = Foreground, Background = Brush("#263247"), BorderThickness = new Thickness(0) };
            AutomationProperties.SetName(button, label);
            button.Click += (_, _) => SelectTool(tool);
            _toolButtons.Add(tool, button);
            tools.Children.Add(button);
        }
        toolbar.Children.Add(tools);

        var options = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
        AddLabel(options, "颜色");
        var colors = MakeCombo(new[] { "红色", "橙色", "绿色", "蓝色", "白色", "黑色" }, 0, 76);
        AutomationProperties.SetName(colors, "标注颜色");
        colors.SelectionChanged += (_, _) => { if (_syncingStyles || colors.SelectedIndex < 0) return; _color = _colors[colors.SelectedIndex]; ApplyStyle(); };
        options.Children.Add(colors);
        AddLabel(options, "线宽");
        var widths = new double[] { 1, 2, 3, 5, 8, 12 };
        var width = MakeCombo(widths.Select(value => value.ToString()).ToArray(), 2, 54);
        AutomationProperties.SetName(width, "标注线宽，像素");
        width.SelectionChanged += (_, _) => { if (_syncingStyles || width.SelectedIndex < 0) return; _strokeWidth = widths[width.SelectedIndex]; ApplyStyle(); };
        options.Children.Add(width);
        AddLabel(options, "字号");
        var sizes = new double[] { 14, 18, 24, 32, 40, 56, 72 };
        var size = MakeCombo(sizes.Select(value => value.ToString()).ToArray(), 2, 56);
        AutomationProperties.SetName(size, "文字字号，像素");
        size.SelectionChanged += (_, _) => { if (_syncingStyles || size.SelectedIndex < 0) return; _fontSize = sizes[size.SelectedIndex]; ApplyStyle(); };
        options.Children.Add(size);
        _showStyles = annotation =>
        {
            _syncingStyles = true;
            try
            {
                _color = annotation.Color;
                _strokeWidth = annotation.StrokeWidth;
                _fontSize = annotation.FontSize;
                colors.SelectedIndex = Array.IndexOf(_colors, annotation.Color);
                width.SelectedIndex = Array.IndexOf(widths, annotation.StrokeWidth);
                size.SelectedIndex = Array.IndexOf(sizes, annotation.FontSize);
            }
            finally { _syncingStyles = false; }
        };
        _undoButton = MakeButton("撤销", () => { CommitText(); Document.Undo(); }, "Ctrl+Z");
        _redoButton = MakeButton("重做", () => { CommitText(); Document.Redo(); }, "Ctrl+Y");
        options.Children.Add(_undoButton);
        options.Children.Add(_redoButton);
        options.Children.Add(MakeButton("删除", DeleteSelected, "Delete"));
        options.Children.Add(MakeButton("调整选区", () => { CommitText(); ReselectRequested?.Invoke(this, EventArgs.Empty); }));
        options.Children.Add(MakeButton(isPinEdit ? "导出草稿" : "保存", RequestSave, "Ctrl+S"));
        options.Children.Add(MakeButton("取消", () => CancelRequested?.Invoke(this, EventArgs.Empty), "Esc"));
        var confirm = MakeButton(isPinEdit ? "更新贴图" : "完成", RequestConfirm, "Enter");
        confirm.Background = Brush("#2563EB");
        confirm.Foreground = Brushes.White;
        options.Children.Add(confirm);
        toolbar.Children.Add(options);

        _viewport = new Canvas { Background = Brushes.Black, ClipToBounds = true, Focusable = true };
        _surface = new DocumentSurface(this, document) { IsHitTestVisible = false };
        _viewport.Children.Add(_surface);
        var viewbox = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = _viewport, Margin = new Thickness(20) };
        var workspace = new Border { Background = Brush("#0B1020"), BorderBrush = Brush("#2A3548"), BorderThickness = new Thickness(1, 1, 1, 0), Child = viewbox };
        Grid.SetRow(workspace, 1);
        root.Children.Add(workspace);
        _status = new TextBlock { Foreground = Brush("#AAB7CA"), FontSize = 11, Margin = new Thickness(16, 8, 16, 8) };
        Grid.SetRow(_status, 2);
        root.Children.Add(_status);

        _viewport.MouseLeftButtonDown += OnPointerDown;
        _viewport.MouseMove += OnPointerMove;
        _viewport.MouseLeftButtonUp += OnPointerUp;
        _viewport.LostMouseCapture += (_, _) => FinishDrag();
        SizeChanged += (_, _) => _surface.InvalidateVisual();
        PreviewKeyDown += OnPreviewKeyDown;
        TextCompositionManager.AddPreviewTextInputStartHandler(this, (_, _) => _imeComposing = true);
        TextCompositionManager.AddPreviewTextInputHandler(this, (_, _) => _imeComposing = false);
        Loaded += (_, _) =>
        {
            if (!_listening) { Document.Changed += OnDocumentChanged; _listening = true; }
            Refresh();
            _viewport.Focus();
        };
        Unloaded += (_, _) =>
        {
            if (_listening) { Document.Changed -= OnDocumentChanged; _listening = false; }
        };
        SelectTool(EditorTool.Select);
        Refresh();
    }

    public CaptureDocument Document { get; }
    public event EventHandler? ConfirmRequested;
    public event EventHandler? CancelRequested;
    public event EventHandler? SaveRequested;
    public event EventHandler? ReselectRequested;

    private static SolidColorBrush Brush(string value) => new((Color)ColorConverter.ConvertFromString(value));

    private static ComboBox MakeCombo(string[] items, int selectedIndex, double width)
        => new() { ItemsSource = items, SelectedIndex = selectedIndex, Width = width, Height = 29, Margin = new Thickness(0, 0, 10, 5), VerticalContentAlignment = VerticalAlignment.Center };

    private static void AddLabel(Panel panel, string text)
        => panel.Children.Add(new TextBlock { Text = text, Margin = new Thickness(0, 6, 5, 0), FontSize = 11 });

    private WpfButton MakeButton(string text, Action action, string? shortcut = null)
    {
        var button = new WpfButton { Content = text, Height = 29, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 6, 5), Background = Brush("#E6EBF3"), Foreground = Brush("#172033"), BorderThickness = new Thickness(0), ToolTip = shortcut is null ? text : $"{text} ({shortcut})" };
        AutomationProperties.SetName(button, text);
        button.Click += (_, _) => action();
        return button;
    }

    private void SelectTool(EditorTool tool)
    {
        CommitText();
        FinishDrag();
        _tool = tool;
        _selectedId = null;
        foreach (var item in _toolButtons)
        {
            item.Value.IsChecked = item.Key == tool;
            item.Value.Background = item.Key == tool ? Brush("#2563EB") : Brush("#263247");
        }
        _viewport.Cursor = tool == EditorTool.Select ? Cursors.Arrow : tool == EditorTool.Text ? Cursors.IBeam : Cursors.Cross;
        Refresh();
    }

    private Point SourcePosition(Point viewportPosition)
    {
        var selection = Document.Selection;
        return new Point(selection.X + Math.Clamp(viewportPosition.X, 0, selection.Width), selection.Y + Math.Clamp(viewportPosition.Y, 0, selection.Height));
    }

    private double ViewportScale()
    {
        var transform = _viewport.TransformToAncestor(this);
        var scale = (transform.Transform(new Point(1, 0)) - transform.Transform(new Point())).Length;
        return scale > 0 && double.IsFinite(scale) ? scale : 1;
    }

    private AnnotationHandleKind? HitSelectedHandle(Point point)
    {
        if (_tool != EditorTool.Select) return null;
        var selected = Document.Annotations.FirstOrDefault(annotation => annotation.Id == _selectedId);
        return selected is null ? null : AnnotationHandles.HitTest(selected, point, ViewportScale());
    }

    private Annotation? HitTest(Point point)
    {
        foreach (var annotation in Document.Annotations.Reverse())
        {
            var bounds = annotation.GetBounds();
            bounds.Inflate(Math.Max(5, annotation.StrokeWidth), Math.Max(5, annotation.StrokeWidth));
            if (bounds.Contains(point)) return annotation;
        }
        return null;
    }

    private void OnPointerDown(object sender, MouseButtonEventArgs args)
    {
        if (_textBox is not null && _textBox.IsMouseOver) return;
        _viewport.Focus();
        if (BeginPointer(args.GetPosition(_viewport), args.ClickCount) && !_viewport.CaptureMouse())
        {
            _dragStart = null;
            _preview = null;
            _annotationDrag = null;
            Refresh();
        }
        args.Handled = true;
    }

    internal bool BeginPointer(Point viewportPosition, int clickCount = 1)
    {
        CommitText();
        var point = SourcePosition(viewportPosition);
        var handle = HitSelectedHandle(point);
        var hit = handle is null ? HitTest(point) : Document.Annotations.First(annotation => annotation.Id == _selectedId);
        if (_tool == EditorTool.Text || (_tool == EditorTool.Select && clickCount == 2 && hit?.Kind == AnnotationKind.Text))
        {
            BeginText(hit?.Kind == AnnotationKind.Text ? hit : new Annotation
            {
                Kind = AnnotationKind.Text, Bounds = new Rect(point.X, point.Y, Math.Max(1, Document.Selection.X + Document.Selection.Width - point.X), _fontSize * 1.5),
                Color = _color, FontSize = _fontSize, StrokeWidth = _strokeWidth
            });
            return false;
        }
        _selectedId = _tool == EditorTool.Select ? hit?.Id : null;
        _annotationDrag = _tool == EditorTool.Select && hit is not null ? new AnnotationDrag(Document, hit, point, handle) : null;
        if (_annotationDrag is not null) _showStyles?.Invoke(_annotationDrag.Original);
        _dragStart = point;
        _penPoints.Clear();
        _penPoints.Add(point);
        if (_tool != EditorTool.Select) _preview = NewAnnotation(point, point);
        Refresh();
        return true;
    }

    private void OnPointerMove(object sender, MouseEventArgs args)
    {
        var position = args.GetPosition(_viewport);
        if (_dragStart is not null && args.LeftButton == MouseButtonState.Pressed) UpdatePointer(position);
        else _viewport.Cursor = HandleCursor(HitSelectedHandle(SourcePosition(position)));
    }

    internal void UpdatePointer(Point viewportPosition)
    {
        if (_dragStart is not Point start) return;
        var point = SourcePosition(viewportPosition);
        if (_annotationDrag is not null)
        {
            _annotationDrag.Update(point);
            _preview = _annotationDrag.Preview;
        }
        else if (_tool != EditorTool.Select)
        {
            if (_tool == EditorTool.Pen && (_penPoints[_penPoints.Count - 1] - point).Length >= 0.5) _penPoints.Add(point);
            _preview = NewAnnotation(start, point, _preview?.Id);
        }
        Refresh();
    }

    private void OnPointerUp(object sender, MouseButtonEventArgs args)
    {
        EndPointer(args.GetPosition(_viewport));
        args.Handled = true;
    }

    internal void EndPointer(Point viewportPosition)
    {
        UpdatePointer(viewportPosition);
        FinishDrag();
    }

    private Cursor HandleCursor(AnnotationHandleKind? handle) => handle switch
    {
        AnnotationHandleKind.TopLeft or AnnotationHandleKind.BottomRight => Cursors.SizeNWSE,
        AnnotationHandleKind.TopRight or AnnotationHandleKind.BottomLeft => Cursors.SizeNESW,
        AnnotationHandleKind.Top or AnnotationHandleKind.Bottom => Cursors.SizeNS,
        AnnotationHandleKind.Left or AnnotationHandleKind.Right => Cursors.SizeWE,
        AnnotationHandleKind.ArrowStart or AnnotationHandleKind.ArrowEnd => Cursors.SizeAll,
        _ => _tool == EditorTool.Select ? Cursors.Arrow : _tool == EditorTool.Text ? Cursors.IBeam : Cursors.Cross
    };

    private Annotation NewAnnotation(Point start, Point end, Guid? id = null)
    {
        var bounds = new Rect(start, end);
        return new Annotation
        {
            Id = id ?? Guid.NewGuid(), Kind = (AnnotationKind)((int)_tool - 1), Bounds = bounds,
            Points = _tool == EditorTool.Pen ? _penPoints.ToArray() : new[] { start, end },
            Color = _color, StrokeWidth = _strokeWidth, FontSize = _fontSize,
            MosaicBlockSize = Math.Max(4, (int)(_strokeWidth * 4))
        };
    }

    private void FinishDrag()
    {
        if (_dragStart is null) return;
        var preview = _preview;
        var annotationDrag = _annotationDrag;
        _dragStart = null;
        _preview = null;
        _annotationDrag = null;
        if (_viewport.IsMouseCaptured) _viewport.ReleaseMouseCapture();
        if (annotationDrag is not null) annotationDrag.Commit();
        else if (preview is not null)
        {
            if (preview.Kind == AnnotationKind.Pen || preview.GetBounds().Width >= 1 || preview.GetBounds().Height >= 1)
            {
                Document.AddAnnotation(preview);
                _selectedId = preview.Id;
            }
        }
        Refresh();
    }

    private void BeginText(Annotation annotation)
    {
        _showStyles?.Invoke(annotation);
        _editingText = annotation;
        _selectedId = annotation.Id;
        _textBox = new TextBox
        {
            Text = annotation.Text, FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = annotation.FontSize,
            Foreground = new SolidColorBrush(annotation.Color), Background = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)),
            BorderBrush = Brush("#2563EB"), BorderThickness = new Thickness(1), Padding = new Thickness(0),
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Width = Math.Max(1, annotation.Bounds.Width),
            MinHeight = annotation.FontSize * 1.5, IsUndoEnabled = true, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        InputMethod.SetIsInputMethodEnabled(_textBox, true);
        Canvas.SetLeft(_textBox, annotation.Bounds.X - Document.Selection.X);
        Canvas.SetTop(_textBox, annotation.Bounds.Y - Document.Selection.Y);
        AutomationProperties.SetName(_textBox, "标注文字，Enter 换行");
        _viewport.Children.Add(_textBox);
        _textBox.Focus();
        _textBox.CaretIndex = _textBox.Text.Length;
        Refresh();
    }

    private void CommitText()
    {
        if (_textBox is null || _editingText is null) return;
        var textBox = _textBox;
        var original = _editingText;
        _textBox = null;
        _editingText = null;
        _imeComposing = false;
        _viewport.Children.Remove(textBox);
        var exists = Document.Annotations.Any(item => item.Id == original.Id);
        if (string.IsNullOrWhiteSpace(textBox.Text))
        {
            if (exists) Document.RemoveAnnotation(original.Id);
            _selectedId = null;
        }
        else
        {
            var annotation = original with { Text = textBox.Text, Color = ((SolidColorBrush)textBox.Foreground).Color, FontSize = textBox.FontSize };
            var bounds = annotation.Bounds;
            bounds.Height = Math.Max(annotation.FontSize * 1.3, DocumentRenderer.FormatText(annotation).Height);
            annotation = annotation with { Bounds = bounds };
            if (exists) Document.ReplaceAnnotation(annotation.Id, annotation);
            else Document.AddAnnotation(annotation);
        }
        Refresh();
    }

    private void ApplyStyle()
    {
        if (_textBox is not null)
        {
            _textBox.Foreground = new SolidColorBrush(_color);
            _textBox.FontSize = _fontSize;
            return;
        }
        var selected = Document.Annotations.FirstOrDefault(item => item.Id == _selectedId);
        if (selected is not null)
            Document.ReplaceAnnotation(selected.Id, selected with { Color = _color, StrokeWidth = _strokeWidth, FontSize = _fontSize, MosaicBlockSize = Math.Max(4, (int)(_strokeWidth * 4)) });
    }

    private void DeleteSelected()
    {
        CommitText();
        if (_selectedId is Guid id) Document.RemoveAnnotation(id);
        _selectedId = null;
        Refresh();
    }

    private void RequestSave() { FinishDrag(); CommitText(); SaveRequested?.Invoke(this, EventArgs.Empty); }
    private void RequestConfirm() { FinishDrag(); CommitText(); ConfirmRequested?.Invoke(this, EventArgs.Empty); }

    private void OnPreviewKeyDown(object sender, KeyEventArgs args)
    {
        var textFocused = _textBox?.IsKeyboardFocusWithin == true || Keyboard.FocusedElement is TextBoxBase;
        var action = EditorKeyRouter.Route(args.Key, Keyboard.Modifiers, textFocused);
        if (action == EditorAction.Confirm && !EditorKeyRouter.ShouldComplete(textFocused, _imeComposing)) return;
        if (action == EditorAction.None) return;
        args.Handled = true;
        switch (action)
        {
            case EditorAction.Confirm: RequestConfirm(); break;
            case EditorAction.Cancel: CancelRequested?.Invoke(this, EventArgs.Empty); break;
            case EditorAction.Save: RequestSave(); break;
            case EditorAction.Undo: FinishDrag(); CommitText(); Document.Undo(); break;
            case EditorAction.Redo: FinishDrag(); CommitText(); Document.Redo(); break;
            case EditorAction.Delete: DeleteSelected(); break;
        }
    }

    private void OnDocumentChanged(object? sender, EventArgs args) => Refresh();

    private void Refresh()
    {
        var selection = Document.Selection;
        _viewport.Width = _surface.Width = selection.Width;
        _viewport.Height = _surface.Height = selection.Height;
        _surface.Preview = _preview;
        _surface.SuppressedId = _editingText?.Id ?? (_preview is not null ? _annotationDrag?.Original.Id : null);
        _surface.SelectedId = _selectedId;
        _surface.ShowHandles = _tool == EditorTool.Select;
        _surface.InvalidateVisual();
        _undoButton.IsEnabled = Document.CanUndo;
        _redoButton.IsEnabled = Document.CanRedo;
        _status.Text = $"{selection.Width} × {selection.Height} 像素    ·    Enter 完成 / Esc 取消    ·    切换“选择”后点击标注，拖动控制点调整，拖动主体移动；双击文字修改";
    }

    private enum EditorTool { Select, Pen, Rectangle, Ellipse, Arrow, Text, Mosaic }

    private sealed class DocumentSurface : FrameworkElement
    {
        private readonly EditorView _owner;
        private readonly CaptureDocument _document;
        public DocumentSurface(EditorView owner, CaptureDocument document) { _owner = owner; _document = document; }
        public Annotation? Preview { get; set; }
        public Guid? SuppressedId { get; set; }
        public Guid? SelectedId { get; set; }
        public bool ShowHandles { get; set; }

        protected override void OnRender(DrawingContext context)
        {
            base.OnRender(context);
            var selection = _document.Selection;
            context.PushClip(new RectangleGeometry(new Rect(0, 0, selection.Width, selection.Height)));
            context.PushTransform(new TranslateTransform(-selection.X, -selection.Y));
            DocumentRenderer.DrawBase(context, _document.Source);
            foreach (var annotation in _document.Annotations)
                if (annotation.Id != SuppressedId) DocumentRenderer.DrawAnnotation(context, _document.Source, annotation);
            if (Preview is not null) DocumentRenderer.DrawAnnotation(context, _document.Source, Preview);
            var selected = Preview ?? _document.Annotations.FirstOrDefault(item => item.Id == SelectedId && item.Id != SuppressedId);
            if (selected is not null)
            {
                var scale = _owner.ViewportScale();
                var bounds = selected.GetBounds();
                bounds.Inflate(4 / scale, 4 / scale);
                context.DrawRectangle(null, new Pen(Brush("#3B82F6"), 1 / scale) { DashStyle = DashStyles.Dash }, bounds);
                if (ShowHandles)
                {
                    var radius = AnnotationHandles.DisplayRadius(scale);
                    var outline = new Pen(Brush("#2563EB"), 1.5 / scale);
                    foreach (var handle in AnnotationHandles.GetHandles(selected))
                        context.DrawEllipse(Brushes.White, outline, handle.Position, radius, radius);
                }
            }
            context.Pop();
            context.Pop();
        }
    }
}
