using System.Runtime.InteropServices;
using Accessibility;
using Clematius.Interop;

namespace Clematius.Scroll;

/// <summary>カーソル直下のスクロールバーの向き。</summary>
internal enum ScrollBarHit
{
    None,
    Horizontal,
    Vertical,
}

/// <summary>
/// カーソル直下がスクロールバーかどうか、およびその向きを判定する。4段構え:
///   1. 単独スクロールバー（独立した "ScrollBar" コントロール）: クラス名＋スタイルで判定
///   2. 標準スクロールバー（ウィンドウ非クライアントの WS_HSCROLL/WS_VSCROLL）: WM_NCHITTEST で判定
///   3. カスタム描画スクロールバー: MSAA の ROLE_SYSTEM_SCROLLBAR
///   4. Chromium 系: MSAA は a11y 既定無効でスタブしか返さないため、UIA の
///      ScrollPattern 要素＋端帯ジオメトリ（<see cref="ScrollBarBand"/>）で判定。
///      スクロール自体は WM_VSCROLL/WM_HSCROLL が Chromium にも効くことを実測確認済み。
/// </summary>
internal static class ScrollBarDetector
{
    private const int GWL_STYLE = -16;
    private const int SBS_VERT = 0x0001; // 立っていれば垂直（単独スクロールバー）

    // 検出はクロスプロセス呼び出し（NCHITTEST/MSAA/UIA）を含み、相手がビジーだと
    // 長くブロックしうるため、連続ホイール中は近傍・短時間の結果を再利用する。
    // 参照の差し替えがアトミックになるよう不変レコードで保持する
    // （フックスレッドとバックグラウンド検出スレッドの両方から書くため）。
    // Wheel=true は「対象が WM_VSCROLL を受け付けないカスタムバー（Excel 等の MSAA 検出分）なので、
    // WM_MOUSEWHEEL/WM_MOUSEHWHEEL で送る」ことを示す。標準バー/Chromium(UIA) は false（WM_SCROLL）。
    private sealed record CacheEntry(int X, int Y, ScrollBarHit Hit, nint Target, bool Wheel, uint Tick);

    private static volatile CacheEntry? _cache;

    // カスタムバー検出のオン/オフ（設定由来）。標準バー（クラス名 / NCHITTEST）は常に検出するが、
    // 重いクロスプロセス検出（MSAA=Office / UIA=Chromium）と先読み（Prime/settle）はここで制御する。
    // 両方 false なら Detect/Prime はホットパスで一切のカスタム検出・先読みを行わない（負荷ゼロ）。
    private static volatile bool _detectOffice;
    private static volatile bool _detectBrowser;

    /// <summary>カスタムバー検出の有効/無効を設定する（設定変更時に <see cref="ScrollEnhancer"/> から呼ぶ）。</summary>
    public static void Configure(bool office, bool browser)
    {
        _detectOffice = office;
        _detectBrowser = browser;
    }

    // キャッシュ再利用の有効期間（いずれも 8px 両軸一致が前提）。None は通常スクロール中に素早く
    // 再評価できるよう短く、ヒット(バー)はホバー先読みが寿命切れせずホイールまで残るよう長くする。
    private const uint CacheNoneMs = 250;
    private const uint CacheHitMs = 1200;

    // MSAA/UIA バックグラウンド検出のリース (0=空き、それ以外=開始tick)。MSAA/UIA が相手プロセス
    // 都合で長時間戻らないと、単純な 0/1 ガードでは解放されず以後の検出が永久に起動しなくなる。
    // ProbeMaxMs を超えたリースは「詰まった」とみなして新しい検出が奪えるようにし、検出が
    // グローバルに固まるのを防ぐ。
    private static uint _probeLease;
    private const uint ProbeMaxMs = 1000;

    // 直近にカスタムバー（MSAA/UIA）と判定した窓と位置を覚えておく。非同期検出が間に合わない
    // 新規/キャッシュ切れのホイールでも、同じ窓・同じバー帯上なら前回の軸で確定して「素通し（＝誤軸
    // スクロール、例: 横バー上で縦に動く）」を防ぐ。フックを止めないため同期 MSAA は使わない。
    // 再利用は「ヒット軸に直交する方向（縦バーなら X、横バーなら Y）の近傍」に限定する。HWND だけで
    // 判定すると、Chromium のようにコンテンツとスクロールバーが同一 HWND の場合、バーから外れて
    // コンテンツ上に移動した直後のホイールまでバー扱いされてしまうため（誤発火）。
    private sealed record CustomHit(nint Hwnd, int X, int Y, ScrollBarHit Hit, bool Wheel, uint Tick);
    private static volatile CustomHit? _lastCustom;
    private const uint CustomMemoryMs = 2000;
    // 横は記憶を短くする。横バンドは Web の横カルーセル下端と紛らわしく、長く覚えていると通常の縦
    // スクロール中に「一瞬だけ横に走る」誤判定が残留しやすい。横バー上の連続ホイールには十分な長さ。
    private const uint CustomMemoryHorizontalMs = 400;
    private const int CustomReuseSlopPx = 12;

    // 直近カスタムバー記憶を今回の位置で再利用してよいか。バー帯から直交方向に外れていたら不可。
    private static bool CanReuseCustom(CustomHit lc, nint hwnd, int x, int y, uint now)
    {
        uint maxAge = lc.Hit == ScrollBarHit.Horizontal ? CustomMemoryHorizontalMs : CustomMemoryMs;
        if (lc.Hwnd != hwnd || now - lc.Tick >= maxAge || lc.Hit == ScrollBarHit.None)
            return false;
        return lc.Hit switch
        {
            ScrollBarHit.Vertical => Math.Abs(x - lc.X) <= CustomReuseSlopPx,   // 縦バー: X 方向のみ見る
            ScrollBarHit.Horizontal => Math.Abs(y - lc.Y) <= CustomReuseSlopPx, // 横バー: Y 方向のみ見る
            _ => false,
        };
    }

    /// <summary>
    /// カーソル直下のスクロールバーの向きと、スクロールメッセージの送出先ウィンドウを返す。
    /// 検出できなければ (None, 0)。
    ///
    /// フックスレッドの停止時間を最大 ~30ms に抑えるため、同期的に行うのは
    /// クラス名判定と NCHITTEST（30ms 打ち切り）まで。MSAA/UIA はバックグラウンドで
    /// 計算してキャッシュへ反映する（その間の数十 ms は素通し扱い）。
    /// タイムアウトなしの同期呼び出しは、高負荷アプリ（画像ビューアの読み込み中等）で
    /// フックごと固まり、入力キュー溢れ＝連続ビープ音の原因になる。
    /// </summary>
    public static (ScrollBarHit hit, nint target, bool wheel) Detect(int x, int y)
    {
        var c = _cache;
        uint now = (uint)Environment.TickCount;
        // ヒット(バー)はホバー先読みが消えないよう長め、None はコンテンツ上で素早く再評価できるよう短め。
        // どちらも 8px 両軸一致が条件なので、長寿命でも別位置（通常スクロール中）には波及しない。
        if (c is not null)
        {
            uint maxAge = c.Hit == ScrollBarHit.None ? CacheNoneMs : CacheHitMs;
            if (now - c.Tick < maxAge && Math.Abs(x - c.X) < 8 && Math.Abs(y - c.Y) < 8)
                return (c.Hit, c.Target, c.Wheel);
        }

        var pt = new NativeMethods.POINT { X = x, Y = y };
        nint hwnd = InputNative.WindowFromPoint(pt);
        if (hwnd == 0)
            return Store(x, y, ScrollBarHit.None, 0, false);

        // 1) 単独スクロールバー コントロール → 親ウィンドウへ送る
        if (GetClassName(hwnd).Equals("ScrollBar", StringComparison.OrdinalIgnoreCase))
        {
            int style = InputNative.GetWindowLongW(hwnd, GWL_STYLE);
            var dir = (style & SBS_VERT) != 0 ? ScrollBarHit.Vertical : ScrollBarHit.Horizontal;
            return Store(x, y, dir, hwnd, false);
        }

        // 2) 非クライアントの標準スクロールバー: 30ms 打ち切りでヒットテスト
        nint lParam = unchecked((nint)((y << 16) | (x & 0xFFFF)));
        if (InputNative.SendMessageTimeoutW(
                hwnd, InputNative.WM_NCHITTEST, 0, lParam,
                InputNative.SMTO_ABORTIFHUNG, 30, out nint hit) == 0)
        {
            // 応答しない相手には MSAA/UIA も掛けない
            return Store(x, y, ScrollBarHit.None, 0, false);
        }
        switch ((int)hit)
        {
            case InputNative.HTHSCROLL:
                return Store(x, y, ScrollBarHit.Horizontal, hwnd, false);
            case InputNative.HTVSCROLL:
                return Store(x, y, ScrollBarHit.Vertical, hwnd, false);
        }

        // 3)+4) カスタム描画スクロールバー（MSAA → UIA）: 相手プロセス次第で
        // 数秒ブロックしうるため、フックスレッドでは待たずに別スレッドで計算して
        // キャッシュへ反映する。完了までは暫定で「スクロールバーでない」をキャッシュし、
        // 連続ホイール中の NCHITTEST 再実行（30ms×N）も防ぐ。
        KickProbe(x, y, hwnd);

        // 非同期検出が間に合わない間も、同じ窓・同じバー帯上を直近カスタムバーと判定済みなら前回の
        // 軸で確定する。これにより横スクロールバー上で素通しの縦スクロールが混ざるのを防ぐ。送出先は
        // この窓でよい（ホイール送出はどの窓でも有効なことを実測確認済み）。
        // 記憶の種別（MSAA=Wheel:true / UIA=Wheel:false）が現在無効な検出のものなら再利用しない。
        var lc = _lastCustom;
        if (lc is not null && (lc.Wheel ? _detectOffice : _detectBrowser)
            && CanReuseCustom(lc, hwnd, x, y, now))
            return Store(x, y, lc.Hit, hwnd, lc.Wheel);

        return Store(x, y, ScrollBarHit.None, 0, false); // 暫定値（プローブ完了時に上書きされる）
    }

    // スクロールバー窓（NUIScrollbar 等）はこの太さ以下の細い窓になることが多い。
    private const int ScrollbarWindowThickness = 26;

    // 大窓（独自描画アプリ）の端帯先読みは、端をなぞって動かすと 8px 近傍キャッシュを外れ続けて
    // probe が連発しうるため、同一窓では短時間に1回までに絞る。
    private sealed record EdgePrime(nint Hwnd, uint Tick);
    private static volatile EdgePrime? _lastEdgePrime;
    private const uint EdgePrimeThrottleMs = 150;

    // ブラウザのコンテンツ描画窓クラス（Chromium 系: Chrome/Edge/WebView2/Electron、Firefox）。
    private static bool IsBrowserContentClass(string cls)
        => cls == "Chrome_RenderWidgetHostHWND" || cls == "MozillaWindowClass";

    // ── ブラウザ描画窓の settle-debounce 先読み ──
    // Chromium の横バーは窓端に無く端帯では拾えないため UIA 検出が要るが、マウス移動毎に UIA を当て
    // 続けると相手のアクセシビリティが持続的に熱くなり重いサイト（YouTube 等）で stutter になる。
    // そこで「カーソルが約100ms静止したら最終位置で1回だけ UIA 先読み」する。高速移動中は probe 0回、
    // バー上で止めてホイールする自然な操作では静止1回で温まり横バー初回ノッチが正しくなる。純縦
    // スクロール（マウス静止）中は WM_MOUSEMOVE 自体来ないので追加 probe は走らない。
    private const int BrowserSettlePrimeDelayMs = 100;
    // バー上に留まる間はキャッシュ寿命(CacheHitMs)を超えないよう定期的に温め直す。静止中は
    // WM_MOUSEMOVE が来ず Prime で再温めできないため、タイマーで補う。コンテンツ(None)では
    // 再アームしないので、コンテンツ上での定期 UIA は走らず stutter を招かない。
    private const int BrowserSettleRefreshMs = 800;
    // 直近のマウス移動からこの時間で定期リフレッシュを打ち切る（バー上に放置された時の無駄を防ぐ）。
    private const uint BrowserSettleMaxIdleMs = 12000;
    // 中央コンテンツ（横カルーセル等）での無駄 probe・誤横判定を避け、バーのある下側/右側に絞る。
    private const double BrowserZoneLowerFraction = 0.30; // 下側30%（横バー狙い）
    private const double BrowserZoneRightFraction = 0.22; // 右側22%（縦バー補助）
    private sealed record SettlePoint(nint Hwnd, int X, int Y);
    private static volatile SettlePoint? _settlePoint;
    private static uint _lastSettleMoveTick;
    private static readonly System.Threading.Timer _settleTimer =
        new(OnSettleTimer, null, System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);

    // フックスレッドから呼ぶ。最終静止位置と移動時刻を記録してタイマーを再セットするだけ（probe はしない）。
    private static void ArmBrowserSettlePrime(nint hwnd, int x, int y)
    {
        Volatile.Write(ref _lastSettleMoveTick, (uint)Environment.TickCount);
        _settlePoint = new SettlePoint(hwnd, x, y);
        _settleTimer.Change(BrowserSettlePrimeDelayMs, System.Threading.Timeout.Infinite);
    }

    // タイマー（スレッドプール）から発火。静止した最終位置で UIA 検出してキャッシュを温める。
    // バー(hit≠None)なら定期的に温め直して寿命切れを防ぐ。コンテンツ(None)では再アームしない。
    private static void OnSettleTimer(object? _)
    {
        var sp = _settlePoint;
        if (sp is null)
            return;

        uint lease = TryAcquireProbe();
        if (lease == 0)
        {
            // 他の検出中。少し後に再試行する。
            _settleTimer.Change(BrowserSettlePrimeDelayMs, System.Threading.Timeout.Infinite);
            return;
        }
        var hit = ScrollBarHit.None;
        try
        {
            // ブラウザ描画窓なので UIA のみ（MSAA は Chromium で無効＝無駄＋lease 占有）。
            var u = DetectByUia(sp.X, sp.Y, sp.Hwnd);
            hit = u.hit;
            uint t = (uint)Environment.TickCount;
            if (LeaseStillMine(lease))
            {
                _cache = new CacheEntry(sp.X, sp.Y, u.hit, u.target, false, t);
                if (u.hit != ScrollBarHit.None)
                    _lastCustom = new CustomHit(sp.Hwnd, sp.X, sp.Y, u.hit, false, t);
            }
        }
        finally
        {
            ReleaseProbe(lease);
        }

        // バー上で、かつ直近の移動から一定時間内なら、静止中もキャッシュを温め直す。
        if (hit != ScrollBarHit.None &&
            (uint)Environment.TickCount - Volatile.Read(ref _lastSettleMoveTick) < BrowserSettleMaxIdleMs)
            _settleTimer.Change(BrowserSettleRefreshMs, System.Threading.Timeout.Infinite);
    }

    /// <summary>
    /// マウス移動中の事前検出（ホバー先読み）。直後のホイールが最初の1ノッチから正しい軸で動くよう
    /// バックグラウンドで検出してキャッシュを温める（プロセス外フックでは同期 MSAA が使えないための代替）。
    /// 対象は (1) 細い窓＝独立カスタムバー窓、(2) ブラウザ描画窓の下側/右側ゾーン（settle-debounce で
    /// 静止時のみ）、(3) その他大窓の右端/下端の端帯。フックスレッドでは WindowFromPoint/GetWindowRect
    /// （ローカルで安全）しか行わない。
    /// </summary>
    public static void Prime(int x, int y)
    {
        bool office = _detectOffice, browser = _detectBrowser;
        if (!office && !browser)
            return; // カスタム検出が両方無効なら先読みもしない（WindowFromPoint ごと省く）

        var c = _cache;
        uint now = (uint)Environment.TickCount;
        if (c is not null && now - c.Tick < 250 && Math.Abs(x - c.X) < 8 && Math.Abs(y - c.Y) < 8)
            return; // 近傍に新しい結果あり

        nint hwnd = InputNative.WindowFromPoint(new NativeMethods.POINT { X = x, Y = y });
        if (hwnd == 0 || !InputNative.GetWindowRect(hwnd, out var r))
            return;

        // (1) 細い窓＝独立カスタムバー窓: 従来どおり MSAA→UIA で即温める（KickProbe 内で種別を出し分け）
        if (Math.Min(r.Right - r.Left, r.Bottom - r.Top) <= ScrollbarWindowThickness)
        {
            if (!ProbeBusy())
                KickProbe(x, y, hwnd);
            return;
        }

        // (2) ブラウザの描画窓: 下側/右側ゾーンなら settle-debounce 先読みを予約する（静止時のみ1回
        // UIA。常時 UIA で相手のアクセシビリティを熱くし続けないため）。ProbeBusy では弾かない
        // （予約だけ。実 probe は静止後にタイマー→lease で起動）。Chromium 検出が無効なら予約しない。
        if (browser && IsBrowserContentClass(GetClassName(hwnd)))
        {
            if (Clematius.Core.Scroll.ScrollBarBand.InEdgeZone(
                    x, y, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top,
                    BrowserZoneLowerFraction, BrowserZoneRightFraction))
                ArmBrowserSettlePrime(hwnd, x, y);
            return;
        }

        // (3) その他の大窓: 右端/下端のスクロールバー帯なら即温める（窓端にバーがあるアプリ向け）。
        if (ProbeBusy())
            return; // 既に検出中（期限内）
        var cand = Clematius.Core.Scroll.ScrollBarBand.EdgeCandidate(
            x, y, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top,
            InputNative.GetSystemMetrics(InputNative.SM_CXVSCROLL),
            InputNative.GetSystemMetrics(InputNative.SM_CYHSCROLL));
        if (cand == Clematius.Core.Scroll.BandHit.None)
            return; // 端帯でない通常領域では先読みしない

        var ep = _lastEdgePrime;
        if (ep is not null && ep.Hwnd == hwnd && now - ep.Tick < EdgePrimeThrottleMs)
            return; // 同一窓の端帯先読みは間引く（ホットパスでの probe 連発防止）
        _lastEdgePrime = new EdgePrime(hwnd, now);

        KickProbe(x, y, hwnd);
    }

    // カスタム描画スクロールバー（MSAA → UIA）をバックグラウンドで検出してキャッシュへ反映する。
    // フックスレッドを止めないため必ず別スレッドで実行する（単発ガード付き）。
    private static void KickProbe(int x, int y, nint hwnd)
    {
        bool office = _detectOffice, browser = _detectBrowser;
        if (!office && !browser)
            return; // カスタム検出は両方無効
        uint lease = TryAcquireProbe();
        if (lease == 0)
            return;
        Task.Run(() =>
        {
            try
            {
                // 3) MSAA カスタムバー（Excel 等）は WM_VSCROLL を受け付けないため WM_MOUSEWHEEL で送る
                if (office)
                {
                    var m = DetectByMsaaCore(x, y);
                    if (m.hit != ScrollBarHit.None)
                    {
                        uint t = (uint)Environment.TickCount;
                        // 期限超過で lease を奪われていたら、別座標の新しい検出を古い結果で上書きしない
                        if (LeaseStillMine(lease))
                        {
                            _cache = new CacheEntry(x, y, m.hit, m.target, true, t);
                            _lastCustom = new CustomHit(hwnd, x, y, m.hit, true, t);
                        }
                        return;
                    }
                }
                // 4) UIA（Chromium 等）は WM_VSCROLL/WM_HSCROLL がそのまま有効
                // browser 無効なら UIA は掛けず、MSAA 非ヒット（または office 無効）として None をキャッシュし
                // 連続ホイール中の再 probe を抑制する。
                var u = browser ? DetectByUia(x, y, hwnd) : (ScrollBarHit.None, (nint)0);
                uint tu = (uint)Environment.TickCount;
                if (LeaseStillMine(lease))
                {
                    _cache = new CacheEntry(x, y, u.Item1, u.Item2, false, tu);
                    if (u.Item1 != ScrollBarHit.None)
                        _lastCustom = new CustomHit(hwnd, x, y, u.Item1, false, tu);
                }
            }
            finally
            {
                ReleaseProbe(lease);
            }
        });
    }

    // MSAA/UIA バックグラウンド検出のリースを取得する。空き、または期限超過で詰まったリースを
    // 奪えたらその開始 tick（解放用トークン、0 は使わない）を返す。取れなければ 0。
    private static uint TryAcquireProbe()
    {
        uint now = (uint)Environment.TickCount;
        if (now == 0) now = 1; // 0 は「空き」を表すので避ける
        while (true)
        {
            uint cur = Volatile.Read(ref _probeLease);
            if (cur != 0 && now - cur < ProbeMaxMs)
                return 0; // 実行中かつ期限内
            if (Interlocked.CompareExchange(ref _probeLease, now, cur) == cur)
                return now; // 空き、または期限超過のリースを奪った
        }
    }

    // 自分のリースのままなら解放する。期限超過で横取りされていたら触らない。
    private static void ReleaseProbe(uint myToken)
        => Interlocked.CompareExchange(ref _probeLease, 0, myToken);

    // 自分のリースがまだ有効か（期限超過で他の検出に奪われていないか）。
    private static bool LeaseStillMine(uint myToken)
        => Volatile.Read(ref _probeLease) == myToken;

    private static bool ProbeBusy()
    {
        uint cur = Volatile.Read(ref _probeLease);
        return cur != 0 && (uint)Environment.TickCount - cur < ProbeMaxMs;
    }

    /// <summary>
    /// 切り分け用: カーソル直下の各検出段（クラス名 / NCHITTEST / MSAA / UIA）の結果を1行にまとめる。
    /// クロスプロセス呼び出しを含むため必ずバックグラウンドで呼ぶこと（フックスレッド禁止）。
    /// </summary>
    public static string Describe(int x, int y)
    {
        var pt = new NativeMethods.POINT { X = x, Y = y };
        nint hwnd = InputNative.WindowFromPoint(pt);
        if (hwnd == 0)
            return $"pos=({x},{y}) window=(none)";

        string cls = GetClassName(hwnd);
        int style = InputNative.GetWindowLongW(hwnd, GWL_STYLE);

        // ホイール到達時点のキャッシュ/直近カスタム記憶のスナップショット（Prime が温まっていたか判る）
        var c = _cache;
        uint now = (uint)Environment.TickCount;
        string cache = c is null ? "(none)"
            : $"{c.Hit}@({c.X},{c.Y}) age={(int)(now - c.Tick)}ms d=({Math.Abs(x - c.X)},{Math.Abs(y - c.Y)})";
        var lcs = _lastCustom;
        string lastc = lcs is null ? "(none)"
            : $"{lcs.Hit}@({lcs.X},{lcs.Y}) age={(int)(now - lcs.Tick)}ms same={(lcs.Hwnd == hwnd)}";

        // 大窓端帯の候補軸（Prime が先読み対象にしたか判る）
        string edge = "-";
        if (InputNative.GetWindowRect(hwnd, out var wr))
        {
            edge = Clematius.Core.Scroll.ScrollBarBand.EdgeCandidate(
                x, y, wr.Left, wr.Top, wr.Right - wr.Left, wr.Bottom - wr.Top,
                InputNative.GetSystemMetrics(InputNative.SM_CXVSCROLL),
                InputNative.GetSystemMetrics(InputNative.SM_CYHSCROLL)).ToString();
        }

        string nchit = "-";
        nint lParam = unchecked((nint)((y << 16) | (x & 0xFFFF)));
        if (InputNative.SendMessageTimeoutW(
                hwnd, InputNative.WM_NCHITTEST, 0, lParam,
                InputNative.SMTO_ABORTIFHUNG, 30, out nint hit) != 0)
        {
            nchit = ((int)hit).ToString();
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var msaa = DetectByMsaaCore(x, y);
        long msaaMs = sw.ElapsedMilliseconds;
        sw.Restart();
        var uia = DetectByUia(x, y, hwnd);
        long uiaMs = sw.ElapsedMilliseconds;

        return $"pos=({x},{y}) class={cls} style=0x{style:X} nchit={nchit} edge={edge} " +
               $"detect=[office={_detectOffice},browser={_detectBrowser}] " +
               $"msaa={msaa.hit}({msaaMs}ms) uia={uia.hit}({uiaMs}ms) " +
               $"cache=[{cache}] lastCustom=[{lastc}]";
    }

    private static (ScrollBarHit hit, nint target, bool wheel) Store(
        int x, int y, ScrollBarHit hit, nint target, bool wheel)
    {
        _cache = new CacheEntry(x, y, hit, target, wheel, (uint)Environment.TickCount);
        return (hit, target, wheel);
    }

    private const int ROLE_SYSTEM_SCROLLBAR = 3;

    // ── 4) UIA: ScrollPattern を持つ要素＋端帯ジオメトリ ──
    // Chromium はスクロールバーを UIA の ScrollBar 要素として公開しないため、
    // 「スクロール可能要素の右端/下端のスクロールバー幅の帯」をスクロールバー扱いする。
    private static (ScrollBarHit hit, nint target) DetectByUia(int x, int y, nint hwnd)
    {
        try
        {
            var el = System.Windows.Automation.AutomationElement.FromPoint(
                new System.Windows.Point(x, y));
            for (int depth = 0; el is not null && depth < 8; depth++)
            {
                if (el.TryGetCurrentPattern(
                        System.Windows.Automation.ScrollPattern.Pattern, out object p))
                {
                    var pattern = (System.Windows.Automation.ScrollPattern)p;
                    var rect = el.Current.BoundingRectangle;
                    var hit = Clematius.Core.Scroll.ScrollBarBand.Hit(
                        x, y,
                        (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height,
                        pattern.Current.VerticallyScrollable,
                        pattern.Current.HorizontallyScrollable,
                        InputNative.GetSystemMetrics(InputNative.SM_CXVSCROLL),
                        InputNative.GetSystemMetrics(InputNative.SM_CYHSCROLL));
                    if (hit == Clematius.Core.Scroll.BandHit.Vertical)
                        return (ScrollBarHit.Vertical, hwnd);
                    if (hit == Clematius.Core.Scroll.BandHit.Horizontal)
                        return (ScrollBarHit.Horizontal, hwnd);
                    // この要素のバー帯ではない（None）。内側に横スクロール要素（カルーセル等）が
                    // あってもその中央ではバー扱いしないため、外側のスクロール要素を候補に親へ継続する。
                }
                el = System.Windows.Automation.TreeWalker.ControlViewWalker.GetParent(el);
            }
        }
        catch (System.Windows.Automation.ElementNotAvailableException) { }
        catch (COMException) { }
        catch (InvalidOperationException) { }
        return (ScrollBarHit.None, 0);
    }

    private static (ScrollBarHit hit, nint target) DetectByMsaaCore(int x, int y)
    {
        try
        {
            if (AccessibleObjectFromPoint(new POINTSTRUCT { x = x, y = y },
                    out IAccessible? acc, out object child) != 0 || acc is null)
            {
                return (ScrollBarHit.None, 0);
            }

            object childId = child ?? 0;
            // ヒットした要素がスクロールバーの子（ボタン/つまみ）のこともあるため親を少し遡る
            for (int depth = 0; depth < 3 && acc is not null; depth++)
            {
                if (RoleOf(acc, childId) == ROLE_SYSTEM_SCROLLBAR)
                {
                    acc.accLocation(out _, out _, out int w, out int h, childId);
                    if (w <= 0 || h <= 0)
                        return (ScrollBarHit.None, 0);
                    if (WindowFromAccessibleObject(acc, out nint hwnd) != 0 || hwnd == 0)
                        return (ScrollBarHit.None, 0);
                    return (w >= h ? ScrollBarHit.Horizontal : ScrollBarHit.Vertical, hwnd);
                }
                acc = acc.accParent as IAccessible;
                childId = 0; // 親へ遡ったら自身を指す
            }
        }
        catch (COMException) { }
        catch (InvalidCastException) { }
        catch (ArgumentException) { }
        return (ScrollBarHit.None, 0);
    }

    private static int RoleOf(IAccessible acc, object childId)
    {
        try
        {
            return acc.get_accRole(childId) is int role ? role : 0;
        }
        catch (COMException)
        {
            return 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINTSTRUCT { public int x, y; }

    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromPoint(
        POINTSTRUCT pt, out IAccessible? ppacc, out object pvarChild);

    [DllImport("oleacc.dll")]
    private static extern int WindowFromAccessibleObject(IAccessible pacc, out nint phwnd);

    private static string GetClassName(nint hwnd)
    {
        var buffer = new char[64];
        int len = InputNative.GetClassNameW(hwnd, buffer, buffer.Length);
        return len > 0 ? new string(buffer, 0, len) : "";
    }
}
