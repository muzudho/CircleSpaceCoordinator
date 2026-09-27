namespace CircleSpaceCoordinator.Desktop.Core.Persistence;

public static class ParticipantExampleFiles
{
    public const string CsvName = "きふわらべオンリー即売会_参加サークル一覧.csv";
    public const string ExcelName = "きふわらべオンリー即売会_参加サークル一覧.xlsx";

    public static string GetDefaultDirectory() => Path.Combine(
        Path.GetDirectoryName(ProjectFileService.GetDefaultProjectsDirectory())!, "examples");

    // Copies bundled samples once. Existing user files are never replaced.
    public static void InstallMissing(string bundledExamplesDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (var name in new[] { CsvName, ExcelName })
        {
            var source = Path.Combine(bundledExamplesDirectory, name);
            var destination = Path.Combine(destinationDirectory, name);
            if (File.Exists(destination)) continue;
            if (!File.Exists(source)) throw new FileNotFoundException("同梱サンプルが見つかりません。", source);
            try { File.Copy(source, destination, overwrite: false); }
            catch (IOException) when (File.Exists(destination)) { }
        }
    }
}
