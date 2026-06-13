namespace Clemotius.Core.Gestures;

/// <summary>ジェスチャーの基本ストローク方向。</summary>
public enum StrokeDirection
{
    Up,
    Down,
    Left,
    Right,
}

public static class StrokeDirectionExtensions
{
    /// <summary>U/D/L/R 1文字に変換（設定・表示用）。</summary>
    public static char ToChar(this StrokeDirection d) => d switch
    {
        StrokeDirection.Up => 'U',
        StrokeDirection.Down => 'D',
        StrokeDirection.Left => 'L',
        StrokeDirection.Right => 'R',
        _ => '?',
    };
}
