using Clemoutis.Actions;
using Clemoutis.Config;
using Clemoutis.Gestures;
using Clemoutis.Hooks;
using Clemoutis.Tray;

namespace Clemoutis;

static class Program
{
    [STAThread]
    static void Main()
    {
        var instance = SingleInstance.TryAcquire();
        if (instance is null)
        {
            // 2つ目の起動: 既存インスタンスへ通知して即終了
            SingleInstance.SignalExisting();
            return;
        }

        using (instance)
        {
            ApplicationConfiguration.Initialize();
            using var ctx = new AppContext(instance);
            Application.Run(ctx);
        }
    }
}

/// <summary>
/// 常駐アプリのルート。フック・トレイ・監視タイマーの寿命を管理する。
/// </summary>
internal sealed class AppContext : ApplicationContext
{
    private readonly SingleInstance _instance;
    private readonly ModifierStateTracker _modifiers = new();
    private readonly MouseHook _mouseHook = new();
    private readonly KeyboardHook _keyboardHook = new();
    private readonly HookWatchdog _watchdog;
    private readonly TrayIcon _tray;
    private readonly ConfigStore _configStore;
    private readonly ActiveConfigProvider _configProvider;
    private readonly System.Windows.Forms.Timer _instancePollTimer;
    // FileSystemWatcher のイベントを UI スレッドへ載せるための隠しコントロール
    private readonly Control _marshal = new();

    public AppContext(SingleInstance instance)
    {
        _instance = instance;

        _ = _marshal.Handle; // ハンドルを生成して BeginInvoke 可能にする
        _configStore = new ConfigStore(_marshal);
        _configProvider = new ActiveConfigProvider(_configStore.Current);
        _configStore.Changed += cfg => _configProvider.Update(cfg);
        _configStore.Corrupted += OnConfigCorrupted;

        var gesture = new GestureEngine(_configProvider, new ActionExecutor());
        var router = new InputRouter(_modifiers, gesture);
        _mouseHook.Handler = router.OnMouse;
        _keyboardHook.Handler = router.OnKeyboard;
        _mouseHook.Install();
        _keyboardHook.Install();

        _watchdog = new HookWatchdog(
            [_mouseHook, _keyboardHook],
            onReinstalled: _modifiers.Reset);

        _tray = new TrayIcon();
        _tray.OpenSettingsRequested += OnOpenSettings;
        _tray.PauseChanged += OnPauseChanged;
        _tray.ExitRequested += OnExit;

        // 2つ目の起動からの「設定を開け」通知をUIスレッドでポーリング
        _instancePollTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _instancePollTimer.Tick += (_, _) =>
        {
            if (_instance.ConsumeShowSettingsRequest())
                OnOpenSettings();
        };
        _instancePollTimer.Start();
    }

    private void OnConfigCorrupted(string backupPath)
    {
        _tray.ShowInfo($"設定ファイルが壊れていたため既定設定で起動しました。\n退避先: {backupPath}");
    }

    private void OnOpenSettings()
    {
        // フェーズ5で SettingsForm に置き換える
        _tray.ShowInfo("設定画面はフェーズ5で実装予定です。");
    }

    private void OnPauseChanged(bool paused)
    {
        if (paused)
        {
            _mouseHook.Uninstall();
            _keyboardHook.Uninstall();
            _modifiers.Reset();
        }
        else
        {
            _mouseHook.Install();
            _keyboardHook.Install();
        }
        _tray.SetPausedIndicator(paused);
    }

    private void OnExit()
    {
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _instancePollTimer.Dispose();
            _watchdog.Dispose();
            _tray.Dispose();
            _mouseHook.Dispose();
            _keyboardHook.Dispose();
            _configStore.Dispose();
            _marshal.Dispose();
        }
        base.Dispose(disposing);
    }
}
