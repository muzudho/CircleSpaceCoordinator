namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Infrastructure.Tabular;

/// <summary>A private draft: dragging changes bindings, never the source table.</summary>
public sealed class ExportColumnDraft(ParticipantTableSheet source, IReadOnlyList<int> selected)
{
    public List<string> Headers { get; } = source.Headers.ToList();
    public int[] Columns { get; } = selected.ToArray();
    public int OriginalColumnCount => source.Headers.Count;
    public bool IsComplete => Columns.Length == 3 && Columns.All(column => column >= 0 && column < Headers.Count) && Columns.Distinct().Count() == 3;

    public void Assign(int field, int column)
    {
        if (field < 0 || field >= 3 || column < -1 || column >= Headers.Count) throw new ArgumentOutOfRangeException();
        if (column >= 0)
            for (var other = 0; other < 3; other++) if (Columns[other] == column) Columns[other] = -1;
        Columns[field] = column;
    }

    public void AddNumberColumn(int field)
    {
        if (field is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(field));
        var name = field == 0 ? "ブロック番号" : "セル番号";
        var unique = name;
        for (var suffix = 2; Headers.Contains(unique, StringComparer.Ordinal); suffix++) unique = $"{name} ({suffix})";
        Headers.Add(unique);
        Assign(field, Headers.Count - 1);
    }

    public (ParticipantTableSheet Sheet, int[] Columns) Build()
    {
        if (!IsComplete) throw new InvalidOperationException("３つの用途に別々の列を割り当ててください。");
        var included = Enumerable.Range(0, Headers.Count).Where(column => column < source.Headers.Count || Columns.Contains(column)).ToArray();
        var rows = source.Rows.Select(row => (IReadOnlyList<string>)included.Select(column => row.ElementAtOrDefault(column) ?? "").ToArray()).ToArray();
        return (new(source.Name, included.Select(column => Headers[column]).ToArray(), rows), Columns.Select(column => Array.IndexOf(included, column)).ToArray());
    }
}
