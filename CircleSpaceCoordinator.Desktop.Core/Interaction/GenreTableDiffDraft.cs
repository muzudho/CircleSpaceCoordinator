namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Core.Model;

public enum GenreTableSide { Left, Right }
public sealed record GenreTableDiffRow(string Code, GenreStyleDefinition? Left, GenreStyleDefinition? Right)
{
    public string Difference => Left is null ? "右のみ" : Right is null ? "左のみ" : Left == Right ? "一致" : "相違";
}

/// <summary>Two detached tables. All row operations are atomic; files and participants are untouched.</summary>
public sealed class GenreTableDiffDraft
{
    private PortableMaterial savedLeft;
    private PortableMaterial savedRight;
    private readonly Stack<(PortableMaterial Left, PortableMaterial Right)> undo = new();
    private readonly Stack<(PortableMaterial Left, PortableMaterial Right)> redo = new();
    public PortableMaterial Left { get; private set; }
    public PortableMaterial Right { get; private set; }
    public bool LeftChanged => !Same(Left, savedLeft);
    public bool RightChanged => !Same(Right, savedRight);
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public IReadOnlyList<GenreTableDiffRow> Rows
    {
        get
        {
            var left = Left.GenreStyles!.ToDictionary(row => row.GenreId, StringComparer.Ordinal);
            var right = Right.GenreStyles!.ToDictionary(row => row.GenreId, StringComparer.Ordinal);
            return left.Keys.Concat(right.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
                .Select(code => new GenreTableDiffRow(code, left.GetValueOrDefault(code), right.GetValueOrDefault(code))).ToArray();
        }
    }

    public GenreTableDiffDraft(CircleSpaceProject project, PortableMaterial incoming, bool packageConfidential)
    {
        incoming.Validate();
        if (incoming.Kind != "genre-styles") throw new ArgumentException("ジャンルコード表を選んでください。");
        Left = new("genre-styles:project", "genre-styles", project.GetGenreCodeTableName(), project.IsConfidential)
        {
            GenreStyles = new GenreStyleDraft(project).Build(), Credits = project.GenreStyleCredits,
            OverallComment = project.GenreStyleComment, GenreCodeOrder = project.GenreCodeOrder.ToArray(),
            GenreCodeOrderComment = project.GenreCodeOrderComment,
        };
        Right = incoming with { GenreStyles = incoming.GenreStyles!.ToArray(), GenreCodeOrder = incoming.GenreCodeOrder?.ToArray(),
            IsConfidential = incoming.IsConfidential || packageConfidential };
        savedLeft = Left;
        savedRight = Right;
    }

    public PortableMaterial Table(GenreTableSide side) => side == GenreTableSide.Left ? Left : Right;
    public bool Contains(GenreTableSide side, string code) => Table(side).GenreStyles!.Any(row => row.GenreId == code);

    public void CopyTo(GenreTableSide destination, string code, bool overwrite)
    {
        var source = Table(destination == GenreTableSide.Left ? GenreTableSide.Right : GenreTableSide.Left);
        var target = Table(destination);
        var row = source.GenreStyles!.SingleOrDefault(row => row.GenreId == code)
            ?? throw new InvalidOperationException("コピー元にこのジャンルコードはありません。");
        var exists = Contains(destination, code);
        if (exists != overwrite) throw new InvalidOperationException(exists
            ? "同じジャンルコードがあります。上書き、または改名してから挿入してください。"
            : "上書き先がありません。コピーを挿入してください。");
        var rows = target.GenreStyles!.Where(item => item.GenreId != code).Append(row).ToArray();
        var order = target.GenreCodeOrder;
        if (!exists && order is { Count: > 0 } && !order.Contains(code, StringComparer.Ordinal)) order = order.Append(code).ToArray();
        Change(destination, target with { GenreStyles = rows, GenreCodeOrder = order,
            IsConfidential = target.IsConfidential || source.IsConfidential });
    }

    public void Delete(GenreTableSide side, string code)
    {
        if (!Contains(side, code)) throw new InvalidOperationException("削除するジャンルコードがありません。");
        var table = Table(side);
        Change(side, table with { GenreStyles = table.GenreStyles!.Where(row => row.GenreId != code).ToArray(),
            GenreCodeOrder = table.GenreCodeOrder?.Where(value => value != code).ToArray() });
    }

    public void Rename(GenreTableSide side, string code, string newCode)
    {
        if (!Contains(side, code)) throw new InvalidOperationException("改名するジャンルコードがありません。");
        try { newCode = PersonCredits.NormalizeChangeLog(newCode); }
        catch (ArgumentException) { throw new ArgumentException("ジャンルコードは改行を含まない1〜1000文字で入力してください。"); }
        if (code == newCode) return;
        if (Contains(side, newCode)) throw new InvalidOperationException("同じジャンルコードが既にあります。");
        var table = Table(side);
        Change(side, table with { GenreStyles = table.GenreStyles!.Select(row => row.GenreId == code ? row with { GenreId = newCode } : row).ToArray(),
            GenreCodeOrder = table.GenreCodeOrder?.Select(value => value == code ? newCode : value).Distinct(StringComparer.Ordinal).ToArray() });
    }

    public void MarkSaved(GenreTableSide side, bool persistedConfidential = false)
    {
        if (persistedConfidential)
        {
            if (side == GenreTableSide.Left) Left = Left with { IsConfidential = true };
            else Right = Right with { IsConfidential = true };
        }
        if (side == GenreTableSide.Left) savedLeft = Left; else savedRight = Right;
    }

    public void Undo()
    {
        if (!undo.TryPop(out var state)) return;
        redo.Push((Left, Right)); (Left, Right) = state;
    }
    public void Redo()
    {
        if (!redo.TryPop(out var state)) return;
        undo.Push((Left, Right)); (Left, Right) = state;
    }
    private void Change(GenreTableSide side, PortableMaterial table)
    {
        table.Validate();
        if (Same(Table(side), table)) return;
        undo.Push((Left, Right)); redo.Clear();
        if (side == GenreTableSide.Left) Left = table; else Right = table;
    }
    private static bool Same(PortableMaterial a, PortableMaterial b) => a.Name == b.Name && a.IsConfidential == b.IsConfidential &&
        a.GenreStyles!.OrderBy(row => row.GenreId, StringComparer.Ordinal).SequenceEqual(b.GenreStyles!.OrderBy(row => row.GenreId, StringComparer.Ordinal)) &&
        (a.GenreCodeOrder ?? []).SequenceEqual(b.GenreCodeOrder ?? []);
}
