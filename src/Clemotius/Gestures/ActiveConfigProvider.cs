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
    private readonly Dictionary<string, GestureMatcher> _matcherCache = new();

    public ActiveConfigProvider(ClemotiusConfig config)
    {
        _config = config;
        _resolver = new ProfileResolver(config.Profiles);
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

        // リゾルバをスナップショットし、一致プロファイルだけを対象に項目判定を行う
        // （項目判定はフックスレッドを止めないよう非ブロッキング。ロック保持時間も最小化する）
        ProfileResolver resolver;
        lock (_gate)
            resolver = _resolver;

        // 一致するプロファイルが無いアプリ（旧グローバルの代替）では右ボタンを
        // 完全にアプリ側へ透過する（ジェスチャーを起動しない）
        var profile = resolver.Resolve(process);
        if (profile is null)
            return null;
        if (!profile.GesturesEnabled)
            return new GestureContext(EmptyMatcher, Enabled: false, WheelUp: null, WheelDown: null);

        // ファイル/フォルダ等の項目の上では右ボタンをアプリへ透過し、アプリ独自の右ドラッグを保つ。
        // 項目の無い背景上ではジェスチャーを扱う（エクスプローラ等での両立）。
        if (RightDragItemDetector.IsOverDraggableItem(startX, startY))
            return null;

        lock (_gate)
        {
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
