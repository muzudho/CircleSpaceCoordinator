namespace CircleSpaceCoordinator.Desktop.Tests;

using CircleSpaceCoordinator.Desktop.Core.Interaction;

internal static partial class Program
{
    private static void GenreCopyRows()
    {
        var source = new StyleMappingEntry("RPG", "blue", "yellow", "diagonal-up") { KnowledgeComment = "説明😀" };
        var original = new StyleMappingEntry("RPG", "red", "white", "solid");
        var draft = new StyleMappingDraft(["RPG"], [original]);
        var rejected = false;
        try { draft.CopyRow(source, "RPG", false); }
        catch (InvalidOperationException) { rejected = true; }
        AssertEqual(true, rejected);
        AssertEqual(false, draft.HasChanges);
        // Insert at the selected position, even when storage order is not alphabetical.
        var rows = new StyleMappingDraft(["A", "B", "新しいジャンルコード", "新しいジャンルコード_2"], []);
        rows.ReorderRows(["B", "A", "新しいジャンルコード", "新しいジャンルコード_2"]);
        var added = rows.InsertNewGenreRow(1);
        AssertEqual("新しいジャンルコード_3", added);
        AssertEqual("B", rows.Rows[0].Key);
        AssertEqual(added, rows.Rows[1].Key);
        AssertEqual("A", rows.Rows[2].Key);
        AssertEqual(true, rows.HasChanges);
        var rebuilt = new StyleMappingDraft(rows.Rows.Select(row => row.Key), rows.Build());
        rebuilt.ReorderRows(rows.Rows.Select(row => row.Key));
        AssertEqual(true, rows.Rows.SequenceEqual(rebuilt.Rows));
        rows.DeleteRow(1);
        AssertEqual("A", rows.Rows[1].Key);
        AssertEqual(false, rows.Build().Any(row => row.Key == added));
        rows.RestoreOpeningSnapshot();
        AssertEqual(false, rows.HasChanges);
        var empty = new StyleMappingDraft([], []);
        AssertEqual("新しいジャンルコード", empty.InsertNewGenreRow(0));
        empty.DeleteRow(0);
        AssertEqual(0, empty.Build().Length);
        AssertEqual("新しいジャンルコード", empty.InsertNewGenreRow(0));
        var hidden = new StyleMappingDraft([], [original with { Key = "新しいジャンルコード" }]);
        AssertEqual("新しいジャンルコード_2", hidden.InsertNewGenreRow(0));
        AssertEqual(2, hidden.Build().Length);
        draft.RenameRow(0, " 新しい名前😀 ");
        AssertEqual(original with { Key = "新しい名前😀" }, draft.Build().Single());
        AssertEqual(true, draft.HasChanges);
        draft.CopyRow(source, "重複", false);
        var beforeRename = draft.Build();
        rejected = false;
        try { draft.RenameRow(0, "重複"); }
        catch (InvalidOperationException) { rejected = true; }
        AssertEqual(true, rejected);
        AssertEqual(true, beforeRename.SequenceEqual(draft.Build()));
        rejected = false;
        try { draft.RenameRow(0, "  "); }
        catch (ArgumentException) { rejected = true; }
        AssertEqual(true, rejected);
        AssertEqual(true, beforeRename.SequenceEqual(draft.Build()));
        draft.RestoreOpeningSnapshot();
        AssertEqual(original, draft.Build().Single());
        AssertEqual(false, draft.HasChanges);
        draft.CopyRow(source, "RPG", true);
        AssertEqual(source, draft.Rows.Single());
        AssertEqual(true, draft.HasChanges);
        draft.CopyRow(source, " 別のRPG ", false);
        AssertEqual(2, draft.Rows.Count);
        AssertEqual(source with { Key = "別のRPG" }, draft.Rows[1]);
        AssertEqual("RPG", source.Key);
        rejected = false;
        try { draft.CopyRow(source with { PrimaryColor = "invalid" }, "RPG", true); }
        catch (InvalidDataException) { rejected = true; }
        AssertEqual(true, rejected);
        AssertEqual(source, draft.Rows[0]);
        AssertEqual(2, draft.Build().Length);
        draft.RestoreOpeningSnapshot();
        AssertEqual(original, draft.Rows.Single());
        AssertEqual(false, draft.HasChanges);
    }
}
