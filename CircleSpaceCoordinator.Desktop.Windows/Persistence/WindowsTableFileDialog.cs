namespace CircleSpaceCoordinator.Desktop.Windows.Persistence;

internal static class WindowsTableFileDialog
{
    public static string? SelectNew(string? initialDirectory)
    {
        using var dialog = new System.Windows.Forms.SaveFileDialog
        {
            Title = "新しいファイルに書き出す",
            Filter = "Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv",
            DefaultExt = "xlsx",
            AddExtension = true,
            FileName = "サークル一覧_スペース番号",
            InitialDirectory = initialDirectory,
            OverwritePrompt = true,
        };
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.FileName : null;
    }

    public static string? Select(bool export, string? initialDirectory)
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = export ? "出力先の Excel / CSV を選択" : "参加サークル一覧を開く",
            Filter = "Excel / CSV (*.xlsx;*.xlsm;*.csv)|*.xlsx;*.xlsm;*.csv",
            CheckFileExists = true,
            InitialDirectory = initialDirectory,
        };
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.FileName : null;
    }
}
