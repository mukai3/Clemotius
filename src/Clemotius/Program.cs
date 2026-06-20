using Clemotius.Actions;
using Clemotius.Config;
using Clemotius.Gestures;
using Clemotius.Hooks;
using Clemotius.Scroll;
using Clemotius.Tray;
using Clemotius.Windowing;

namespace Clemotius;

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
            // WPF オーバーレイ/設定画面用に Application.Current を用意（WinForms 主体のため Run はしない）
            var wpfApp = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown,
            };
            // WPF-UI のテーマ（設定画面 SettingsUi で使用）。オーバーレイは独自描画のため影響なし
            wpfApp.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary());
            wpfApp.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
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
    private readonly ScrollEnhancer _scroll;
    private readonly TitlebarActionHandler _titlebar;
    private readonly GestureWpfOverlay _trail = new();
    private readonly GestureCommandOverlay _command = new();
    private volatile bool _drawTrail;
    private volatile bool _commandMode; // true=コマンド表示, false=軌跡
    private int _lastX;
    private int _lastY;
    private readonly System.Windows.Forms.Timer _instancePollTimer;
    // FileSystemWatcher のイベントを UI スレッドへ載せるための隠しコントロール
    private readonly Control _marshal = new();

    public AppContext(SingleInstance instance)
    {
        _instance = instance;

        _ = _marshal.Handle; // ハンドルを生成して BeginInvoke 可能にする
        _configStore = new ConfigStore(_marshal);
        _configProvider = new ActiveConfigProvider(_configStore.Current);
        _scroll = new ScrollEnhancer(_modifiers, _configStore.Current.Scroll);
        _titlebar = new TitlebarActionHandler(
            _modifiers, new WindowActionExecutor(), _configStore.Current.Titlebar);
        _titlebar.UpdateSettings(_configStore.Current.Titlebar); // 不透明度を反映
        _configStore.Changed += OnConfigChanged;
        _configStore.Corrupted += OnConfigCorrupted;

        _trail.ApplySettings(_configStore.Current.Gesture);
        _drawTrail = _configStore.Current.Gesture.DrawStroke;
        _commandMode = _configStore.Current.Gesture.DrawingType == 1;

        var gesture = new GestureEngine(_configProvider, new ActionExecutor());
        // 表示イベントはフックスレッドから来るので UI スレッドへマーシャルする。
        // drawingType に応じて軌跡（フルスクリーン）かコマンド（カーソル付近）を出す。
        gesture.GestureStarted += (x, y) => RunOnUi(() =>
        {
            _lastX = x; _lastY = y;
            if (_drawTrail && !_commandMode) _trail.Begin(x, y);
        });
        gesture.GesturePoint += (x, y) => RunOnUi(() =>
        {
            _lastX = x; _lastY = y;
            if (_drawTrail && !_commandMode) _trail.AddPoint(x, y);
        });
        gesture.GestureProgress += (strokes, action) => RunOnUi(() =>
        {
            if (_drawTrail && _commandMode)
                _command.ShowText(FormatProgress(strokes, action), _lastX, _lastY);
        });
        gesture.GestureEnded += () => RunOnUi(() => { _trail.End(); _command.HideText(); });

        var router = new InputRouter(_modifiers, gesture, _scroll, _titlebar);
        _mouseHook.Handler = router.OnMouse;
        _keyboardHook.Handler = router.OnKeyboard;
        _mouseHook.Install();
        _keyboardHook.Install();

        _watchdog = new HookWatchdog(
            _mouseHook, _keyboardHook,
            onReinstalled: _modifiers.Reset);

        _tray = new TrayIcon();
        _tray.OpenSettingsRequested += OnOpenSettings;
        _tray.PauseChanged += OnPauseChanged;
        _tray.ExitRequested += OnExit;
        _tray.DoubleClicked += OnTrayDoubleClick;

        // 2つ目の起動からの「設定を開け」通知をUIスレッドでポーリング
        _instancePollTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _instancePollTimer.Tick += (_, _) =>
        {
            if (_instance.ConsumeShowSettingsRequest())
                OnOpenSettings();
        };
        _instancePollTimer.Start();
    }

    private void OnConfigChanged(Clemotius.Core.Config.ClemotiusConfig cfg)
    {
        _configProvider.Update(cfg);
        _scroll.UpdateSettings(cfg.Scroll);
        _titlebar.UpdateSettings(cfg.Titlebar);
        _drawTrail = cfg.Gesture.DrawStroke;
        _commandMode = cfg.Gesture.DrawingType == 1;
        _trail.ApplySettings(cfg.Gesture);
    }

    // フックスレッドからの呼び出しを UI スレッドへ載せる（隠しコントロール経由）
    private void RunOnUi(Action action)
    {
        if (_marshal.IsHandleCreated)
            _marshal.BeginInvoke(action);
    }

    // コマンド表示用テキスト: ストロークを矢印化し、成立コマンドがあれば併記（例 "↓→ Close"）
    private static string FormatProgress(string strokes, Clemotius.Core.Actions.GestureAction? action)
    {
        var arrows = strokes.Select(c => c switch
        {
            'U' => '↑', 'D' => '↓', 'L' => '←', 'R' => '→', _ => c,
        }).ToArray();
        string s = new(arrows);
        return action is null ? s : $"{s}  {SettingsUi.ActionDisplay.ShortLabel(action)}";
    }

    private void OnConfigCorrupted(string backupPath)
    {
        _tray.ShowInfo($"設定ファイルが壊れていたため既定設定で起動しました。\n退避先: {backupPath}");
    }

    private SettingsUi.SettingsWindow? _wpfSettings;

    private void OnOpenSettings()
    {
        if (_wpfSettings is not null)
        {
            _wpfSettings.BringToFront();
            return;
        }
        _wpfSettings = new SettingsUi.SettingsWindow(_configStore.Current);
        // 本アプリは WinForms 主体（Application.Run）で、設定ウィンドウは WPF をモードレス Show する。
        // この構成では WPF の入力系へキーメッセージが橋渡しされず、WM_CHAR を要する直接タイプ
        // （IME-OFF）だけ文字にならない（IME/Ctrl+V/クリックは別経路で成立）。これを有効化して
        // モードレス WPF ウィンドウでも通常のキー入力が効くようにする。
        System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(_wpfSettings);
        _wpfSettings.ViewModel.Applied += cfg => _configStore.Save(cfg);
        _wpfSettings.Closed += (_, _) =>
        {
            _wpfSettings?.ViewModel.FlushPending();
            _wpfSettings = null;
        };
        _wpfSettings.Show();
        _wpfSettings.BringToFront();
    }

    // トレイアイコンのダブルクリック: 設定に応じて「設定を開く」か「一時停止の切り替え」。
    private void OnTrayDoubleClick()
    {
        if (_configStore.Current.Tray.DoubleClickAction == 1)
            _tray.TogglePause();
        else
            OnOpenSettings();
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
            _trail.Close();
            _command.Close();
            _marshal.Dispose();
        }
        base.Dispose(disposing);
    }
}
