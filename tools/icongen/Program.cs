/// <summary>
/// クレマウチス（クレマチス花）アイコン生成ツール。
/// 16/32/48/256px の多解像度 .ico ファイルを出力する。
/// </summary>

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

var outputPath = args.Length > 0 ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Clemoutis", "clemoutis.ico");

outputPath = Path.GetFullPath(outputPath);

var sizes   = new[] { 16, 32, 48, 256 };
var bitmaps = sizes.Select(CreateClematisBitmap).ToList();

SaveAsIco(bitmaps, outputPath);
Console.WriteLine($"Icon saved: {outputPath}");

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

    // 後ろ層：4枚・45° オフセット・やや小さく・少し暗め
    for (int i = 0; i < 4; i++)
        DrawPetal(g, cx, cy, i * 90f + 45f,
            length    : size * 0.42f,
            width     : size * 0.265f,
            innerColor: Color.FromArgb(170, 210, 190, 240),  // 薄ラベンダー
            outerColor: Color.FromArgb(195, 130,  85, 200),  // 中紫
            veinAlpha : 0);

    // 前層：4枚・メイン
    for (int i = 0; i < 4; i++)
        DrawPetal(g, cx, cy, i * 90f,
            length    : size * 0.462f,
            width     : size * 0.285f,
            innerColor: Color.FromArgb(220, 245, 235, 255),  // ほぼ白ラベンダー
            outerColor: Color.FromArgb(245, 150, 100, 215),  // 明るい薄紫
            veinAlpha : size >= 20 ? 50 : 0);

    // 中心：白い小円（かざぐるマウスと同様）
    float cR = size * 0.10f;
    using var wb = new SolidBrush(Color.FromArgb(250, 255, 255, 255));
    g.FillEllipse(wb, cx - cR, cy - cR, cR * 2, cR * 2);

    return bmp;
}

/// <summary>
/// 左右非対称ベジェ曲線による花びらを描画する。
/// 非対称にすることで風車ブレードのような動きのある形状になる。
/// </summary>
static void DrawPetal(Graphics g, float cx, float cy, float angleDeg,
    float length, float width, Color innerColor, Color outerColor, int veinAlpha)
{
    float hw = width / 2f;

    // ローカル座標（上方向が先端）で非対称花びらを定義
    // 左辺：外側に大きく膨らむ（かざぐるマウス風の動きを演出）
    // 右辺：控えめなカーブ
    using var path = new GraphicsPath();
    path.AddBezier(
        new PointF(0f,           0f),
        new PointF(-hw * 1.15f, -length * 0.30f),
        new PointF(-hw * 1.10f, -length * 0.65f),
        new PointF(0f,          -length));
    path.AddBezier(
        new PointF(0f,          -length),
        new PointF( hw * 0.75f, -length * 0.62f),
        new PointF( hw * 0.70f, -length * 0.28f),
        new PointF(0f,           0f));
    path.CloseFigure();

    using var matrix = new Matrix();
    matrix.Translate(cx, cy);
    matrix.Rotate(angleDeg);
    path.Transform(matrix);

    // PathGradientBrush：中心（根元）から外側（先端周辺）へグラデーション
    using var pgb = new PathGradientBrush(path)
    {
        CenterColor    = innerColor,
        SurroundColors = new[] { outerColor },
        FocusScales    = new PointF(0f, 0.20f),
    };
    g.FillPath(pgb, path);

    // 中心の白い葉脈
    if (veinAlpha > 0 && length > 8)
    {
        float ar  = angleDeg * MathF.PI / 180f;
        float tx  = cx + length * 0.88f * MathF.Sin(ar);
        float ty  = cy - length * 0.88f * MathF.Cos(ar);
        float vW  = MathF.Max(0.5f, width * 0.075f);
        using var vp = new Pen(Color.FromArgb(veinAlpha, 255, 255, 255), vW)
        {
            StartCap = LineCap.Round,
            EndCap   = LineCap.Round,
        };
        g.DrawLine(vp, cx, cy, tx, ty);
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
