namespace CircleSpaceCoordinator.Infrastructure.Tabular;

using ClosedXML.Excel;

public sealed record CircleSeatExportRow(string CircleId, string BlockName, string SeatName);

public sealed record CircleSeatExportResult(int UpdatedRowCount, int MatchedCircleCount, int MissingCircleCount);

public static class CircleSeatExcelExporter
{
    public static CircleSeatExportResult Export(
        string path,
        string sheetName,
        int blockColumnIndex,
        int seatColumnIndex,
        int circleIdColumnIndex,
        IReadOnlyList<CircleSeatExportRow> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(sheetName);
        ArgumentNullException.ThrowIfNull(rows);
        if (blockColumnIndex < 0 || seatColumnIndex < 0 || circleIdColumnIndex < 0)
            throw new ArgumentOutOfRangeException("列を選択してください。");
        if (new[] { blockColumnIndex, seatColumnIndex, circleIdColumnIndex }.Distinct().Count() != 3)
            throw new InvalidOperationException("ブロック番号・席番号・サークルIDには、それぞれ別の列を選択してください。");

        var byCircleId = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.CircleId))
            .GroupBy(row => row.CircleId.Trim(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        using var workbook = new XLWorkbook(path);
        var worksheet = workbook.Worksheet(sheetName);
        var used = worksheet.RangeUsed() ?? throw new InvalidOperationException("選択したシートに見出し行がありません。");
        var firstRow = used.FirstRow().RowNumber();
        var firstColumn = used.FirstColumn().ColumnNumber();
        var lastRow = used.LastRow().RowNumber();
        var lastColumn = used.LastColumn().ColumnNumber();
        var columns = new[] { blockColumnIndex, seatColumnIndex, circleIdColumnIndex };
        if (columns.Any(index => firstColumn + index > lastColumn))
            throw new InvalidOperationException("選択した列がシートの範囲外です。");

        var found = new HashSet<string>(StringComparer.Ordinal);
        var updatedRows = 0;
        for (var rowNumber = firstRow + 1; rowNumber <= lastRow; rowNumber++)
        {
            var circleId = worksheet.Cell(rowNumber, firstColumn + circleIdColumnIndex).GetFormattedString().Trim();
            if (!byCircleId.TryGetValue(circleId, out var row))
                continue;
            worksheet.Cell(rowNumber, firstColumn + blockColumnIndex).Value = row.BlockName;
            worksheet.Cell(rowNumber, firstColumn + seatColumnIndex).Value = row.SeatName;
            found.Add(circleId);
            updatedRows++;
        }
        workbook.Save();
        return new CircleSeatExportResult(updatedRows, found.Count, byCircleId.Count - found.Count);
    }
}
