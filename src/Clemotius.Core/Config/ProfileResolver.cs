using Clemotius.Core.Gestures;

namespace Clemotius.Core.Config;

/// <summary>
/// 前面アプリのプロセス名から適用プロファイルを決定する。Win32 非依存。
/// マッチ規則: ProcessPattern がワイルドカード "*" 以外で、プロセス名に
/// （拡張子 .exe を無視して）一致するものを優先。ProcessPattern はカンマ区切りで
/// 複数のプロセス名を指定でき、いずれかに一致すれば該当（例 "chrome, edge, brave"）。
/// 複数プロファイルが該当する場合は定義順で先勝ち。該当が無ければ "*"（既定）。
/// </summary>
public sealed class ProfileResolver
{
    private readonly IReadOnlyList<GestureProfile> _profiles;

    public ProfileResolver(IReadOnlyList<GestureProfile> profiles)
    {
        _profiles = profiles;
    }

    public GestureProfile? Resolve(string? processName)
    {
        string name = NormalizeProcess(processName);

        GestureProfile? wildcard = null;
        foreach (var p in _profiles)
        {
            if (p.ProcessPattern == "*")
            {
                wildcard ??= p; // 最初の "*" を既定として温存
                continue;
            }
            if (Matches(p.ProcessPattern, name))
                return p; // 具体的なパターンを優先
        }
        return wildcard;
    }

    /// <summary>
    /// グローバル("*")プロファイルをベースに、アプリ別プロファイルをマージした
    /// 「実効プロファイル」を返す。アプリ別が同じストロークを定義していれば上書きし、
    /// 右+ホイールはアプリ別が未設定ならグローバルを引き継ぐ。GesturesEnabled は
    /// アプリ別が一致すればアプリ別を、無ければグローバルを採用する。
    /// </summary>
    public GestureProfile? ResolveEffective(string? processName)
    {
        string name = NormalizeProcess(processName);
        GestureProfile? global = null;
        GestureProfile? specific = null;
        foreach (var p in _profiles)
        {
            if (p.ProcessPattern == "*")
                global ??= p;
            else if (specific is null && Matches(p.ProcessPattern, name))
                specific = p;
        }

        if (specific is null) return global;
        if (global is null) return specific;

        // グローバルのジェスチャーをベースに、アプリ別で上書き・追加
        var merged = new Dictionary<string, GestureBinding>(StringComparer.Ordinal);
        foreach (var g in global.Gestures) merged[g.Strokes] = g;
        foreach (var g in specific.Gestures) merged[g.Strokes] = g;

        return specific with
        {
            Gestures = merged.Values.ToArray(),
            WheelUp = specific.WheelUp ?? global.WheelUp,
            WheelDown = specific.WheelDown ?? global.WheelDown,
        };
    }

    private static bool Matches(string pattern, string processName)
    {
        // カンマ区切りの各プロセス名のいずれかに一致すれば真
        foreach (var part in pattern.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(NormalizeProcess(part), processName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string NormalizeProcess(string? value) => ProcessName.Normalize(value);
}
