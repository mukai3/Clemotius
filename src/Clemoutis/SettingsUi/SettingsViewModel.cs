using System.Windows.Threading;
using Clemoutis.Core.Config;

namespace Clemoutis.SettingsUi;

/// <summary>
/// 設定画面のルート ViewModel。編集用の作業状態を保持し、変更を
/// デバウンス（300ms）してから <see cref="Applied"/> で確定済みの
/// <see cref="ClemoutisConfig"/> を通知する（即時適用方式）。
///
/// 数値スピンやスライダー連打でファイル保存が暴れないようにするのが
/// デバウンスの目的。フライアウト/ダイアログ系の編集は確定ボタン側で
/// <see cref="NotifyChanged"/> を1回呼ぶ。
/// </summary>
public sealed class SettingsViewModel
{
    private readonly ClemoutisConfig _original;
    private readonly DispatcherTimer _debounce;

    /// <summary>デバウンス確定時に再構築済みの設定を通知する（ConfigStore.Save へ接続）。</summary>
    public event Action<ClemoutisConfig>? Applied;

    public SettingsViewModel(ClemoutisConfig config)
    {
        _original = config;
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _debounce.Tick += (_, _) => Flush();
    }

    /// <summary>編集項目が変化したときに各ページから呼ぶ。300ms 静止後に保存される。</summary>
    public void NotifyChanged()
    {
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
    }

    /// <summary>
    /// 現在の編集状態から設定を再構築する。
    /// フェーズ2以降、ページの作業状態を追加するたびにここへ反映する。
    /// </summary>
    public ClemoutisConfig Build() => _original;
}
