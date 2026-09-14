namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using System.Globalization;

/// <summary>Read-only participant cell content and Unicode-safe selection, independent of its window.</summary>
public sealed class ParticipantCellText
{
    private readonly string[] lines;
    private readonly Dictionary<int, int[]> boundaries = [];
    public ParticipantCellText(string text) => lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    public IReadOnlyList<string> Lines => lines;
    public int[] Boundaries(int row)
    {
        if (!boundaries.TryGetValue(row, out var result))
            boundaries[row] = result = StringInfo.ParseCombiningCharacters(lines[row]).Append(lines[row].Length).ToArray();
        return result;
    }
    public (int Row, int Column) ClampPosition((int Row, int Column) position)
    {
        var row = Math.Clamp(position.Row, 0, lines.Length - 1);
        var column = Math.Clamp(position.Column, 0, lines[row].Length);
        var index = Array.BinarySearch(Boundaries(row), column);
        return (row, index >= 0 ? column : Boundaries(row)[Math.Max(0, ~index - 1)]);
    }
    public string Select((int Row, int Column) anchor, (int Row, int Column) end)
    {
        var a = ClampPosition(anchor);
        var b = ClampPosition(end);
        if (a.CompareTo(b) > 0) (a, b) = (b, a);
        return string.Join(Environment.NewLine, Enumerable.Range(a.Row, b.Row - a.Row + 1).Select(row =>
            lines[row][(row == a.Row ? a.Column : 0)..(row == b.Row ? b.Column : lines[row].Length)]));
    }
}
