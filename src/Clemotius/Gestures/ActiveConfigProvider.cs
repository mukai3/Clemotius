using Clemotius.Actions;
using Clemotius.Core.Config;
using Clemotius.Core.Gestures;
using Clemotius.Interop;

namespace Clemotius.Gestures;

/// <summary>
/// 現在の設定とプロファイル解決を保持し、ジェスチャー開始位置から
/// 適用すべき <see cref="GestureContext"/> を組み立てる。設定リロード時は
/// <see cref="Update"/> で差し替える（マッチャのキャッシュも破棄）。
/// </summary>
internal sealed class ActiveConfigProvider : IGestureContextProvider
{
    private readonly object _gate = new();
    private ClemotiusConfig _config;
    private ProfileResolver _resolver;
    private HashSet<string> _excluded;
    private readonly Dictionary<string, GestureMatcher> _matcherCache = new();

    public ActiveConfigProvider(ClemotiusConfig config)
    {
        _config = config;
        _resolver = new ProfileResolver(config.Profiles);
        _excluded = BuildExcluded(config);
    }

    // 除外プロセス名を正規化した集合にする（大文字小文字・拡張子を無視して照合）
    private static HashSet<string> BuildExcluded(ClemotiusConfig config)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in config.Gesture.ExcludedProcesses)
        {
            string n = ProcessName.Normalize(name);
            if (n.Length > 0)
                set.Add(n);
        }
        return set;
    }

    public int Range
    {
        get { lock (_gate) return _config.Gesture.Range; }
    }

    public int TimeoutMs
    {
        get { lock (_gate) return _config.Gesture.TimeoutMs; }
    }

    public void Update(ClemotiusConfig config)
    {
        lock (_gate)
        {
            _config = config;
            _resolver = new ProfileResolver(config.Profiles);
            _excluded = BuildExcluded(config);
            _matcherCache.Clear();
        }
    }

    private static readonly uint OwnProcessId = (uint)Environment.ProcessId;

    public GestureContext? Resolve(int startX, int startY)
    {
        // 開始位置直下のトップレベル窓のプロセス名でプロファイルを決める
        nint target = TargetWindowResolver.Resolve(startX, startY);

        // 自アプリ（設定画面・ストローク入力ダイアログ等）の上ではジェスチャーを無効化し、
        // 右ドラッグをダイアログ側の操作として通す
        InputNative.GetWindowThreadProcessId(target, out uint pid);
        if (pid == OwnProcessId)
            return null;

        string? process = ProcessNameResolver.FromWindow(target);

        lock (_gate)
        {
            // 除外登録アプリでは右ボタンを完全にアプリ側へ透過する（ジェスチャーを起動しない）
            if (process is not null && _excluded.Contains(ProcessName.Normalize(process)))
                return null;

            var profile = _resolver.ResolveEffective(process);
            if (profile is null)
                return null;
            if (!profile.GesturesEnabled)
                return new GestureContext(EmptyMatcher, Enabled: false, WheelUp: null, WheelDown: null);

            if (!_matcherCache.TryGetValue(profile.Name, out var matcher))
            {
                matcher = new GestureMatcher(profile.Gestures);
                _matcherCache[profile.Name] = matcher;
            }
            return new GestureContext(matcher, Enabled: true, profile.WheelUp, profile.WheelDown);
        }
    }

    private static readonly GestureMatcher EmptyMatcher = new(Array.Empty<GestureBinding>());
}
