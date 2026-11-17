using System.Windows.Input;

namespace ScreenshotTool.Editing;

public enum EditorAction { None, Confirm, Cancel, Save, Undo, Redo, Delete }

public static class EditorKeyRouter
{
    public static bool ShouldComplete(bool textInputFocused, bool imeComposing) => !textInputFocused && !imeComposing;

    public static EditorAction Route(Key key, ModifierKeys modifiers, bool textInputFocused)
    {
        if (key == Key.Escape) return EditorAction.Cancel;
        if (modifiers == ModifierKeys.Control && key == Key.S) return EditorAction.Save;
        if (textInputFocused) return EditorAction.None;
        if (modifiers == ModifierKeys.Control)
            return key switch { Key.Z => EditorAction.Undo, Key.Y => EditorAction.Redo, _ => EditorAction.None };
        if (modifiers == ModifierKeys.None)
            return key switch { Key.Enter => EditorAction.Confirm, Key.Delete => EditorAction.Delete, _ => EditorAction.None };
        return EditorAction.None;
    }
}
