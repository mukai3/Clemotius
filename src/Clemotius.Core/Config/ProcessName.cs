namespace Clemotius.Core.Config;

/// <summary>
/// プロセス名の正規化を一箇所に集約する。プロファイル解決とジェスチャー除外判定で
/// 同一規則（前後空白を除去し、末尾の .exe を大文字小文字無視で取り除く）を共有する。
/// </summary>
public static class ProcessName
{
    /// <summary>前後空白を除去し、末尾 ".exe" を取り除いた名前を返す。null/空白は空文字。</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        string s = value.Trim();
        if (s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            s = s[..^4];
        return s;
    }
}
