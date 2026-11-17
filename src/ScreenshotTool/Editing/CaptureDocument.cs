using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media.Imaging;
using ScreenshotTool.Capture;

namespace ScreenshotTool.Editing;

public sealed class CaptureDocument
{
    private List<Annotation> _annotations = new();
    private readonly Stack<DocumentState> _undo = new();
    private readonly Stack<DocumentState> _redo = new();

    public CaptureDocument(BitmapSource source, PixelRect selection, PixelPoint sourceOrigin = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source.IsFrozen ? source : source.CloneCurrentValue();
        if (!Source.IsFrozen) Source.Freeze();
        ValidateSelection(selection);
        Selection = selection;
        SourceOrigin = sourceOrigin;
    }

    public BitmapSource Source { get; }
    public PixelPoint SourceOrigin { get; }
    public PixelRect Selection { get; private set; }
    public IReadOnlyList<Annotation> Annotations => new ReadOnlyCollection<Annotation>(_annotations);
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public event EventHandler? Changed;

    public void SetSelection(PixelRect selection)
    {
        ValidateSelection(selection);
        if (Selection == selection) return;
        BeforeChange();
        Selection = selection;
        OnChanged();
    }

    public void AddAnnotation(Annotation annotation)
    {
        ValidateAnnotation(annotation);
        if (_annotations.Any(item => item.Id == annotation.Id)) throw new ArgumentException("标注 ID 已存在。", nameof(annotation));
        BeforeChange();
        _annotations.Add(annotation.DeepCopy());
        OnChanged();
    }

    public void ReplaceAnnotation(Guid id, Annotation annotation)
    {
        ValidateAnnotation(annotation);
        var index = _annotations.FindIndex(item => item.Id == id);
        if (index < 0) throw new KeyNotFoundException("找不到要修改的标注。");
        BeforeChange();
        _annotations[index] = (annotation with { Id = id }).DeepCopy();
        OnChanged();
    }

    public void RemoveAnnotation(Guid id)
    {
        var index = _annotations.FindIndex(item => item.Id == id);
        if (index < 0) return;
        BeforeChange();
        _annotations.RemoveAt(index);
        OnChanged();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        _redo.Push(Snapshot());
        Restore(_undo.Pop());
    }

    public void Redo()
    {
        if (!CanRedo) return;
        _undo.Push(Snapshot());
        Restore(_redo.Pop());
    }

    public CaptureDocument Clone()
    {
        var clone = new CaptureDocument(Source, Selection, SourceOrigin) { _annotations = CopyAnnotations(_annotations) };
        foreach (var state in _undo.Reverse()) clone._undo.Push(state.DeepCopy());
        foreach (var state in _redo.Reverse()) clone._redo.Push(state.DeepCopy());
        return clone;
    }

    private void ValidateSelection(PixelRect selection)
    {
        if (selection.X < 0 || selection.Y < 0 || selection.Width <= 0 || selection.Height <= 0 ||
            (long)selection.X + selection.Width > Source.PixelWidth || (long)selection.Y + selection.Height > Source.PixelHeight)
            throw new ArgumentOutOfRangeException(nameof(selection), "选区必须位于原始图像内且至少为 1 × 1 像素。");
    }

    private static void ValidateAnnotation(Annotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        if (annotation.Id == Guid.Empty || !Enum.IsDefined(annotation.Kind) || annotation.Points is null || annotation.Text is null ||
            !double.IsFinite(annotation.StrokeWidth) || annotation.StrokeWidth <= 0 ||
            !double.IsFinite(annotation.FontSize) || annotation.FontSize <= 0 || annotation.MosaicBlockSize < 1 ||
            annotation.Bounds.IsEmpty || !double.IsFinite(annotation.Bounds.X) || !double.IsFinite(annotation.Bounds.Y) ||
            !double.IsFinite(annotation.Bounds.Width) || !double.IsFinite(annotation.Bounds.Height) ||
            annotation.Points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
            throw new ArgumentException("标注包含无效的坐标或样式。", nameof(annotation));
    }

    private void BeforeChange() { _undo.Push(Snapshot()); _redo.Clear(); }
    private DocumentState Snapshot() => new(Selection, CopyAnnotations(_annotations));
    private void Restore(DocumentState state) { Selection = state.Selection; _annotations = CopyAnnotations(state.Annotations); OnChanged(); }
    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
    private static List<Annotation> CopyAnnotations(IEnumerable<Annotation> annotations) => annotations.Select(item => item.DeepCopy()).ToList();
    private sealed record DocumentState(PixelRect Selection, List<Annotation> Annotations)
    {
        public DocumentState DeepCopy() => new(Selection, CopyAnnotations(Annotations));
    }
}
