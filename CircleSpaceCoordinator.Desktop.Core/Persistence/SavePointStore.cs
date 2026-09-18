namespace CircleSpaceCoordinator.Desktop.Core.Persistence;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public sealed record SavePointInfo(string Id, DateTimeOffset Created, string Name, bool Protected, string? DailyDate);

public sealed class SavePointStore
{
    private sealed record Payload(string Name, string Json);
    private sealed record Document(int Version, string Id, DateTimeOffset Created, bool Protected, string? DailyDate, byte[] Encrypted);
    private readonly string directory;
    private readonly int generations;
    public SavePointStore(string root, string projectPath, int generations = 10)
    {
        if (generations is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(generations));
        this.generations = generations;
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(projectPath).ToUpperInvariant())));
        directory = Path.Combine(Path.GetFullPath(root), key);
    }
    public IReadOnlyList<SavePointInfo> List() => ReadAll().OrderByDescending(item => item.Created).Select(item =>
        new SavePointInfo(item.Id, item.Created, Decode(item).Name, item.Protected, item.DailyDate)).ToArray();
    public string Read(string id) => Decode(ReadDocument(id)).Json;
    public void Create(string json, string name, bool protect = false, DateOnly? dailyDate = null)
    {
        using var guard = Lock();
        var day = dailyDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var marker = Path.Combine(directory, "daily-created.txt");
        if (day is not null && (File.Exists(marker) && File.ReadAllText(marker) == day || ReadAll().Any(item => item.DailyDate == day))) return;
        var id = Guid.NewGuid().ToString("N");
        var encrypted = WindowsBackupProtection.Protect(JsonSerializer.SerializeToUtf8Bytes(new Payload(name, json)));
        var document = new Document(1, id, DateTimeOffset.UtcNow, protect, day, encrypted);
        // Verify before rotating away older copies.
        if (Decode(document).Json != json) throw new IOException("バックアップの検証に失敗しました。");
        Write(document);
        if (day is not null) AtomicWrite(marker, day);
        Prune();
    }
    public void SetProtected(string id, bool value)
    {
        using var guard = Lock();
        Write(ReadDocument(id) with { Protected = value });
        // Unprotecting does not delete immediately; the next creation rotates old copies.
    }
    private FileStream Lock()
    {
        Directory.CreateDirectory(directory);
        return new FileStream(Path.Combine(directory, ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    private string FilePath(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("セーブポイントIDが不正です。");
        return Path.Combine(directory, id + ".csc-savepoint");
    }
    private Document ReadDocument(string id)
    {
        var doc = JsonSerializer.Deserialize<Document>(File.ReadAllText(FilePath(id))) ?? throw new InvalidDataException("バックアップが空です。");
        if (doc.Version != 1 || doc.Id != id) throw new InvalidDataException("未対応または破損したバックアップです。");
        return doc;
    }
    private IEnumerable<Document> ReadAll() => !Directory.Exists(directory) ? [] :
        Directory.EnumerateFiles(directory, "*.csc-savepoint").Select(path => ReadDocument(Path.GetFileNameWithoutExtension(path))).ToArray();
    private static Payload Decode(Document doc) => JsonSerializer.Deserialize<Payload>(WindowsBackupProtection.Unprotect(doc.Encrypted))
        ?? throw new InvalidDataException("バックアップを復号できません。");
    private void Write(Document doc) => AtomicWrite(FilePath(doc.Id), JsonSerializer.Serialize(doc));
    private void Prune()
    {
        foreach (var item in ReadAll().Where(item => !item.Protected).OrderByDescending(item => item.Created).Skip(generations))
            File.Delete(FilePath(item.Id));
    }
    internal static void AtomicWrite(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, json); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

/// <summary>Captures the on-disk starting point before the first overwrite; rejects external changes.</summary>
public sealed class AutoSaveSession
{
    private readonly string path;
    private string? expected;
    public AutoSaveSession(string path) { this.path = Path.GetFullPath(path); expected = File.Exists(path) ? File.ReadAllText(path) : null; }
    public void Save(string json, SavePointStore backups, DateOnly day)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var guard = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if ((File.Exists(path) ? File.ReadAllText(path) : null) != expected)
            throw new IOException("別のアプリでイベントファイルが変更されました。上書きを停止しました。セーブポイントに現在の編集を退避できます。");
        if (expected == json) return;
        if (expected is not null) backups.Create(expected, "その日の編集開始前", dailyDate: day);
        SavePointStore.AtomicWrite(path, json);
        expected = json;
    }
}
