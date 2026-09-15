namespace CircleSpaceCoordinator.Infrastructure.Tabular;

using System.Text;
using ClosedXML.Excel;

public sealed record PreparedCircleSeatTable(ParticipantTableSheet Sheet, CircleSeatExportResult Result);

public static class CircleSeatTableWriter
{
    public static PreparedCircleSeatTable Prepare(ParticipantTableSheet source, int block, int seat, int circle,
        IReadOnlyList<CircleSeatExportRow> assignments)
    {
        var columns = new[] { block, seat, circle };
        if (columns.Any(index => index < 0 || index >= source.Headers.Count))
            throw new InvalidOperationException("書出し列を選択してください。");
        if (columns.Distinct().Count() != 3)
            throw new InvalidOperationException("ブロック番号・セル番・サークルIDには別々の列を選択してください。");
        var byId = assignments.ToDictionary(row => row.CircleId.Trim(), StringComparer.Ordinal);
        var found = new HashSet<string>(StringComparer.Ordinal);
        var updated = 0;
        var rows = source.Rows.Select(row =>
        {
            var values = Enumerable.Range(0, source.Headers.Count).Select(index => row.ElementAtOrDefault(index) ?? "").ToArray();
            if (byId.TryGetValue(values[circle].Trim(), out var assignment))
            {
                values[block] = assignment.BlockName;
                values[seat] = assignment.SeatName;
                found.Add(assignment.CircleId.Trim());
                updated++;
            }
            return (IReadOnlyList<string>)values;
        }).ToArray();
        return new(new ParticipantTableSheet(source.Name, source.Headers, rows), new(updated, found.Count, byId.Count - found.Count));
    }

    public static void Create(string path, ParticipantTableSheet sheet)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".xlsx" or ".csv")) throw new NotSupportedException("新規書出しは .xlsx / .csv に対応しています。");
        if (File.Exists(path)) throw new IOException("同名のファイルが存在します。別の名前を選ぶか、既存ファイルへの書出しを選択してください。");
        var temporary = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, $".{Guid.NewGuid():N}{extension}");
        try
        {
            if (extension == ".csv")
            {
                static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
                using var writer = new StreamWriter(temporary, false, new UTF8Encoding(true));
                writer.WriteLine(string.Join(",", sheet.Headers.Select(Quote)));
                foreach (var row in sheet.Rows) writer.WriteLine(string.Join(",", row.Select(Quote)));
            }
            else
            {
                using var book = new XLWorkbook();
                var target = book.AddWorksheet("サークル一覧");
                for (var column = 0; column < sheet.Headers.Count; column++) target.Cell(1, column + 1).Value = sheet.Headers[column];
                for (var row = 0; row < sheet.Rows.Count; row++)
                    for (var column = 0; column < sheet.Headers.Count; column++)
                        target.Cell(row + 2, column + 1).Value = sheet.Rows[row].ElementAtOrDefault(column) ?? "";
                book.SaveAs(temporary);
            }
            File.Move(temporary, path, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
