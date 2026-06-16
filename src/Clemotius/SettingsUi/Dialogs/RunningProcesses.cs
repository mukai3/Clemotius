using System.Diagnostics;
using Clemotius.Core.Config;

namespace Clemotius.SettingsUi.Dialogs;

/// <summary>選択候補となる実行中プロセス1件（正規化済み名＋代表ウィンドウタイトル）。</summary>
internal sealed record RunningProcess(string Name, string Title);

/// <summary>現在実行中のプロセスを列挙する。Win32 直接呼び出しは行わず System.Diagnostics に依存。</summary>
internal static class RunningProcesses
{
    /// <summary>
    /// 可視のメインウィンドウを持つプロセスを、正規化済みプロセス名で重複排除して返す。
    /// 自プロセスとアクセス不可プロセスは除外し、プロセス名の昇順で並べる。
    /// </summary>
    public static IReadOnlyList<RunningProcess> WithVisibleWindows()
    {
        int self = Environment.ProcessId;
        var map = new Dictionary<string, RunningProcess>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.Id == self || p.MainWindowHandle == IntPtr.Zero)
                    continue;
                string title = p.MainWindowTitle;
                if (string.IsNullOrWhiteSpace(title))
                    continue;
                string name = ProcessName.Normalize(p.ProcessName);
                if (name.Length > 0 && !map.ContainsKey(name))
                    map[name] = new RunningProcess(name, title);
            }
            catch
            {
                // アクセス権限などで情報を取得できないプロセスは候補から除外
            }
            finally
            {
                p.Dispose();
            }
        }
        return map.Values.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
