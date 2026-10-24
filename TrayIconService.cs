using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace ActivateWindows;

/// <summary>
/// 通知領域（タスクトレイ）のアイコン。WPF には標準の NotifyIcon が無いので WinForms のものを借りる。
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ToolStripMenuItem _toggleItem;

    public TrayIconService(Action openSettings, Action toggleWatermark, Action exit)
    {
        var openItem = new WinForms.ToolStripMenuItem("設定を開く");
        openItem.Click += (_, _) => openSettings();

        _toggleItem = new WinForms.ToolStripMenuItem("ウォーターマークを表示");
        _toggleItem.Click += (_, _) => toggleWatermark();

        var exitItem = new WinForms.ToolStripMenuItem("終了");
        exitItem.Click += (_, _) => exit();

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(openItem);
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _icon = new WinForms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Activate Windows",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => openSettings();
    }

    // exe に設定したアイコン（<ApplicationIcon>）があればそれを使う。無ければ標準アイコン。
    private static Drawing.Icon LoadIcon()
    {
        Drawing.Icon? icon = null;
        try
        {
            if (Environment.ProcessPath is { } path)
                icon = Drawing.Icon.ExtractAssociatedIcon(path);
        }
        catch
        {
            // 取れなければフォールバック
        }
        return icon ?? Drawing.SystemIcons.Application;
    }

    public void SetWatermarkVisible(bool visible) =>
        _toggleItem.Text = visible ? "ウォーターマークを非表示" : "ウォーターマークを表示";

    public void ShowBalloon(string title, string text) =>
        _icon.ShowBalloonTip(3000, title, text, WinForms.ToolTipIcon.Info);

    public void Dispose()
    {
        _icon.Visible = false; // 消し忘れで幽霊アイコンが残らないように
        _icon.Dispose();
    }
}