using Clemoutis.Actions;
using Clemoutis.Core.Config;
using Clemoutis.Core.Gestures;
using Clemoutis.Interop;

namespace Clemoutis.Gestures;

/// <summary>
/// 現在の設定とプロファイル解決を保持し、ジェスチャー開始位置から
/// 適用すべき <see cref="GestureContext"/> を組み立てる。設定リロード時は
/// <see cref="Update"/> で差し替える（マッチャのキャッシュも破棄）。
/// </summary>
internal sealed class ActiveConfigProvider : IGestureContextProvider
{
    private readonly object _gate = new();
    private ClemoutisConfig _config;
    private ProfileResolver _resolver;
    private readonly Dictionary<string, GestureMatcher> _matcherCache = new();

    public ActiveConfigProvider(ClemoutisConfig config)
    {
        _config = config;
        _resolver = new ProfileResolver(config.Profiles);
    }

    public int Range
    {
        get { lock (_gate) return _config.Gesture.Range; }
    }

    public void Update(ClemoutisConfig config)
    {
        lock (_gate)
        {
            _config = config;
            _resolver = new ProfileResolver(config.Profiles);
            _matcherCache.Clear();
        }
    }

    public GestureContext? Resolve(int startX, int startY)
    {
        // 開始位置直下のトップレベル窓のプロセス名でプロファイルを決める
        nint target = TargetWindowResolver.Resolve(startX, startY);
        string? process = ProcessNameResolver.FromWindow(target);

        lock (_gate)
        {
            var profile = _resolver.Resolve(process);
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
