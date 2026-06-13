namespace Clemotius.Core.Gestures;

/// <summary>
/// マウス移動の座標列を方向ストローク列にエンコードする。Win32 非依存のピュアロジック。
/// しきい値 <see cref="Range"/> を超えた移動のみを方向として確定し、
/// 同方向の連続は1ストロークに集約する（オリジナルの GestureRange 相当）。
/// </summary>
public sealed class StrokeEncoder
{
    private readonly int _range;
    private readonly List<StrokeDirection> _strokes = new();

    private bool _hasAnchor;
    private int _anchorX;
    private int _anchorY;

    public StrokeEncoder(int range)
    {
        if (range <= 0)
            throw new ArgumentOutOfRangeException(nameof(range), "range は正の値が必要です。");
        _range = range;
    }

    /// <summary>これまでに確定したストローク列。</summary>
    public IReadOnlyList<StrokeDirection> Strokes => _strokes;

    /// <summary>1つでもストロークが確定しているか。</summary>
    public bool HasStrokes => _strokes.Count > 0;

    /// <summary>新しいジェスチャー開始。基準点を置く。</summary>
    public void Begin(int x, int y)
    {
        _strokes.Clear();
        _anchorX = x;
        _anchorY = y;
        _hasAnchor = true;
    }

    /// <summary>
    /// カーソル位置を与え、しきい値を超えていれば方向を確定する。
    /// </summary>
    /// <returns>新しいストロークが追加されたら true。</returns>
    public bool Add(int x, int y)
    {
        if (!_hasAnchor)
            return false;

        int dx = x - _anchorX;
        int dy = y - _anchorY;

        if (Math.Abs(dx) < _range && Math.Abs(dy) < _range)
            return false; // まだ移動が小さい

        // 支配的な軸で方向を決める
        StrokeDirection dir = Math.Abs(dx) >= Math.Abs(dy)
            ? (dx > 0 ? StrokeDirection.Right : StrokeDirection.Left)
            : (dy > 0 ? StrokeDirection.Down : StrokeDirection.Up);

        // 基準点を現在位置へ進める（次のストロークを相対判定）
        _anchorX = x;
        _anchorY = y;

        // 同方向の連続は集約
        if (_strokes.Count > 0 && _strokes[^1] == dir)
            return false;

        _strokes.Add(dir);
        return true;
    }

    /// <summary>確定済みストローク列を U/D/L/R 文字列にする（例 "DR"）。</summary>
    public string ToStrokeString()
    {
        return string.Create(_strokes.Count, _strokes, static (span, src) =>
        {
            for (int i = 0; i < src.Count; i++)
                span[i] = src[i].ToChar();
        });
    }

    public void Reset()
    {
        _strokes.Clear();
        _hasAnchor = false;
    }
}
