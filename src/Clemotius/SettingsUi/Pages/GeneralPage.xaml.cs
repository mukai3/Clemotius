namespace Clemotius.SettingsUi.Pages;

/// <summary>一般ページ（トレイ設定＋ジェスチャー詳細）。色選択は WinForms の ColorDialog を流用。</summary>
public partial class GeneralPage
{
    public GeneralPage()
    {
        InitializeComponent();
        PageDataContext.AttachFallback(this);
    }

    private SettingsViewModel? Vm => DataContext as SettingsViewModel;

    private void OnPickValidColor(object sender, System.Windows.RoutedEventArgs e)
    {
        if (Vm is { } vm && PickColor(vm.General.ValidStrokeColor) is { } hex)
            vm.General.ValidStrokeColor = hex;
    }

    private void OnPickInvalidColor(object sender, System.Windows.RoutedEventArgs e)
    {
        if (Vm is { } vm && PickColor(vm.General.InvalidStrokeColor) is { } hex)
            vm.General.InvalidStrokeColor = hex;
    }

    /// <summary>WinForms の ColorDialog で色を選ぶ。キャンセル時は null。</summary>
    private static string? PickColor(string currentHex)
    {
        var current = System.Drawing.Color.Black;
        try
        {
            current = System.Drawing.ColorTranslator.FromHtml(currentHex);
        }
        catch (Exception)
        {
            // 不正な保存値は黒から開始
        }

        using var dlg = new System.Windows.Forms.ColorDialog { Color = current, FullOpen = true };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return null;
        return $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
    }
}
