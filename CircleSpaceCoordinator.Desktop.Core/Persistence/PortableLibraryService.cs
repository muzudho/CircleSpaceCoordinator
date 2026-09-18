namespace CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.Engine.Model;

public sealed record PortableLibraryEntry(string Path, string Json, PortablePackage? Package, string? Error)
{
    public bool Matches(string query) => string.Join(" ", Package?.Name, Package?.Description,
        Package is null ? "" : string.Join(" ", Package.Tags), System.IO.Path.GetFileName(Path))
        .Contains(query, StringComparison.CurrentCultureIgnoreCase);
}

public static class PortableLibraryService
{
    public const int MaximumFiles = 200;
    public static IReadOnlyList<PortableLibraryEntry> Read(string directory, Func<string, PortablePackage> parse)
    {
        if (!Directory.Exists(directory)) return [];
        var entries = new List<PortableLibraryEntry>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith(".package-csc.json", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".project-portable.json", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".frame-layout.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).Take(MaximumFiles))
        {
            try
            {
                if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("16 MiBを超えています。");
                var json = File.ReadAllText(path);
                entries.Add(new(path, json, parse(json), null));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            { entries.Add(new(path, "", null, ex.Message)); }
        }
        return entries;
    }

    public static void SaveMetadata(PortableLibraryEntry entry, string json)
    {
        if (entry.Package is null) throw new InvalidOperationException("読込みに失敗したファイルは編集できません。");
        // Same cooperating-process lock and stale-content check as the common catalog.
        using var guard = new FileStream(entry.Path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (File.ReadAllText(entry.Path) != entry.Json)
            throw new IOException("ファイルが変更されました。一覧を更新してから編集してください。");
        FrameLayoutPortableService.SaveDocument(entry.Path, json, overwrite: true);
    }
}
