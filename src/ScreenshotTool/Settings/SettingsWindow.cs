using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenshotTool.Desktop;
using ScreenshotTool.Workflow;

namespace ScreenshotTool.Settings;

public sealed class SettingsWindow : Window
{
    private readonly SettingsService settings;
    private readonly TextBox hotkeyBox;
    private readonly ComboBox completion;
    private readonly ComboBox format;
    private readonly CheckBox startup;
    private readonly TextBlock feedback;
    private Hotkey hotkey;

    public event EventHandler? SettingsChanged;
    public bool IsRecordingHotkey => hotkeyBox.IsKeyboardFocusWithin;

    public SettingsWindow(SettingsService settings)
    {
        this.settings = settings;
        hotkey = settings.Current.CaptureHotkey;
        Title = "截图工具设置";
        Width = 480;
        Height = 540;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Microsoft YaHei UI");
        Background = new SolidColorBrush(Color.FromRgb(248, 249, 251));
        var root = new StackPanel { Margin = new Thickness(28) };
        Content = root;
        root.Children.Add(new TextBlock { Text = "随时开始一张截图", FontSize = 22, FontWeight = FontWeights.SemiBold });
        root.Children.Add(new TextBlock { Text = "设置立即生效，并在下次启动时保留。", Foreground = Brushes.DimGray, Margin = new Thickness(0, 6, 0, 20) });
        root.Children.Add(Label("截图快捷键"));
        hotkeyBox = new TextBox { Text = FormatHotkey(hotkey), IsReadOnly = true, Padding = new Thickness(9), ToolTip = "点击此处，然后按下新的组合键" };
        hotkeyBox.PreviewKeyDown += RecordHotkey;
        root.Children.Add(hotkeyBox);
        root.Children.Add(new TextBlock { Text = "点击输入框后按组合键；请至少使用一个修饰键。", FontSize = 11, Foreground = Brushes.DimGray, Margin = new Thickness(0, 5, 0, 12) });
        root.Children.Add(Label("完成截图时"));
        completion = new ComboBox { Padding = new Thickness(7), ItemsSource = new[] { "仅复制", "仅贴图", "复制并贴图" }, SelectedIndex = (int)settings.Current.CompletionAction, Margin = new Thickness(0, 0, 0, 12) };
        root.Children.Add(completion);
        root.Children.Add(Label("默认导出格式"));
        format = new ComboBox { Padding = new Thickness(7), ItemsSource = new[] { "PNG（无损）", "JPG" }, SelectedIndex = (int)settings.Current.ImageFormat, Margin = new Thickness(0, 0, 0, 16) };
        root.Children.Add(format);
        startup = new CheckBox { Content = "登录 Windows 时自动启动", IsChecked = settings.Current.StartWithWindows };
        root.Children.Add(startup);
        feedback = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, MinHeight = 36, Margin = new Thickness(0, 12, 0, 4) };
        root.Children.Add(feedback);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var close = new Button { Content = "关闭", Padding = new Thickness(18, 6, 18, 6), IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
        close.Click += (_, _) => Close();
        var apply = new Button { Content = "应用设置", Padding = new Thickness(18, 6, 18, 6), IsDefault = true };
        apply.Click += (_, _) => Apply();
        buttons.Children.Add(close);
        buttons.Children.Add(apply);
        root.Children.Add(buttons);
    }

    private static TextBlock Label(string text) => new() { Text = text, Margin = new Thickness(0, 0, 0, 5), FontWeight = FontWeights.SemiBold };

    private void RecordHotkey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        var modifiers = Keyboard.Modifiers;
        var nativeModifiers = ((modifiers & ModifierKeys.Alt) != 0 ? 1 : 0) | ((modifiers & ModifierKeys.Control) != 0 ? 2 : 0)
            | ((modifiers & ModifierKeys.Shift) != 0 ? 4 : 0) | ((modifiers & ModifierKeys.Windows) != 0 ? 8 : 0);
        if (nativeModifiers == 0)
        {
            feedback.Text = "请同时按住 Ctrl、Alt、Shift 或 Win 中的至少一个键。";
            return;
        }
        hotkey = new(nativeModifiers, KeyInterop.VirtualKeyFromKey(key));
        hotkeyBox.Text = FormatHotkey(hotkey);
        feedback.Text = "";
    }

    private void Apply()
    {
        var candidate = settings.Current with { CaptureHotkey = hotkey, CompletionAction = (CompletionAction)completion.SelectedIndex,
            ImageFormat = (ImageFormat)format.SelectedIndex, StartWithWindows = startup.IsChecked == true };
        var result = settings.TryApply(candidate);
        feedback.Foreground = result.Success ? Brushes.DarkGreen : Brushes.Firebrick;
        feedback.Text = result.Success ? "设置已保存。" : result.Error;
        if (result.Success) SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string FormatHotkey(Hotkey hotkey)
    {
        var parts = new List<string>();
        if ((hotkey.Modifiers & 2) != 0) parts.Add("Ctrl");
        if ((hotkey.Modifiers & 1) != 0) parts.Add("Alt");
        if ((hotkey.Modifiers & 4) != 0) parts.Add("Shift");
        if ((hotkey.Modifiers & 8) != 0) parts.Add("Win");
        parts.Add(KeyInterop.KeyFromVirtualKey(hotkey.VirtualKey).ToString());
        return string.Join(" + ", parts);
    }
}
