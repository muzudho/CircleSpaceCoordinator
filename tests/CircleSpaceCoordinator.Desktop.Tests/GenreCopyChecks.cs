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
