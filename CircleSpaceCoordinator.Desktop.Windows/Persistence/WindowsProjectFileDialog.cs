namespace CircleSpaceCoordinator.Desktop.Windows.Persistence;

public static class WindowsProjectFileDialog
{
    public static string? Open(string? currentPath, string? projectsDirectory = null)
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            CheckFileExists = true,
            DefaultExt = "event-project-csc.json",
            Filter = "イベントプロジェクト (*.event-project-csc.json)|*.event-project-csc.json|従来のプロジェクトJSON (*.json)|*.json|すべてのファイル (*.*)|*.*",
            InitialDirectory = GetInitialDirectory(currentPath, projectsDirectory),
            Title = "イベントプロジェクトを開く",
        };
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.FileName : null;
    }

    public static string? Save(string suggestedFileName, string? projectsDirectory = null)
    {
        using var dialog = new System.Windows.Forms.SaveFileDialog
        {
            AddExtension = true,
            CheckPathExists = true,
            DefaultExt = "event-project-csc.json",
            FileName = suggestedFileName,
            Filter = "イベントプロジェクト (*.event-project-csc.json)|*.event-project-csc.json",
            InitialDirectory = GetInitialDirectory(null, projectsDirectory),
            OverwritePrompt = true,
            Title = "イベントプロジェクトの保存先を指定",
        };
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.FileName : null;
    }

    private static string GetInitialDirectory(string? path, string? projectsDirectory)
    {
        var directory = string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null && Directory.Exists(directory))
            return directory;
        return !string.IsNullOrWhiteSpace(projectsDirectory) && Directory.Exists(projectsDirectory)
            ? projectsDirectory
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }
}
