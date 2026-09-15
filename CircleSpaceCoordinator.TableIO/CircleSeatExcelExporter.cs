namespace CircleSpaceCoordinator.Infrastructure.Tabular;

using ClosedXML.Excel;
using System.Text;
using Microsoft.VisualBasic.FileIO;

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
        IReadOnlyList<CircleSeatExportRow> rows,
        ParticipantCsvEncoding csvEncoding = ParticipantCsvEncoding.Utf8)
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
        if (Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            return ExportCsv(path, blockColumnIndex, seatColumnIndex, circleIdColumnIndex, byCircleId, csvEncoding);
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

    private static CircleSeatExportResult ExportCsv(string path, int block, int seat, int circle,
        IReadOnlyDictionary<string, CircleSeatExportRow> byCircleId, ParticipantCsvEncoding csvEncoding)
    {
        var bytes = File.ReadAllBytes(path);
        var bom = bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF });
        Encoding encoding = csvEncoding switch
        {
            ParticipantCsvEncoding.Utf8 => new UTF8Encoding(bom, true),
            ParticipantCsvEncoding.ShiftJis => CodePagesEncodingProvider.Instance.GetEncoding(
                932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)!,
            _ => throw new ArgumentOutOfRangeException(nameof(csvEncoding)),
        };
        var offset = csvEncoding == ParticipantCsvEncoding.Utf8 && bom ? 3 : 0;
        var text = encoding.GetString(bytes, offset, bytes.Length - offset);
        using var parser = new TextFieldParser(new StringReader(text))
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false,
        };
        parser.SetDelimiters(",");
        var records = new List<string[]>();
        while (!parser.EndOfData) records.Add(parser.ReadFields() ?? []);
        if (records.Count == 0 || new[] { block, seat, circle }.Any(index => index >= records[0].Length))
            throw new InvalidOperationException("選択した列が CSV の見出しの範囲外です。");
        var found = new HashSet<string>(StringComparer.Ordinal);
        var updated = 0;
        for (var i = 1; i < records.Count; i++)
        {
            var cells = records[i];
            if (circle >= cells.Length || !byCircleId.TryGetValue(cells[circle].Trim(), out var row)) continue;
            if (cells.Length <= Math.Max(block, seat))
            {
                var previousLength = cells.Length;
                Array.Resize(ref cells, Math.Max(block, seat) + 1);
                Array.Fill(cells, "", previousLength, cells.Length - previousLength);
                records[i] = cells;
            }
            cells[block] = row.BlockName;
            cells[seat] = row.SeatName;
            found.Add(row.CircleId.Trim());
            updated++;
        }
        static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var output = string.Join(newline, records.Select(record => string.Join(",", record.Select(Quote)))) + newline;
        // Encode fully before replacing the destination, so CP932 failures leave it untouched.
        var content = encoding.GetPreamble().Concat(encoding.GetBytes(output)).ToArray();
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, content);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
        return new CircleSeatExportResult(updated, found.Count, byCircleId.Count - found.Count);
    }

}
