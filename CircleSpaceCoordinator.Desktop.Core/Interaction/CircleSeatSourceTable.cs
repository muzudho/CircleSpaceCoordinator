namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Infrastructure.Tabular;

public static class CircleSeatSourceTable
{
    public static bool HasSameValues(CircleSpaceProject first, CircleSpaceProject second)
    {
        var a = new ParticipantTableView(first);
        var b = new ParticipantTableView(second);
        if (a.RowCount != b.RowCount || !a.Headers.SequenceEqual(b.Headers)) return false;
        for (var row = 0; row < a.RowCount; row++)
        {
            if (first.Participants[row].CircleId != second.Participants[row].CircleId) return false;
            for (var column = 0; column < a.ColumnCount; column++)
                if (a.GetValue(row, column) != b.GetValue(row, column)) return false;
        }
        return true;
    }

    public static (ParticipantTableSheet Sheet, int Block, int Seat, int Circle) Build(CircleSpaceProject project)
    {
        var view = new ParticipantTableView(project);
        var headers = view.Headers.ToList();
        var rows = Enumerable.Range(0, view.RowCount)
            .Select(row => Enumerable.Range(0, view.ColumnCount).Select(column => view.GetValue(row, column)).ToList()).ToArray();
        // Find the actual imported ID column by its values, including nonstandard headings.
        var circle = Enumerable.Range(0, view.ColumnCount).FirstOrDefault(column =>
            project.Participants.Count > 0 && project.Participants.Select((participant, row) =>
                string.Equals(rows[row][column].Trim(), participant.CircleId.Trim(), StringComparison.Ordinal)).All(match => match), -1);
        string Unique(string name)
        {
            var candidate = name;
            for (var suffix = 2; headers.Contains(candidate, StringComparer.Ordinal); suffix++) candidate = $"{name} ({suffix})";
            return candidate;
        }
        if (circle < 0)
        {
            circle = headers.Count;
            headers.Add(Unique("サークルID"));
            for (var row = 0; row < rows.Length; row++) rows[row].Add(project.Participants[row].CircleId);
        }
        var block = headers.Count;
        headers.Add(Unique("ブロック番号"));
        var seat = headers.Count;
        headers.Add(Unique("セル番号"));
        foreach (var row in rows) { row.Add(""); row.Add(""); }
        return (new("サークル一覧", headers, rows.Select(row => (IReadOnlyList<string>)row).ToArray()), block, seat, circle);
    }
}
