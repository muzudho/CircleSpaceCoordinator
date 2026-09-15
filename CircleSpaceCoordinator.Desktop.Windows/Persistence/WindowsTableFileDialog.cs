namespace CircleSpaceCoordinator.Desktop.Windows.Persistence;

internal static class WindowsTableFileDialog
{
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
