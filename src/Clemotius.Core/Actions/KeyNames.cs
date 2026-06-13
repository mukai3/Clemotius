namespace Clemotius.Core.Actions;

/// <summary>
/// キー名と仮想キーコード(VK)の相互変換。設定ファイルやUIで使う名前を扱う。
/// 網羅は最小限から始め、必要に応じて拡張する。
/// </summary>
public static class KeyNames
{
    private static readonly Dictionary<string, ushort> NameToVk =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<ushort, string> VkToName = new();

    static KeyNames()
    {
        // A-Z（VK は ASCII 大文字に一致）
        for (char c = 'A'; c <= 'Z'; c++)
            Register(c.ToString(), c);
        // 0-9（VK は ASCII 数字に一致）
        for (char c = '0'; c <= '9'; c++)
            Register(c.ToString(), c);
        // ファンクションキー F1-F24（VK_F1 = 0x70）
        for (int i = 1; i <= 24; i++)
            Register($"F{i}", (ushort)(0x70 + (i - 1)));

        Register("Tab", 0x09);
        Register("Enter", 0x0D);
        Register("Esc", 0x1B);
        Register("Escape", 0x1B);
        Register("Space", 0x20);
        Register("PageUp", 0x21);
        Register("PageDown", 0x22);
        Register("End", 0x23);
        Register("Home", 0x24);
        Register("Left", 0x25);
        Register("Up", 0x26);
        Register("Right", 0x27);
        Register("Down", 0x28);
        Register("Insert", 0x2D);
        Register("Delete", 0x2E);
        Register("Back", 0x08);
        Register("Backspace", 0x08);
    }

    private static void Register(string name, ushort vk)
    {
        NameToVk[name] = vk;
        // 先に登録した名前を正規表示名とする（別名は上書きしない）
        VkToName.TryAdd(vk, name);
    }

    public static bool TryGetVk(string name, out ushort vk)
        => NameToVk.TryGetValue(name.Trim(), out vk);

    public static string NameOf(ushort vk)
        => VkToName.TryGetValue(vk, out var name) ? name : $"VK_0x{vk:X2}";
}
