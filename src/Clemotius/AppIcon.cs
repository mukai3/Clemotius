using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Clemotius.Interop;

namespace Clemotius;

/// <summary>
/// アプリ共通のアイコン（clemotius.ico）。アセンブリの埋め込みリソースから読み込み、
/// 取得できなければ既定のアプリアイコンにフォールバックする。
/// 一時停止表示用にグレースケール版も提供する。
/// </summary>
internal static class AppIcon
{
    private static Icon? _shared;
    private static Icon? _grayscale;

    public static Icon Shared
    {
        get
        {
            if (_shared is null)
            {
                try
                {
                    var asm = typeof(AppIcon).Assembly;
                    string? name = Array.Find(
                        asm.GetManifestResourceNames(),
                        n => n.EndsWith("clemotius.ico", StringComparison.OrdinalIgnoreCase));
                    using var stream = name is null ? null : asm.GetManifestResourceStream(name);
                    _shared = stream is not null ? new Icon(stream) : SystemIcons.Application;
                }
                catch (Exception ex) when (ex is IOException or ArgumentException)
                {
                    _shared = SystemIcons.Application;
                }
            }
            return _shared;
        }
    }

    /// <summary>一時停止中に表示するグレースケール版アイコン（<see cref="Shared"/> から生成）。</summary>
    public static Icon Grayscale
    {
        get
        {
            if (_grayscale is null)
            {
                try
                {
                    _grayscale = ToGrayscale(Shared);
                }
                catch (Exception ex) when (ex is ArgumentException or ExternalException)
                {
                    _grayscale = Shared; // 生成に失敗したら通常アイコンで代替
                }
            }
            return _grayscale;
        }
    }

    private static Icon ToGrayscale(Icon source)
    {
        using var src = source.ToBitmap();
        using var gray = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(gray))
        {
            // 輝度変換（アルファは保持）
            var matrix = new ColorMatrix(new[]
            {
                new[] { 0.299f, 0.299f, 0.299f, 0f, 0f },
                new[] { 0.587f, 0.587f, 0.587f, 0f, 0f },
                new[] { 0.114f, 0.114f, 0.114f, 0f, 0f },
                new[] { 0f, 0f, 0f, 1f, 0f },
                new[] { 0f, 0f, 0f, 0f, 1f },
            });
            using var attrs = new ImageAttributes();
            attrs.SetColorMatrix(matrix);
            g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height),
                0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
        }

        nint hicon = gray.GetHicon();
        try
        {
            // ハンドル所有を独立させたコピーを返す（hicon は破棄してよい状態にする）
            using var fromHandle = Icon.FromHandle(hicon);
            return (Icon)fromHandle.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hicon);
        }
    }
}
