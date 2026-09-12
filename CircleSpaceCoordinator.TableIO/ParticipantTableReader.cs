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

public enum ParticipantCsvEncoding
{
    Utf8,
    ShiftJis,
}

public sealed record ParticipantCsvPreview(ParticipantTableSheet Sheet, bool HasDecodingErrors);

public static class ParticipantTableReader
{
    public static IReadOnlyList<ParticipantTableSheet> Read(string path, ParticipantCsvEncoding csvEncoding = ParticipantCsvEncoding.Utf8)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".xlsx" or ".xlsm" => ReadWorkbook(path),
            ".csv" => [ReadCsv(path, csvEncoding)],
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

    private static ParticipantTableSheet ReadCsv(string path, ParticipantCsvEncoding csvEncoding)
    {
        if (TryReadCsv(path, csvEncoding, out var sheet))
            return sheet!;
        throw new DecoderFallbackException("The CSV contains bytes that cannot be decoded using the selected encoding.");
    }

    // Encoding mismatch is an ordinary preview result, not an exception that stops a debugger.
    public static bool TryReadCsv(string path, ParticipantCsvEncoding csvEncoding, out ParticipantTableSheet? sheet)
    {
        var preview = ReadCsvPreview(path, csvEncoding);
        sheet = preview.HasDecodingErrors ? null : preview.Sheet;
        return !preview.HasDecodingErrors;
    }

    public static ParticipantCsvPreview ReadCsvPreview(string path, ParticipantCsvEncoding csvEncoding)
    {
        var encoding = csvEncoding switch
        {
            // Recognize the UTF-8 preamble while retaining strict decoding for BOM files too.
            ParticipantCsvEncoding.Utf8 => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true),
            ParticipantCsvEncoding.ShiftJis => CodePagesEncodingProvider.Instance.GetEncoding(
                932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)!,
            _ => throw new ArgumentOutOfRangeException(nameof(csvEncoding)),
        };
        var fallback = new TrackingDecoderFallback();
        encoding = (Encoding)encoding.Clone();
        encoding.DecoderFallback = fallback;
        string text;
        using (var reader = new StreamReader(path, encoding, detectEncodingFromByteOrderMarks: false))
            text = reader.ReadToEnd();
        using var parser = new TextFieldParser(new StringReader(text))
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
        {
            return new ParticipantCsvPreview(
                new ParticipantTableSheet(Path.GetFileNameWithoutExtension(path), [], []), fallback.HadInvalidBytes);
        }

        var width = records.Max(row => row.Length);
        var headers = Pad(records[0], width);
        var rows = records.Skip(1)
            .Select(row => (IReadOnlyList<string>)Pad(row, width))
            .Where(row => row.Any(value => !string.IsNullOrWhiteSpace(value)))
            .ToArray();
        return new ParticipantCsvPreview(
            new ParticipantTableSheet(Path.GetFileNameWithoutExtension(path), headers, rows), fallback.HadInvalidBytes);
    }

    private sealed class TrackingDecoderFallback : DecoderFallback
    {
        public bool HadInvalidBytes { get; private set; }
        public override int MaxCharCount => 1;
        public override DecoderFallbackBuffer CreateFallbackBuffer() => new Buffer(this);

        private sealed class Buffer(TrackingDecoderFallback owner) : DecoderFallbackBuffer
        {
            private readonly DecoderFallbackBuffer replacement = new DecoderReplacementFallback("\uFFFD").CreateFallbackBuffer();
            public override bool Fallback(byte[] bytesUnknown, int index)
            {
                owner.HadInvalidBytes = true;
                return replacement.Fallback(bytesUnknown, index);
            }
            public override char GetNextChar() => replacement.GetNextChar();
            public override bool MovePrevious() => replacement.MovePrevious();
            public override int Remaining => replacement.Remaining;
            public override void Reset() => replacement.Reset();
        }
    }

    private static string[] Pad(IReadOnlyList<string> source, int width) => Enumerable.Range(0, width)
        .Select(index => index < source.Count ? source[index].Trim() : "")
        .ToArray();
}
