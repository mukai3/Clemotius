using System.IO;

namespace Clemoutis;

/// <summary>
/// アプリ共通のアイコン（clemoutis.ico）。出力ディレクトリから読み込み、
/// 取得できなければ既定のアプリアイコンにフォールバックする。
/// </summary>
internal static class AppIcon
{
    private static Icon? _shared;

    public static Icon Shared
    {
        get
        {
            if (_shared is null)
            {
                string path = Path.Combine(System.AppContext.BaseDirectory, "clemoutis.ico");
                try
                {
                    _shared = File.Exists(path) ? new Icon(path) : SystemIcons.Application;
                }
                catch (Exception ex) when (ex is IOException or ArgumentException)
                {
                    _shared = SystemIcons.Application;
                }
            }
            return _shared;
        }
    }
}
