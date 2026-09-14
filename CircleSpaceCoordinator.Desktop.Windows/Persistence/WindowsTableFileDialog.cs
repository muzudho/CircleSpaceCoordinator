namespace CircleSpaceCoordinator.Desktop.Windows.Persistence;

internal static class WindowsTableFileDialog
{
    public static string? Select(bool export, string? initialDirectory)
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = export ? "書き出し先のExcelを選択" : "参加サークル一覧を開く",
            Filter = export ? "Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm" : "参加サークル一覧 (*.xlsx;*.xlsm;*.csv)|*.xlsx;*.xlsm;*.csv",
            CheckFileExists = true,
            InitialDirectory = initialDirectory,
        };
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.FileName : null;
    }
}
