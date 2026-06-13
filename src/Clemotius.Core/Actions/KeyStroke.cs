namespace Clemotius.Core.Actions;

/// <summary>
/// 修飾キー付きの単一キー押下。仮想キーコードと修飾フラグを保持する。
/// </summary>
public readonly record struct KeyStroke(ushort VirtualKey, bool Ctrl, bool Shift, bool Alt, bool Win)
{
    /// <summary>"Ctrl+W" 等の表示文字列。</summary>
    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Ctrl) parts.Add("Ctrl");
        if (Shift) parts.Add("Shift");
        if (Alt) parts.Add("Alt");
        if (Win) parts.Add("Win");
        parts.Add(KeyNames.NameOf(VirtualKey));
        return string.Join('+', parts);
    }
}
