namespace CircleSpaceCoordinator.Core.Model;

using CircleSpaceCoordinator.Core.Geometry;

public sealed record SpaceCell(int X, int Y, int Area);
public sealed record SpaceTypeDefinition(string Id, string Name, string Kind, int Width, int Height,
    IReadOnlyList<SpaceCell> Cells, IReadOnlyList<string> Edges)
{
    // Keep the legacy JSON field; positive area numbers all mean placeable now.
    public SpaceTypeDefinition NormalizeCellStates() => this with
    {
        Cells = Cells.Select(cell => cell with { Area = cell.Area > 0 ? 1 : 0 }).ToArray(),
    };
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<FrameCellConnection>? Connections { get; init; }
}
public sealed record SpaceTarget(string TypeId, int Area);
public sealed record SpaceRequestDefinition(string Id, string Value, string Description, IReadOnlyList<SpaceTarget> Targets);
public sealed record SpaceDefinitionCatalog(IReadOnlyList<SpaceTypeDefinition> Types, IReadOnlyList<SpaceRequestDefinition> Requests)
{
    public int SchemaVersion { get; init; } = 1;
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsConfidential { get; init; }

    public SpaceDefinitionCatalog NormalizeCellStates() => this with
    {
        Types = Types.Select(type => type.NormalizeCellStates()).ToArray(),
        Requests = Requests.Select(request => request with
        {
            Targets = request.Targets.Select(target => new SpaceTarget(target.TypeId, 1)).Distinct().ToArray(),
        }).ToArray(),
    };

    public static SpaceDefinitionCatalog CreateDefault()
    {
        SpaceTypeDefinition Type(string id, string name, string kind, int width, int height, Func<int, int, int> area, string[] edges) =>
            new(id, name, kind, width, height, Enumerable.Range(0, height).SelectMany(y =>
                Enumerable.Range(0, width).Select(x => new SpaceCell(x, y, area(x, y)))).ToArray(), edges);
        return new(
        [
            Type("desk-2-seats", "長さ2・2サークル用長机", "机", 2, 1, (_, _) => 1, ["正面", "開放", "開放", "開放"]),
            Type("desk-whole", "長さ2・1サークル用長机", "机", 2, 1, (_, _) => 1, ["正面", "開放", "開放", "開放"]),
            Type("desk-3-seats", "長さ3・3サークル用長机", "机", 3, 1, (_, _) => 1, ["正面", "開放", "開放", "開放"]),
            Type("desk-3-ends", "長さ3・2サークル用長机", "机", 3, 1, (x, _) => x == 1 ? 0 : 1, ["正面", "開放", "開放", "開放"]),
            Type("free-space", "自由配置フレーム2×2", "場所", 2, 2, (_, _) => 1, ["入口", "入口", "入口", "入口"]),
            Type("booth", "ブース3×3", "ブース", 3, 3, (_, _) => 1, ["壁", "入口", "入口", "壁"]),
        ],
        [
            new("request-1", "1", "長さ2セルの長机のうち1セル", [new("desk-2-seats", 1)]),
            new("request-2", "2", "長さ2セルの長机全体", [new("desk-whole", 1)]),
        ]);
    }

    public void Validate(bool requireRepresentativeCell = true)
    {
        if (SchemaVersion is not (1 or 2)) throw new InvalidDataException("未対応のフレーム定義形式です。");
        if (Types is null || Requests is null) throw new InvalidDataException("型と申込スペースの一覧が必要です。");
        if (Types.Any(type => type is null) || Requests.Any(request => request is null))
            throw new InvalidDataException("空の定義は使えません。");
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
            if (type.Cells is null || type.Cells.Count == 0 ||
                type.Cells.Any(c => c is null || c.X < 0 || c.X >= type.Width || c.Y < 0 || c.Y >= type.Height || c.Area is < 0 or > 9) ||
                type.Cells.Select(c => (c.X, c.Y)).Distinct().Count() != type.Cells.Count)
                throw new InvalidDataException("有効な位置と状態を持つフレームのセルを1つ以上指定してください。");
            if (requireRepresentativeCell && !type.Cells.Any(cell => cell.Area > 0))
                throw new InvalidDataException("ブロックを入力するために、フレームを代表するセルが１つは必要です");
            if (!FrameCellConnection.AreValid(type.Connections, type.Cells.Where(cell => cell.Area > 0)
                .Select(cell => new GridPosition(cell.X, cell.Y)).ToHashSet()))
                throw new InvalidDataException("接続は異なる配置可能セル同士を指定してください。同じ接続の重複はできません。");
        }
        foreach (var request in Requests)
        {
            if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Value) || request.Targets is null || request.Targets.Count == 0)
                throw new InvalidDataException("申込スペースの読込み値と割当先を指定してください。");
            foreach (var target in request.Targets)
                if (target is null || target.Area <= 0 || !Types.Any(t => t.Id == target.TypeId))
                    throw new InvalidDataException("申込スペースが参照しているフレームを削除できません。先に対応を変更してください。");
        }
    }
}
