using System.Windows;
using Wpf.Ui.Appearance;

namespace Clemotius.SettingsUi;

/// <summary>
/// 設定値("system"/"light"/"dark")に応じて設定画面のテーマを適用する。
/// system のときだけ OS のライト/ダーク切替に追従(SystemThemeWatcher)し、
/// light/dark の固定時は追従を止めて指定テーマを適用する。
/// </summary>
internal static class ThemeApplier
{
    /// <summary>テーマの色を適用し、system のときだけ OS 追従(Watch)を設定する。</summary>
    public static void Apply(string theme, Window window)
    {
        ApplyColors(theme);
        if (theme is "light" or "dark")
            UnWatchIfLoaded(window);
        else // "system"
            SystemThemeWatcher.Watch(window);
    }

    /// <summary>
    /// テーマの色だけを(再)適用する。Watch 状態は変更しない。
    /// 構築直後(未ロード時)の初回適用では一部の DynamicResource(リスト行の文字色など)が
    /// 旧テーマのまま取りこぼされることがあるため、ロード後の再適用に使う。
    /// </summary>
    public static void ApplyColors(string theme)
    {
        switch (theme)
        {
            case "light":
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
                break;
            case "dark":
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                break;
            default: // "system"
                ApplicationThemeManager.ApplySystemTheme();
                break;
        }
    }

    // SystemThemeWatcher.UnWatch は未ロードのウィンドウに対して例外を投げる。
    // コンストラクタ時点(未ロード)はまだ Watch しておらず外す対象も無いため、ロード済みのときだけ外す。
    private static void UnWatchIfLoaded(Window window)
    {
        if (window.IsLoaded)
            SystemThemeWatcher.UnWatch(window);
    }
}
