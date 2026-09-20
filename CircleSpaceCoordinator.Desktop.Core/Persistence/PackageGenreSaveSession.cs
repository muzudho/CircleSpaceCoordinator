namespace CircleSpaceCoordinator.Desktop.Core.Persistence;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Engine.Model;

/// <summary>One package edit scope, including immutable opening bytes and stale-file protection.</summary>
public sealed class PackageGenreSaveSession(string path, string openingJson, PortablePackage openingPackage,
    PortableMaterial openingTable)
{
    public string Path { get; private set; } = System.IO.Path.GetFullPath(path);
    public void RenameFile(string expectedJson, string newName)
    {
        Path = PackageFileOperations.Rename(Path, expectedJson, newName);
    }
    public string OpeningJson { get; } = openingJson;
    public PortableMaterial OpeningTable { get; } = openingTable;
    public string CurrentJson { get; private set; } = openingJson;
    private PortablePackage currentPackage = openingPackage;
    public PortableMaterial CurrentTable => currentPackage.Materials.Single(item => item.Id == OpeningTable.Id);
    public string? ApprovedOverwriteJson { private get; set; }

    public static bool HasMissingComment(PortableMaterial table) => string.IsNullOrWhiteSpace(table.Credits?.ChangeLog);

    public void Save(PortableMaterial table, string handle, DateOnly date, string log, bool restore,
        Func<PortableGenreTableUpdate, string> update, Func<string, PortablePackage> parse)
    {
        var latestJson = File.ReadAllText(Path);
        var latest = parse(latestJson);
        var latestTable = latest.Materials.Single(item => item.Id == OpeningTable.Id);
        var externalChange = latestJson != CurrentJson;
        var replacesComment = !restore && !HasMissingComment(latestTable) &&
            (externalChange || (!restore && !string.IsNullOrWhiteSpace(log) && log != latestTable.Credits?.ChangeLog));
        var approved = ApprovedOverwriteJson == latestJson;
        ApprovedOverwriteJson = null;
        if (replacesComment && !approved)
            throw new PackageCommentConflictException(latestJson, latestTable.Credits!);
        if (externalChange && (!approved || restore))
            throw new IOException("ファイルが変更されました。一覧を更新してから編集してください。");
        // Update only this item against the confirmed version, preserving other package items.
        var next = restore ? OpeningJson : update(new(latestJson, table, handle, date, log));
        var parsed = parse(next);
        PortableLibraryService.SaveMetadata(new(Path, latestJson, latest, null), next);
        CurrentJson = next;
        currentPackage = parsed;
    }
}

public sealed class PackageCommentConflictException(string json, PersonCredits credits)
    : IOException("変更コメントが既に入力されています。作業中に他の誰かがデータを変更したのかもしれません。上書きしますか？")
{
    public string Json { get; } = json;
    public PersonCredits Credits { get; } = credits;
}
