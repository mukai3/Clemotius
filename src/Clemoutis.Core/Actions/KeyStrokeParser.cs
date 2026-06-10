namespace Clemoutis.Core.Actions;

/// <summary>
/// "Ctrl+Shift+Tab" のようなキー文字列を <see cref="KeyStroke"/> に変換する。
/// 修飾キー（Ctrl/Shift/Alt/Win）は順不同。最後の非修飾トークンを主キーとする。
/// </summary>
public static class KeyStrokeParser
{
    public static KeyStroke Parse(string text)
    {
        if (!TryParse(text, out var stroke, out var error))
            throw new FormatException(error);
        return stroke;
    }

    public static bool TryParse(string text, out KeyStroke stroke, out string error)
    {
        stroke = default;
        error = "";

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "キー文字列が空です。";
            return false;
        }

        bool ctrl = false, shift = false, alt = false, win = false;
        ushort? key = null;

        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control":
                    ctrl = true;
                    break;
                case "shift":
                    shift = true;
                    break;
                case "alt":
                    alt = true;
                    break;
                case "win" or "windows":
                    win = true;
                    break;
                default:
                    if (key is not null)
                    {
                        error = $"主キーが複数あります: '{raw}'。";
                        return false;
                    }
                    if (!KeyNames.TryGetVk(raw, out var vk))
                    {
                        error = $"未知のキー名です: '{raw}'。";
                        return false;
                    }
                    key = vk;
                    break;
            }
        }

        if (key is null)
        {
            error = "主キーがありません（修飾キーのみ）。";
            return false;
        }

        stroke = new KeyStroke(key.Value, ctrl, shift, alt, win);
        return true;
    }
}
