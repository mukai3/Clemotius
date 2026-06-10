using Clemoutis.Core.Actions;

namespace Clemoutis.Core.Gestures;

/// <summary>
/// 1つのジェスチャー定義: ストローク列（U/D/L/R 文字列）とアクションの対応。
/// </summary>
public sealed record GestureBinding(string Strokes, GestureAction Action);
