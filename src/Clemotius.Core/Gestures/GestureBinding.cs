using Clemotius.Core.Actions;

namespace Clemotius.Core.Gestures;

/// <summary>
/// 1つのジェスチャー定義: ストローク列とアクションの対応。
/// ストロークは U/D/L/R の軌跡、または右ボタン+ホイールを表す <see cref="WheelStrokes.Up"/>/<see cref="WheelStrokes.Down"/>。
/// </summary>
public sealed record GestureBinding(string Strokes, GestureAction Action);
