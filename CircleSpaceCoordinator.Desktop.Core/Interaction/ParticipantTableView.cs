namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Core.Model;

/// <summary>A read-only view over existing values; never copies the cell matrix.</summary>
public sealed class ParticipantTableView
{
    private readonly IReadOnlyList<Participant> participants;
    private readonly IReadOnlyList<string> keys;
    private readonly bool usesSourceValues;
    public IReadOnlyList<string> Headers { get; }
    public int RowCount => participants.Count;
    public int ColumnCount => Headers.Count;
    public string SourceDescription { get; }

    public ParticipantTableView(CircleSpaceProject project)
    {
        participants = project.Participants;
        if (project.ParticipantTableSource is { } source && source.Headers.Count == source.ColumnKeys.Count)
        {
            Headers = source.Headers;
            keys = source.ColumnKeys;
            usesSourceValues = true;
            SourceDescription = $"{source.FileName}　シート: {source.SheetName}";
        }
        else
        {
            keys = participants.SelectMany(p => p.SourceValues.Keys).Distinct(StringComparer.Ordinal).ToArray();
            usesSourceValues = keys.Count > 0;
            Headers = usesSourceValues ? keys : ["サークルID", "サークル名", "必要セル数", "ジャンルID", "合体先サークルID"];
            SourceDescription = usesSourceValues
                ? "取込み元の情報なし（保存済みの列値を表示）"
                : "取込み元の情報なし（登録済みのサークル情報を表示）";
        }
    }

    public string GetValue(int row, int column)
    {
        var participant = participants[row];
        if (usesSourceValues)
            return participant.SourceValues.TryGetValue(keys[column], out var value) ? value : "";
        return column switch
        {
            0 => participant.CircleId,
            1 => participant.DisplayName,
            2 => participant.RequiredCellCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            3 => participant.GenreId ?? "",
            4 => participant.CombinedWithCircleId ?? "",
            _ => "",
        };
    }
}

/// <summary>Whole-cell scrolling keeps both drawing and hit testing within the visible range.</summary>
public sealed class TableScrollPosition
{
    public int Row { get; private set; }
    public int Column { get; private set; }
    public void MoveTo(int row, int column, int rows, int columns, int visibleRows, int visibleColumns)
    {
        Row = Math.Clamp(row, 0, Math.Max(0, rows - Math.Max(1, visibleRows)));
        Column = Math.Clamp(column, 0, Math.Max(0, columns - Math.Max(1, visibleColumns)));
    }
}
