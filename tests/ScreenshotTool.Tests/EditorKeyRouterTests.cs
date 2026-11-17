using System.Windows.Input;
using ScreenshotTool.Editing;
using Xunit;

namespace ScreenshotTool.Tests;

public sealed class EditorKeyRouterTests
{
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void EnterInTextOrIme_DoesNotComplete(bool textFocused, bool imeComposing, bool expected)
        => Assert.Equal(expected, EditorKeyRouter.ShouldComplete(textFocused, imeComposing));

    [Theory]
    [InlineData(Key.Enter, ModifierKeys.None, false, EditorAction.Confirm)]
    [InlineData(Key.Enter, ModifierKeys.None, true, EditorAction.None)]
    [InlineData(Key.ImeProcessed, ModifierKeys.None, false, EditorAction.None)]
    [InlineData(Key.Escape, ModifierKeys.None, true, EditorAction.Cancel)]
    [InlineData(Key.S, ModifierKeys.Control, true, EditorAction.Save)]
    [InlineData(Key.Z, ModifierKeys.Control, false, EditorAction.Undo)]
    [InlineData(Key.Y, ModifierKeys.Control, false, EditorAction.Redo)]
    [InlineData(Key.Z, ModifierKeys.Control, true, EditorAction.None)]
    [InlineData(Key.Y, ModifierKeys.Control, true, EditorAction.None)]
    [InlineData(Key.Delete, ModifierKeys.None, true, EditorAction.None)]
    [InlineData(Key.Delete, ModifierKeys.None, false, EditorAction.Delete)]
    public void Route_ProtectsNativeTextEditing(Key key, ModifierKeys modifiers, bool textFocused, EditorAction expected)
        => Assert.Equal(expected, EditorKeyRouter.Route(key, modifiers, textFocused));
}
