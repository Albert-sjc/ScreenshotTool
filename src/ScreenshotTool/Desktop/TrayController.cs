using System;
using System.Drawing;
using Forms = System.Windows.Forms;

namespace ScreenshotTool.Desktop;

public sealed class TrayController : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly Forms.ContextMenuStrip menu;

    public TrayController(Action capture, Action settings, Action managePins, Action exit)
    {
        menu = new Forms.ContextMenuStrip();
        menu.Items.Add("开始截图", null, (_, _) => capture());
        menu.Items.Add("设置", null, (_, _) => settings());
        menu.Items.Add("管理贴图", null, (_, _) => managePins());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());
        icon = new Forms.NotifyIcon { Icon = SystemIcons.Application, Text = "ScreenshotTool 截图工具", ContextMenuStrip = menu, Visible = true };
        icon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left) settings();
        };
    }

    public void Notify(string message)
    {
        icon.BalloonTipTitle = "ScreenshotTool";
        icon.BalloonTipText = message;
        icon.BalloonTipIcon = Forms.ToolTipIcon.Info;
        icon.ShowBalloonTip(5000);
    }

    public void Dispose()
    {
        icon.Visible = false;
        icon.Dispose();
        menu.Dispose();
    }
}
