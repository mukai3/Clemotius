using Clemotius.Core.Actions;
using Clemotius.Core.Gestures;

namespace Clemotius.Gestures;

/// <summary>
/// 1回のジェスチャー開始時に確定する文脈。どのプロファイルのマッチャを使うか、
/// そのプロファイルでジェスチャーが有効か、右+ホイールに割り当てたアクションを保持する。
/// </summary>
internal sealed record GestureContext(
    GestureMatcher Matcher,
    bool Enabled,
    GestureAction? WheelUp,
    GestureAction? WheelDown);

/// <summary>
/// ジェスチャー開始位置からプロファイル文脈を解決する。
/// 戻り値 null は「対象なし＝通常の右クリックとして扱う」を意味する。
/// </summary>
internal interface IGestureContextProvider
{
    GestureContext? Resolve(int startX, int startY);

    /// <summary>
    /// マウス移動中の事前判定。カーソル下がプロファイル一致ウィンドウなら、項目（ファイル/フォルダ）
    /// 判定をバックグラウンドで温めておき、右DOWN時に即答できるようにする（フックスレッドは待たない）。
    /// </summary>
    void Prime(int startX, int startY);

    /// <summary>ストローク確定のしきい値（gesture.range）。</summary>
    int Range { get; }

    /// <summary>入力開始のタイムアウト（ms）。0 以下で無効。gesture.timeoutMs。</summary>
    int TimeoutMs { get; }
}
