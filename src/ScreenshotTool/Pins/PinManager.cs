using System;
using System.Collections.Generic;
using System.Linq;
using ScreenshotTool.Editing;

namespace ScreenshotTool.Pins;

public sealed record PinnedImage(Guid Id, CaptureDocument Document, bool IsVisible = true);
public sealed record PinEditSession(Guid PinId, CaptureDocument Draft)
{
    internal bool IsActive { get; set; } = true;
}
public sealed class PinManager
{
    private readonly Dictionary<Guid, PinnedImage> _pins = new();
    public event EventHandler? Changed;
    public int Count => _pins.Count;
    public IReadOnlyList<PinnedImage> Pins => _pins.Values.ToArray();
    public Guid Create(CaptureDocument document)
    {
        var id = Guid.NewGuid();
        _pins.Add(id, new PinnedImage(id, document.Clone()));
        Changed?.Invoke(this, EventArgs.Empty);
        return id;
    }
    public CaptureDocument Get(Guid id) => _pins[id].Document;
    public bool IsVisible(Guid id) => _pins.TryGetValue(id, out var pin) && pin.IsVisible;
    public void Close(Guid id)
    {
        if (_pins.Remove(id)) Changed?.Invoke(this, EventArgs.Empty);
    }
    public PinEditSession BeginEdit(Guid id) => new(id, Get(id).Clone());
    public void Commit(PinEditSession edit)
    {
        if (!edit.IsActive || !_pins.TryGetValue(edit.PinId, out var pin))
            throw new InvalidOperationException("这次贴图编辑已经结束。");
        _pins[edit.PinId] = pin with { Document = edit.Draft.Clone() };
        edit.IsActive = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }
    public void Cancel(PinEditSession edit) => edit.IsActive = false;
    public IDisposable HideForCapture()
    {
        var previous = _pins.ToDictionary(pair => pair.Key, pair => pair.Value.IsVisible);
        void RestoreVisibility()
        {
            foreach (var (id, visible) in previous)
                if (_pins.TryGetValue(id, out var pin)) _pins[id] = pin with { IsVisible = visible };
        }
        try
        {
            foreach (var id in previous.Keys) _pins[id] = _pins[id] with { IsVisible = false };
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception hideError)
        {
            RestoreVisibility();
            try { Changed?.Invoke(this, EventArgs.Empty); }
            catch (Exception restoreError) { throw new AggregateException("隐藏贴图失败，恢复窗口时也发生错误。", hideError, restoreError); }
            throw;
        }
        return new RestoreScope(() =>
        {
            RestoreVisibility();
            Changed?.Invoke(this, EventArgs.Empty);
        });
    }
    private sealed class RestoreScope(Action restore) : IDisposable
    {
        private Action? _restore = restore;
        public void Dispose()
        {
            var action = _restore;
            _restore = null;
            action?.Invoke();
        }
    }
}
