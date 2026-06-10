using Clemoutis.Core.Actions;

namespace Clemoutis.Core.Gestures;

/// <summary>
/// ストローク列をジェスチャー定義に完全一致でマッチングする。Win32 非依存。
/// </summary>
public sealed class GestureMatcher
{
    private readonly Dictionary<string, GestureAction> _map;

    public GestureMatcher(IEnumerable<GestureBinding> bindings)
    {
        _map = new Dictionary<string, GestureAction>(StringComparer.Ordinal);
        foreach (var b in bindings)
            _map[b.Strokes] = b.Action; // 後勝ち（重複定義は後者を採用）
    }

    /// <summary>ストローク列に一致するアクションを返す。無ければ null。</summary>
    public GestureAction? Match(string strokes)
        => _map.TryGetValue(strokes, out var action) ? action : null;
}
