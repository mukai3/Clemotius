/// <summary>
/// Clemotius（クレマチス花＝風車モチーフ）アイコン生成ツール。
/// 16/20/24/32/48/256px の多解像度 .ico ファイルを出力する。
///
/// 小サイズ(タスクトレイ)での視認性を最優先に、
///  - 塗りつぶしの紫円盤を土台にして任意の地色(明/暗タスクバー)でも輪郭が出るようにし、
///  - その上に白い風車状の花びらを高コントラストで重ね、
///  - 小サイズでは要素を減らしてベタ塗りで描く（グラデ/葉脈/背面花びらを省く）
/// という方針で描画する。
/// </summary>

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// 既定の出力先: リポジトリ内 src/Clemotius/clemotius.ico
// (BaseDirectory = tools/icongen/bin/Debug/<tfm>/ なので 5 階層上がリポジトリルート)
var outputPath = args.Length > 0 ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Clemotius", "clemotius.ico");

outputPath = Path.GetFullPath(outputPath);

var sizes   = new[] { 16, 20, 24, 32, 48, 256 };
var bitmaps = sizes.Select(CreateClematisBitmap).ToList();

SaveAsIco(bitmaps, outputPath);
Console.WriteLine($"Icon saved: {outputPath}");

// プレビュー用に各サイズの PNG も temp へ出力（リポジトリは汚さない）。
SavePreview(sizes, bitmaps);

foreach (var b in bitmaps) b.Dispose();

// ─────────────────────────────────────────────────────────────────────────────
// 描画
// ─────────────────────────────────────────────────────────────────────────────

static Bitmap CreateClematisBitmap(int size)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode      = SmoothingMode.AntiAlias;
    g.CompositingQuality = CompositingQuality.HighQuality;
    g.PixelOffsetMode    = PixelOffsetMode.HighQuality;
    g.Clear(Color.Transparent);

    float cx = size / 2f;
    float cy = size / 2f;
    bool  small = size < 32;   // 16/20/24px は細部(雄しべ/筋)を省く

    // 実物のクレマチス(青紫・幅広で先の尖った8枚・中央に白い雄しべの放射と黄緑の芯)に寄せる。
    // 背景の円盤は持たず、花そのものでアイコンを構成する。花びらは太め・重なりあり。
    const int petals = 8;
    float length = size * 0.47f;
    float width  = size * 0.34f;

    for (int i = 0; i < petals; i++)
        DrawPetal(g, cx, cy, i * (360f / petals), length, width, small);

    DrawCenter(g, cx, cy, size, small);

    return bmp;
}

/// <summary>
/// 幅広で先の尖ったクレマチスの花びらを描画する。
/// 根元(花の中心側)を淡く、先端へ向け青紫を濃くした線形グラデで実物の色味に寄せ、
/// 重なる花びらの境界を出すための淡い縁取りと、中央の明るい筋(クレマチス特有のバー)を入れる。
/// </summary>
static void DrawPetal(Graphics g, float cx, float cy, float angleDeg,
    float length, float width, bool small)
{
    float hw = width / 2f;

    // ローカル座標(上方向が先端)。広い腹→尖った先端の対称な花びら。
    using var path = new GraphicsPath();
    path.AddBezier(
        new PointF(0f,   0f),
        new PointF(-hw, -length * 0.30f),
        new PointF(-hw, -length * 0.72f),
        new PointF(0f,  -length));
    path.AddBezier(
        new PointF(0f,  -length),
        new PointF( hw, -length * 0.72f),
        new PointF( hw, -length * 0.30f),
        new PointF(0f,   0f));
    path.CloseFigure();

    using var matrix = new Matrix();
    matrix.Translate(cx, cy);
    matrix.Rotate(angleDeg);
    path.Transform(matrix);

    float ar = angleDeg * MathF.PI / 180f;
    var basePt = new PointF(cx, cy);
    var tipPt  = new PointF(cx + length * MathF.Sin(ar), cy - length * MathF.Cos(ar));

    using (var lgb = new LinearGradientBrush(basePt, tipPt,
        Color.FromArgb(255, 0xC4, 0xB0, 0xEE),   // 根元: 淡い藤紫
        Color.FromArgb(255, 0x7A, 0x4F, 0xCC)))  // 先端: 紫
    {
        g.FillPath(lgb, path);
    }

    // 重なる花びらの境界(実物も花びら縁が見える)
    float pw = MathF.Max(0.75f, length * 0.03f);
    using (var pen = new Pen(Color.FromArgb(small ? 150 : 130, 0x4A, 0x36, 0x9E), pw))
    {
        g.DrawPath(pen, path);
    }

    // 中央の明るい筋(クレマチスのバー)。小サイズでは省く。
    if (!small)
    {
        float vw = MathF.Max(1f, width * 0.16f);
        var vTip = new PointF(cx + length * 0.80f * MathF.Sin(ar),
                              cy - length * 0.80f * MathF.Cos(ar));
        using var vpen = new Pen(Color.FromArgb(140, 0xDD, 0xE3, 0xF8), vw)
        {
            StartCap = LineCap.Round,
            EndCap   = LineCap.Round,
        };
        g.DrawLine(vpen, cx, cy, vTip.X, vTip.Y);
    }
}

/// <summary>
/// 花の中心。大サイズでは白い雄しべの放射(スパイダー状)＋先端の黄緑の葯＋黄緑の芯、
/// 小サイズでは淡黄の小円のみ。
/// </summary>
static void DrawCenter(Graphics g, float cx, float cy, int size, bool small)
{
    if (small)
    {
        float r = MathF.Max(1.4f, size * 0.12f);
        using var yb = new SolidBrush(Color.FromArgb(255, 0xEC, 0xE7, 0x9A)); // 淡黄
        g.FillEllipse(yb, cx - r, cy - r, r * 2, r * 2);
        return;
    }

    const int filaments = 22;
    float r1 = size * 0.05f;
    float r2 = size * 0.17f;
    float fw = MathF.Max(0.8f, size * 0.012f);
    float ad = MathF.Max(0.8f, size * 0.018f);
    using var fpen = new Pen(Color.FromArgb(235, 0xFB, 0xFA, 0xEC), fw)
    {
        StartCap = LineCap.Round,
        EndCap   = LineCap.Round,
    };
    using var anther = new SolidBrush(Color.FromArgb(255, 0xC9, 0xC7, 0x4F)); // 黄緑の葯
    for (int i = 0; i < filaments; i++)
    {
        float a  = i * (2f * MathF.PI / filaments) + 0.2f;
        float x1 = cx + r1 * MathF.Cos(a), y1 = cy + r1 * MathF.Sin(a);
        float x2 = cx + r2 * MathF.Cos(a), y2 = cy + r2 * MathF.Sin(a);
        g.DrawLine(fpen, x1, y1, x2, y2);
        g.FillEllipse(anther, x2 - ad, y2 - ad, ad * 2, ad * 2);
    }

    float cr = size * 0.08f;
    using var core = new SolidBrush(Color.FromArgb(255, 0xCF, 0xCE, 0x5C)); // 黄緑の芯
    g.FillEllipse(core, cx - cr, cy - cr, cr * 2, cr * 2);
}

// ─────────────────────────────────────────────────────────────────────────────
// プレビュー出力（temp）。各サイズの等倍 PNG と、確認しやすい拡大montageを書き出す。
// ─────────────────────────────────────────────────────────────────────────────

static void SavePreview(int[] sizes, List<Bitmap> bitmaps)
{
    try
    {
        string dir = Path.Combine(Path.GetTempPath(), "clemotius-icon-preview");
        Directory.CreateDirectory(dir);
        for (int i = 0; i < sizes.Length; i++)
            bitmaps[i].Save(Path.Combine(dir, $"icon-{sizes[i]}.png"), ImageFormat.Png);

        // 小サイズを実寸とチェッカー背景で並べた確認用montage（明暗どちらでも見えるか確認用）
        int[] preview = { 16, 20, 24, 32, 48 };
        int pad = 16, cell = 64;
        using var montage = new Bitmap(pad + preview.Length * (cell + pad), cell + pad * 3);
        using (var g = Graphics.FromImage(montage))
        {
            g.Clear(Color.FromArgb(255, 240, 240, 240));
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            // 上半分=明背景, 下帯=暗背景の2行で確認できるよう、各セルに明暗の地を敷く
            for (int i = 0; i < preview.Length; i++)
            {
                int idx = Array.IndexOf(sizes, preview[i]);
                if (idx < 0) continue;
                int x = pad + i * (cell + pad);
                // 明背景セル
                g.FillRectangle(Brushes.White, x, pad, cell, cell / 2);
                // 暗背景セル
                using var dark = new SolidBrush(Color.FromArgb(255, 32, 32, 32));
                g.FillRectangle(dark, x, pad + cell / 2, cell, cell / 2);
                // 実寸で中央に配置（明・暗それぞれ）
                int s = preview[i];
                g.DrawImage(bitmaps[idx], x + (cell - s) / 2, pad + (cell / 2 - s) / 2, s, s);
                g.DrawImage(bitmaps[idx], x + (cell - s) / 2, pad + cell / 2 + (cell / 2 - s) / 2, s, s);
            }
        }
        montage.Save(Path.Combine(dir, "montage.png"), ImageFormat.Png);
        Console.WriteLine($"Preview saved: {dir}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"(preview skipped: {ex.Message})");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ICO ファイル出力（各サイズを PNG として格納する現代的 ICO 形式）
// ─────────────────────────────────────────────────────────────────────────────

static void SaveAsIco(List<Bitmap> bitmaps, string path)
{
    var pngData = bitmaps.Select(bmp =>
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }).ToList();

    Directory.CreateDirectory(Path.GetDirectoryName(path)!);

    using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
    using var bw = new BinaryWriter(fs);

    int count     = bitmaps.Count;
    int dataStart = 6 + 16 * count;

    bw.Write((short)0);
    bw.Write((short)1);
    bw.Write((short)count);

    int offset = dataStart;
    for (int i = 0; i < count; i++)
    {
        int w = bitmaps[i].Width;
        int h = bitmaps[i].Height;
        bw.Write((byte)(w >= 256 ? 0 : w));
        bw.Write((byte)(h >= 256 ? 0 : h));
        bw.Write((byte)0);
        bw.Write((byte)0);
        bw.Write((short)1);
        bw.Write((short)32);
        bw.Write(pngData[i].Length);
        bw.Write(offset);
        offset += pngData[i].Length;
    }

    foreach (var data in pngData)
        bw.Write(data);
}
