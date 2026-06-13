namespace Clemotius.Core.Actions;

/// <summary>
/// WM_APPCOMMAND の APPCOMMAND_* に対応するコマンド。値は Win32 の定数に一致させる。
/// （WM_APPCOMMAND の lParam 上位ワードに入れる値）
/// </summary>
public enum AppCommand
{
    BrowserBackward = 1,
    BrowserForward = 2,
    BrowserRefresh = 3,
    BrowserStop = 4,
    BrowserSearch = 5,
    BrowserFavorites = 6,
    BrowserHome = 7,
    VolumeMute = 8,
    VolumeDown = 9,
    VolumeUp = 10,
    MediaNextTrack = 11,
    MediaPreviousTrack = 12,
    MediaStop = 13,
    MediaPlayPause = 14,
    // ウィンドウ/タブを閉じる。Win32 に APPCOMMAND_BROWSER_CLOSE は無いため
    // 汎用の APPCOMMAND_CLOSE(31) を使う（ブラウザは WM_APPCOMMAND の Close を処理する）。
    Close = 31,
}
