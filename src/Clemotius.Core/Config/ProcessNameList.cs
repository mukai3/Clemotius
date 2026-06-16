namespace Clemotius.Core.Config;

/// <summary>
/// カンマ区切りのプロセス名テキスト（プロファイルの対象プロセスやジェスチャー除外リスト）の
/// 解析・整形・マージを一箇所に集約する。正規化は <see cref="ProcessName.Normalize"/> に従う
/// （前後空白除去・末尾 .exe 除去）。比較は大文字小文字を無視し、出現順を保って重複を除く。
/// </summary>
public static class ProcessNameList
{
    /// <summary>カンマ区切りテキストを正規化済みプロセス名の一覧にする（空要素・重複を除外）。</summary>
    public static IReadOnlyList<string> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];
        return Dedupe(text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>プロセス名の並びを正規化・重複除去して "a, b, c" 形式の表示テキストにする。</summary>
    public static string Format(IEnumerable<string> names) => string.Join(", ", Dedupe(names));

    /// <summary>既存テキストへ追加分を結合した表示テキストを返す（既存を先、重複は除外）。</summary>
    public static string Merge(string? existingText, IEnumerable<string> additions)
        => Format(Parse(existingText).Concat(additions));

    private static List<string> Dedupe(IEnumerable<string> names)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var raw in names)
        {
            string name = ProcessName.Normalize(raw);
            if (name.Length > 0 && seen.Add(name))
                result.Add(name);
        }
        return result;
    }
}
