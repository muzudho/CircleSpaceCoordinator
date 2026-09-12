namespace CircleSpaceCoordinator.Desktop.Core.Persistence;

using System.Text.Json;

public sealed record SpaceCell(int X, int Y, int Area);
public sealed record SpaceTypeDefinition(string Id, string Name, string Kind, int Width, int Height,
    IReadOnlyList<SpaceCell> Cells, IReadOnlyList<string> Edges);
public sealed record SpaceTarget(string TypeId, int Area);
public sealed record SpaceRequestDefinition(string Id, string Value, string Description, IReadOnlyList<SpaceTarget> Targets);
public sealed record SpaceDefinitionCatalog(IReadOnlyList<SpaceTypeDefinition> Types, IReadOnlyList<SpaceRequestDefinition> Requests)
{
    public int SchemaVersion { get; init; } = 1;

    public static SpaceDefinitionCatalog CreateDefault()
    {
        SpaceTypeDefinition Type(string id, string name, string kind, int width, int height, Func<int, int, int> area, string[] edges) =>
            new(id, name, kind, width, height, Enumerable.Range(0, height).SelectMany(y =>
                Enumerable.Range(0, width).Select(x => new SpaceCell(x, y, area(x, y)))).ToArray(), edges);
        return new(
        [
            Type("desk-2-seats", "長さ2・2サークル用長机", "机", 2, 1, (x, _) => x + 1, ["正面", "開放", "開放", "開放"]),
            Type("desk-whole", "長さ2・1サークル用長机", "机", 2, 1, (_, _) => 1, ["正面", "開放", "開放", "開放"]),
            Type("desk-3-seats", "長さ3・3サークル用長机", "机", 3, 1, (x, _) => x + 1, ["正面", "開放", "開放", "開放"]),
            Type("desk-3-ends", "長さ3・2サークル用長机", "机", 3, 1, (x, _) => x == 1 ? 0 : x == 0 ? 1 : 2, ["正面", "開放", "開放", "開放"]),
            Type("free-space", "自由配置フレーム2×2", "場所", 2, 2, (_, _) => 1, ["入口", "入口", "入口", "入口"]),
            Type("booth", "ブース3×3", "ブース", 3, 3, (_, _) => 1, ["壁", "入口", "入口", "壁"]),
        ],
        [
            new("request-1", "1", "長さ2セルの長机のうち1セル", [new("desk-2-seats", 1), new("desk-2-seats", 2)]),
            new("request-2", "2", "長さ2セルの長机全体", [new("desk-whole", 1)]),
        ]);
    }

    public void Validate()
    {
        if (SchemaVersion != 1) throw new InvalidDataException("未対応のフレーム定義形式です。");
        if (Types is null || Requests is null) throw new InvalidDataException("型と申込スペースの一覧が必要です。");
        if (Types.Select(t => t.Id).Distinct().Count() != Types.Count || Requests.Select(r => r.Id).Distinct().Count() != Requests.Count)
            throw new InvalidDataException("定義IDが重複しています。");
        if (Requests.Select(r => r.Value).Distinct(StringComparer.Ordinal).Count() != Requests.Count)
            throw new InvalidDataException("申込スペースの読込み値が重複しています。");
        foreach (var type in Types)
        {
            if (string.IsNullOrWhiteSpace(type.Id) || string.IsNullOrWhiteSpace(type.Name) || type.Width is < 1 or > 12 || type.Height is < 1 or > 12)
                throw new InvalidDataException("型の名前と1～12セルの寸法を指定してください。");
            if (type.Kind is not ("机" or "場所" or "ブース") || type.Edges is null || type.Edges.Count != 4 ||
                type.Edges.Any(e => e is not ("開放" or "壁" or "入口" or "正面")))
                throw new InvalidDataException("種類と各辺の情報を指定してください。");
            if (type.Cells is null || type.Cells.Count == 0 || !type.Cells.Any(c => c.Area > 0) ||
                type.Cells.Any(c => c.X < 0 || c.X >= type.Width || c.Y < 0 || c.Y >= type.Height || c.Area is < 0 or > 9) ||
                type.Cells.Select(c => (c.X, c.Y)).Distinct().Count() != type.Cells.Count)
                throw new InvalidDataException("占有範囲内に少なくとも1つの割当区画を作ってください。区画番号は1～9です。");
        }
        foreach (var request in Requests)
        {
            if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Value) || request.Targets is null || request.Targets.Count == 0)
                throw new InvalidDataException("申込スペースの読込み値と割当先を指定してください。");
            foreach (var target in request.Targets)
                if (target.Area <= 0 || !Types.Any(t => t.Id == target.TypeId && t.Cells.Any(c => c.Area == target.Area)))
                    throw new InvalidDataException("申込スペースが参照している型・区画を削除できません。先に対応を変更してください。");
        }
    }
}

/// <summary>One catalog per application installation, independent of event and plan selection.</summary>
public sealed class SpaceDefinitionStore
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public string Path { get; }
    public SpaceDefinitionCatalog Current { get; private set; }
    private string? savedJson;

    public SpaceDefinitionStore(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        if (File.Exists(Path))
        {
            savedJson = File.ReadAllText(Path);
            Current = JsonSerializer.Deserialize<SpaceDefinitionCatalog>(savedJson, Options) ?? throw new InvalidDataException("フレーム定義を読み込めません。");
        }
        else Current = SpaceDefinitionCatalog.CreateDefault();
        Current.Validate();
    }

    public void Save(SpaceDefinitionCatalog catalog)
    {
        catalog.Validate();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temporary = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        // A second running application must reload instead of silently overwriting edits.
        using var guard = new FileStream(Path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if ((File.Exists(Path) ? File.ReadAllText(Path) : null) != savedJson)
            throw new IOException("別のアプリでフレーム定義が変更されました。アプリを開き直してください。");
        var json = JsonSerializer.Serialize(catalog, Options);
        try
        {
            File.WriteAllText(temporary, json);
            File.Move(temporary, Path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        savedJson = json;
        Current = catalog;
    }
}
