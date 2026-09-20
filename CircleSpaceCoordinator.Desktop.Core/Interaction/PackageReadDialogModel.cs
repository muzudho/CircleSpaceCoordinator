namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Core.Model;

/// <summary>Selection state for the shared package reader; never imports or modifies a package.</summary>
public sealed class PackageReadDialogModel(Func<string, PortablePackage> parse)
{
    public string DirectoryPath { get; private set; } = "";
    public string[] Files { get; private set; } = [];
    public PortableMaterial[] Tables { get; private set; } = [];
    public int FileIndex { get; private set; } = -1;
    public int TableIndex { get; private set; } = -1;
    public string? Error { get; private set; }
    public string? SelectedJson { get; private set; }
    public PortablePackage? SelectedPackage { get; private set; }
    public bool CanRead => TableIndex >= 0 && TableIndex < Tables.Length;

    public void SetDirectory(string directory)
    {
        DirectoryPath = directory;
        Files = [];
        FileIndex = -1;
        ClearTables();
        try
        {
            Files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => path.EndsWith(".package-csc.json", StringComparison.OrdinalIgnoreCase))
                .OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { Error = ex.Message; }
    }

    public void SelectFile(int index)
    {
        FileIndex = index >= 0 && index < Files.Length ? index : -1;
        ClearTables();
        if (FileIndex < 0) return;
        try
        {
            var path = Files[FileIndex];
            if (new FileInfo(path).Length > 16 * 1024 * 1024)
                throw new InvalidDataException("16 MiBを超えています。");
            var json = File.ReadAllText(path);
            var package = parse(json);
            SelectedJson = json;
            SelectedPackage = package;
            Tables = package.Materials.Where(material => material.Kind == "genre-styles")
                .OrderBy(material => material.Name, StringComparer.Ordinal).ToArray();
        }
        catch (Exception ex)
        {
            // Includes parser/engine failures. A failed selection must never retain the previous table.
            Error = ex.Message;
        }
    }

    public void SelectTable(int index) => TableIndex = index >= 0 && index < Tables.Length ? index : -1;

    private void ClearTables()
    {
        Tables = [];
        SelectedJson = null;
        SelectedPackage = null;
        TableIndex = -1;
        Error = null;
    }
}
