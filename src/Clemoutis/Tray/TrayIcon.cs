namespace Clemoutis.Tray;

/// <summary>
/// タスクトレイ常駐アイコンとコンテキストメニュー（設定・一時停止・終了）。
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _pauseItem;

    public event Action? OpenSettingsRequested;
    public event Action<bool>? PauseChanged;
    public event Action? ExitRequested;

    public TrayIcon()
    {
        var menu = new ContextMenuStrip();

        var settingsItem = new ToolStripMenuItem("設定(&S)...");
        settingsItem.Click += (_, _) => OpenSettingsRequested?.Invoke();

        _pauseItem = new ToolStripMenuItem("一時停止(&P)") { CheckOnClick = true };
        _pauseItem.CheckedChanged += (_, _) => PauseChanged?.Invoke(_pauseItem.Checked);

        var exitItem = new ToolStripMenuItem("終了(&X)");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        menu.Items.Add(settingsItem);
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _icon = new NotifyIcon
        {
            Icon = AppIcon.Shared,
            Text = "Clemoutis",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => OpenSettingsRequested?.Invoke();
    }

    public void ShowInfo(string message)
    {
        _icon.ShowBalloonTip(3000, "Clemoutis", message, ToolTipIcon.Info);
    }

    public void SetPausedIndicator(bool paused)
    {
        _icon.Text = paused ? "Clemoutis(一時停止中)" : "Clemoutis";
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
