namespace CircleSpaceCoordinator.Desktop.Tests;

using CircleSpaceCoordinator.Desktop.Core.Interaction;

internal static partial class Program
{
    private static void ShadingMultipleSelectionAndBatches()
    {
        var selection = new ShadingRowSelection();
        var rows = Enumerable.Range(0, 20).Select(index => "row" + index).ToArray();
        selection.Click(false, rows, rows[2], true, false);
        selection.Click(false, rows, rows[8], true, false);
        AssertEqual(true, selection.Keys.SetEquals(rows.Skip(2).Take(7)));
        selection.Click(false, rows, rows[15], false, true);
        AssertEqual(rows[15], selection.Anchor!);
        selection.Click(false, rows, rows[18], true, true);
        AssertEqual(true, selection.Keys.SetEquals(rows.Skip(2).Take(7).Concat(rows.Skip(15).Take(4))));
        AssertEqual(false, selection.Click(true, rows, rows[0], false, false));
        AssertEqual(false, selection.Right);
        selection.Click(false, rows, rows[15], false, true);
        AssertEqual(false, selection.Keys.Contains(rows[15]));
        selection.Click(false, rows, rows[10], false, false);
        AssertEqual(false, selection.Multiple);
        AssertEqual(true, selection.Click(true, rows, rows[12], false, false));
        selection.Click(true, rows, rows[7], true, false);
        AssertEqual(true, selection.Keys.SetEquals(rows.Skip(7).Take(6)));
        AssertEqual(true, selection.IsLocked(false));
        // Shrinking a Shift range replaces only the current selection when Ctrl is not held.
        selection.Click(true, rows, rows[10], true, false);
        AssertEqual(true, selection.Keys.SetEquals(rows.Skip(10).Take(3)));
        selection.Clear();
        AssertEqual(false, selection.Multiple);
        selection.Click(false, rows, rows[0], false, true);
        selection.Click(false, rows, rows[0], false, true);
        AssertEqual(0, selection.Keys.Count);
        AssertEqual(true, selection.Active is null);

        var red = new StyleMappingEntry("red", "red", "white", "solid") { KnowledgeComment = "赤の補足" };
        var blue = new StyleMappingEntry("blue", "blue", "black", "diagonal-up") { KnowledgeComment = "青の補足" };
        var target = new StyleMappingDraft(["blue"], [blue], "表の補足");
        var before = target.Build();
        var failed = false;
        try { target.CopyRows([(red, "red", false), (blue, "blue", false)]); }
        catch (InvalidOperationException) { failed = true; }
        AssertEqual(true, failed);
        AssertEqual(true, before.SequenceEqual(target.Build()));
        AssertEqual(false, target.HasChanges);
        target.CopyRows([(red, "red", false), (red, "blue", true)]);
        AssertEqual(2, target.Rows.Count);
        AssertEqual(true, target.Rows.All(row => row.KnowledgeComment == "赤の補足"));
        AssertEqual("表の補足", target.OverallComment!);
        target.RestoreOpeningSnapshot();
        AssertEqual(true, before.SequenceEqual(target.Build()));
        target.CopyRows([(red, "red", false), (blue, "renamed", false)]);
        target.DeleteRows(["blue", "renamed"]);
        AssertEqual("red", target.Rows.Single().Key);
        failed = false;
        try { target.DeleteRows(["red", "missing"]); }
        catch (InvalidOperationException) { failed = true; }
        AssertEqual(true, failed);
        AssertEqual("red", target.Rows.Single().Key);
        target.DeleteRows(["red"]);
        AssertEqual(0, target.Rows.Count);
        target.RestoreOpeningSnapshot();
        AssertEqual(true, before.SequenceEqual(target.Build()));
    }
}
