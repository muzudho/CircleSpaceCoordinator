namespace CircleSpaceCoordinator.Infrastructure.Tabular;

using CircleSpaceCoordinator.Application.Participants;

public sealed record ParticipantColumnMapping(
    int CircleIdColumn,
    int DisplayNameColumn,
    int? RequiredCellCountColumn = null,
    int? CombinedWithCircleIdColumn = null,
    int? GenreIdColumn = null);

public static class ParticipantTableMapper
{
    public static IReadOnlyList<ParticipantImportRow> Map(
        ParticipantTableSheet sheet,
        ParticipantColumnMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(mapping);
        EnsureColumn(mapping.CircleIdColumn, sheet.Headers.Count, nameof(mapping.CircleIdColumn));
        EnsureColumn(mapping.DisplayNameColumn, sheet.Headers.Count, nameof(mapping.DisplayNameColumn));
        if (mapping.RequiredCellCountColumn is { } requiredColumn)
            EnsureColumn(requiredColumn, sheet.Headers.Count, nameof(mapping.RequiredCellCountColumn));
        if (mapping.CombinedWithCircleIdColumn is { } combinedColumn)
            EnsureColumn(combinedColumn, sheet.Headers.Count, nameof(mapping.CombinedWithCircleIdColumn));
        if (mapping.GenreIdColumn is { } genreColumn)
            EnsureColumn(genreColumn, sheet.Headers.Count, nameof(mapping.GenreIdColumn));

        return sheet.Rows.Select((row, index) => new ParticipantImportRow(
            Value(row, mapping.CircleIdColumn),
            Value(row, mapping.DisplayNameColumn),
            ParseRequiredCellCount(row, mapping.RequiredCellCountColumn, index),
            mapping.CombinedWithCircleIdColumn is { } column ? NullIfEmpty(Value(row, column)) : null,
            mapping.GenreIdColumn is { } genre ? NullIfEmpty(Value(row, genre)) : null)).ToArray();
    }

    public static ParticipantColumnMapping Guess(IReadOnlyList<string> headers)
    {
        var circleId = Find(headers, "サークルID", "サークルＩＤ", "circleId", "circle_id", "id");
        var displayName = Find(headers, "サークル名", "circleName", "circle_name", "name");
        var required = Find(headers, "必要セル数", "スペース数", "requiredCellCount", "required_cell_count");
        var combined = Find(headers, "合体先サークルID", "合体先サークルＩＤ", "combinedWithCircleId", "combined_with_circle_id");
        var genre = Find(headers, "ジャンルID", "ジャンルＩＤ", "ジャンル", "genreId", "genre_id", "genre");
        return new ParticipantColumnMapping(circleId ?? 0, displayName ?? Math.Min(1, Math.Max(0, headers.Count - 1)), required, combined, genre);
    }

    private static int ParseRequiredCellCount(IReadOnlyList<string> row, int? column, int rowIndex)
    {
        if (column is null || string.IsNullOrWhiteSpace(Value(row, column.Value)))
            return 1;
        if (int.TryParse(Value(row, column.Value), out var value) && value > 0)
            return value;
        throw new FormatException($"{rowIndex + 2}行目の必要セル数を整数として読み取れません。");
    }

    private static string Value(IReadOnlyList<string> row, int column) =>
        column < row.Count ? row[column].Trim() : "";

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static int? Find(IReadOnlyList<string> headers, params string[] candidates)
    {
        for (var index = 0; index < headers.Count; index++)
            if (candidates.Any(candidate => string.Equals(headers[index].Trim(), candidate, StringComparison.OrdinalIgnoreCase)))
                return index;
        return null;
    }

    private static void EnsureColumn(int column, int count, string parameterName)
    {
        if (column < 0 || column >= count)
            throw new ArgumentOutOfRangeException(parameterName);
    }
}
