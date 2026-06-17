using System.Windows.Threading;
using Clemotius.Core.Config;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clemotius.SettingsUi;

/// <summary>
/// 設定画面のルート ViewModel。編集用の作業状態を保持し、変更を
/// デバウンス（300ms）してから <see cref="Applied"/> で確定済みの
/// <see cref="ClemotiusConfig"/> を通知する（即時適用方式）。
///
/// 数値スピンやスライダー連打でファイル保存が暴れないようにするのが
/// デバウンスの目的。フライアウト/ダイアログ系の編集は確定ボタン側で
/// <see cref="NotifyChanged"/> を1回呼ぶ。
/// </summary>
internal sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ClemotiusConfig _original;
    private readonly DispatcherTimer _debounce;
    private readonly DispatcherTimer _statusClear;

    /// <summary>デバウンス確定時に再構築済みの設定を通知する（ConfigStore.Save へ接続）。</summary>
    public event Action<ClemotiusConfig>? Applied;

    /// <summary>保存状態の表示文言（即時適用のフィードバック）。空文字なら非表示。</summary>
    [ObservableProperty] private string _statusText = "";

    // ── 拡張スクロール: 修飾キー6種 ──
    public IReadOnlyList<ChoiceSlotViewModel> ModifierSlots { get; }

    // ── ホイール: スクロールバー上での動作（[0]=垂直, [1]=水平）──
    public IReadOnlyList<ChoiceSlotViewModel> WheelBarSlots { get; }

    // ── ウィンドウ: タイトルバーアクション6種＋不透明度 ──
    public IReadOnlyList<ChoiceSlotViewModel> TitlebarSlots { get; }

    [ObservableProperty] private int _windowOpacity;
    partial void OnWindowOpacityChanged(int value) => NotifyChanged();

    // ── 一般: トレイ＋ジェスチャー詳細 ──
    public GeneralViewModel General { get; }

    // ── ジェスチャー: プロファイル＋一覧＋右ボタンホイール ──
    public GestureViewModel Gesture { get; }

    public SettingsViewModel(ClemotiusConfig config)
    {
        _original = config;
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _debounce.Tick += (_, _) => Flush();

        // 「保存しました」を数秒後に消すためのタイマー。
        _statusClear = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _statusClear.Tick += (_, _) => { _statusClear.Stop(); StatusText = ""; };

        var ms = config.Scroll.ModifierScroll;
        ModifierSlots = new[]
        {
            Slot("Shift", "Shift + ホイール", ScrollBehaviorChoice.Modifier, ms.Shift),
            Slot("Ctrl", "Ctrl + ホイール", ScrollBehaviorChoice.Modifier, ms.Ctrl),
            Slot("CtrlShift", "Ctrl + Shift + ホイール", ScrollBehaviorChoice.Modifier, ms.CtrlShift),
            Slot("Alt", "Alt + ホイール", ScrollBehaviorChoice.Modifier, ms.Alt),
            Slot("ShiftAlt", "Shift + Alt + ホイール", ScrollBehaviorChoice.Modifier, ms.ShiftAlt),
            Slot("CtrlAlt", "Ctrl + Alt + ホイール", ScrollBehaviorChoice.Modifier, ms.CtrlAlt),
        };

        WheelBarSlots = new[]
        {
            Slot("V", "垂直スクロールバー上でホイール回転", ScrollBehaviorChoice.VerticalBar, config.Scroll.OnVerticalScrollbar),
            Slot("H", "水平スクロールバー上でホイール回転", ScrollBehaviorChoice.HorizontalBar, config.Scroll.OnHorizontalScrollbar),
        };

        var tb = config.Titlebar;
        TitlebarSlots = new[]
        {
            Slot("ShiftClick", "タイトルバーを Shift+クリック", TitlebarActionChoice.All, tb.ShiftClick),
            Slot("CtrlClick", "タイトルバーを Ctrl+クリック", TitlebarActionChoice.All, tb.CtrlClick),
            Slot("RightClick", "タイトルバーを右クリック", TitlebarActionChoice.All, tb.RightClick),
            Slot("MiddleClick", "タイトルバーを中央クリック", TitlebarActionChoice.All, tb.MiddleClick),
            Slot("MinButtonRightClick", "最小化ボタンを右クリック", TitlebarActionChoice.All, tb.MinButtonRightClick),
            Slot("CloseButtonRightClick", "閉じるボタンを右クリック", TitlebarActionChoice.All, tb.CloseButtonRightClick),
        };
        _windowOpacity = Math.Clamp(tb.WindowOpacity, 10, 90);

        General = new GeneralViewModel(config, NotifyChanged);
        Gesture = new GestureViewModel(config.Profiles, NotifyChanged);
    }

    private ChoiceSlotViewModel Slot(
        string key, string label, IReadOnlyList<ScrollBehaviorChoice.Choice> choices, string initial)
        => new(key, label, choices, initial, NotifyChanged);

    /// <summary>編集項目が変化したときに呼ばれる。300ms 静止後に保存される。</summary>
    public void NotifyChanged()
    {
        _statusClear.Stop();
        StatusText = "変更を保存中…";
        _debounce.Stop();
        _debounce.Start();
    }

    /// <summary>保留中の変更があれば即時確定する（ウィンドウクローズ時など）。</summary>
    public void FlushPending()
    {
        if (_debounce.IsEnabled)
            Flush();
    }

    private void Flush()
    {
        _debounce.Stop();
        Applied?.Invoke(Build());
        StatusText = "保存しました";
        _statusClear.Stop();
        _statusClear.Start();
    }

    /// <summary>現在の編集状態から設定を再構築する（プロファイルはフェーズ3で追加）。</summary>
    public ClemotiusConfig Build()
    {
        var scroll = _original.Scroll with
        {
            OnVerticalScrollbar = WheelBarSlots[0].Value,
            OnHorizontalScrollbar = WheelBarSlots[1].Value,
            ModifierScroll = new ModifierScrollSettings
            {
                Shift = SlotValue(ModifierSlots, "Shift"),
                Ctrl = SlotValue(ModifierSlots, "Ctrl"),
                CtrlShift = SlotValue(ModifierSlots, "CtrlShift"),
                Alt = SlotValue(ModifierSlots, "Alt"),
                ShiftAlt = SlotValue(ModifierSlots, "ShiftAlt"),
                CtrlAlt = SlotValue(ModifierSlots, "CtrlAlt"),
            },
        };

        var titlebar = new TitlebarSettings
        {
            ShiftClick = SlotValue(TitlebarSlots, "ShiftClick"),
            CtrlClick = SlotValue(TitlebarSlots, "CtrlClick"),
            RightClick = SlotValue(TitlebarSlots, "RightClick"),
            MiddleClick = SlotValue(TitlebarSlots, "MiddleClick"),
            MinButtonRightClick = SlotValue(TitlebarSlots, "MinButtonRightClick"),
            CloseButtonRightClick = SlotValue(TitlebarSlots, "CloseButtonRightClick"),
            WindowOpacity = WindowOpacity,
        };

        var gesture = _original.Gesture with
        {
            Range = General.Range,
            TimeoutMs = General.TimeoutMs,
            PushHoldTimeMs = General.PushHoldTimeMs,
            DrawStroke = General.DrawStroke,
            DrawingType = General.DrawingType,
            StrokeWidth = General.StrokeWidth,
            ValidStrokeColor = General.ValidStrokeColor,
            InvalidStrokeColor = General.InvalidStrokeColor,
        };

        var tray = _original.Tray with
        {
            ShowTrayIcon = General.ShowTrayIcon,
            ShowBalloonTip = General.ShowBalloonTip,
        };

        return _original with
        {
            Scroll = scroll,
            Titlebar = titlebar,
            Gesture = gesture,
            Tray = tray,
            Profiles = Gesture.BuildProfiles(),
        };
    }

    private static string SlotValue(IReadOnlyList<ChoiceSlotViewModel> slots, string key)
        => slots.First(s => s.Key == key).Value;
}
