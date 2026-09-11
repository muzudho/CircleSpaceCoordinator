namespace CircleSpaceCoordinator.Infrastructure.Tabular;

using System.Text;
using ClosedXML.Excel;
using Microsoft.VisualBasic.FileIO;

public sealed record ParticipantTableSheet(
    string Name,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows)
{
    // Preserve numeric precision for evaluation even when Excel displays rounded
    // values or percentages. The ordinary rows still preserve formatted IDs.
    public IReadOnlyList<IReadOnlyList<string>>? ChannelRows { get; init; }
}

public static class ParticipantTableReader
{
    public static IReadOnlyList<ParticipantTableSheet> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".xlsx" or ".xlsm" => ReadWorkbook(path),
            ".csv" => [ReadCsv(path)],
            _ => throw new NotSupportedException("Only .xlsx, .xlsm and .csv participant lists are supported."),
        };
    }

    private static IReadOnlyList<ParticipantTableSheet> ReadWorkbook(string path)
    {
        using var workbook = new XLWorkbook(path);
        return workbook.Worksheets
            .Select(ReadWorksheet)
            .Where(sheet => sheet.Headers.Count > 0)
            .ToArray();
    }

    private static ParticipantTableSheet ReadWorksheet(IXLWorksheet worksheet)
    {
        var used = worksheet.RangeUsed();
        if (used is null)
            return new ParticipantTableSheet(worksheet.Name, [], []);

        var firstRow = used.FirstRow().RowNumber();
        var firstColumn = used.FirstColumn().ColumnNumber();
        var lastColumn = used.LastColumn().ColumnNumber();
        var lastRow = used.LastRow().RowNumber();
        var headers = Enumerable.Range(firstColumn, lastColumn - firstColumn + 1)
            .Select(column => worksheet.Cell(firstRow, column).GetFormattedString().Trim())
            .ToArray();
        var rows = Enumerable.Range(firstRow + 1, Math.Max(0, lastRow - firstRow))
            .Select(row =>
            {
                var cells = Enumerable.Range(firstColumn, headers.Length).Select(column => worksheet.Cell(row, column)).ToArray();
                return (Display: (IReadOnlyList<string>)cells.Select(cell => cell.GetFormattedString().Trim()).ToArray(),
                    Channel: (IReadOnlyList<string>)cells.Select(cell => cell.DataType == XLDataType.Number
                        ? cell.GetDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                        : cell.GetFormattedString().Trim()).ToArray());
            })
            .Where(row => row.Display.Any(value => !string.IsNullOrWhiteSpace(value)))
            .ToArray();
        return new ParticipantTableSheet(worksheet.Name, headers, rows.Select(row => row.Display).ToArray())
        { ChannelRows = rows.Select(row => row.Channel).ToArray() };
    }

    private static ParticipantTableSheet ReadCsv(string path)
    {
        using var parser = new TextFieldParser(path, Encoding.UTF8, detectEncoding: true)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true,
        };
        parser.SetDelimiters(",");
        var records = new List<string[]>();
        while (!parser.EndOfData)
            records.Add(parser.ReadFields() ?? []);
        if (records.Count == 0)
            return new ParticipantTableSheet(Path.GetFileNameWithoutExtension(path), [], []);

        var width = records.Max(row => row.Length);
        var headers = Pad(records[0], width);
        var rows = records.Skip(1)
            .Select(row => (IReadOnlyList<string>)Pad(row, width))
            .Where(row => row.Any(value => !string.IsNullOrWhiteSpace(value)))
            .ToArray();
        return new ParticipantTableSheet(Path.GetFileNameWithoutExtension(path), headers, rows);
    }

    private static string[] Pad(IReadOnlyList<string> source, int width) => Enumerable.Range(0, width)
        .Select(index => index < source.Count ? source[index].Trim() : "")
        .ToArray();
}
