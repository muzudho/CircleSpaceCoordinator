namespace CircleSpaceCoordinator.Desktop.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Infrastructure.Json;

internal static partial class Program
{
    private static void SharedShadingTablesRoundTrip()
    {
        var connection = EditorConnection.Current;
        var source = PortableExample() with
        {
            BlockStyles = [new("B", "red", "white", "solid") { KnowledgeComment = "未使用でも残す" },
                new("A", "blue", "black", "diagonal-up")],
            BlockStyleTable = new() { Name = "ブロックの表", OverallComment = "表のコメント",
                RowOrder = ["B", "A"], OrderComment = "入場順" },
        };
        var saved = ProjectJsonSerializer.Save(source);
        var loaded = ProjectJsonSerializer.Load(saved);
        AssertEqual("ブロックの表", loaded.GetBlockStyleTableName());
        AssertEqual("表のコメント", loaded.BlockStyleTable.OverallComment!);
        AssertEqual("B,A", string.Join(',', loaded.BlockStyleTable.RowOrder));
        AssertEqual("入場順", loaded.BlockStyleTable.OrderComment!);
        AssertEqual("未使用でも残す", loaded.BlockStyles.Single(row => row.BlockNumber == "B").KnowledgeComment!);
        var legacy = JsonNode.Parse(saved)!.AsObject();
        legacy.Remove("blockStyleTable");
        foreach (var row in legacy["blockStyles"]!.AsArray()) row!.AsObject().Remove("knowledgeComment");
        var old = ProjectJsonSerializer.Load(legacy.ToJsonString());
        AssertEqual(0, old.BlockStyleTable.RowOrder.Count);
        AssertEqual(2, ShadingTable.CreateDraft(old, true).Rows.Count(row => row.Key is "A" or "B"));

        using var workspace = connection.Open(saved);
        var before = ProjectJsonSerializer.Save(workspace.Project);
        var genre = JsonSerializer.Serialize(workspace.Project.GenreStyles);
        var layouts = JsonSerializer.Serialize(workspace.Project.DeskLayouts);
        var draft = ShadingTable.CreateDraft(workspace.Project, true);
        draft.ReorderRows(source.BlockStyleTable.RowOrder);
        var key = draft.InsertNewGenreRow(0, "新しいブロック");
        AssertEqual("新しいブロック", key);
        AssertEqual("新しいブロック_2", draft.InsertNewGenreRow(1, "新しいブロック"));
        draft.SetOverallComment("編集したコメント");
        var table = new PortableMaterial("blocks", "block-styles", "新しい表名", false)
            { BlockStyles = [], TableMetadata = source.BlockStyleTable };
        table = table.WithRows(draft.Build()).WithOrder(draft.Rows.Select(row => row.Key).ToArray());
        var date = new DateOnly(2026, 9, 20);
        var credits = new PersonCredits().WrittenBy("test", date, "表の変更");
        workspace.Execute(new SetBlockStyles(table.BlockStyles!)
        {
            Table = table.Metadata() with { OverallComment = draft.OverallComment },
            UpdateCredits = true, Credits = credits,
        }, selectedPlanEdit: false);
        AssertEqual("新しい表名", workspace.Project.GetBlockStyleTableName());
        AssertEqual("編集したコメント", workspace.Project.BlockStyleTable.OverallComment!);
        AssertEqual("表の変更", workspace.Project.BlockStyleCredits!.ChangeLog!);
        AssertEqual(genre, JsonSerializer.Serialize(workspace.Project.GenreStyles));
        AssertEqual(layouts, JsonSerializer.Serialize(workspace.Project.DeskLayouts));
        var after = ProjectJsonSerializer.Save(workspace.Project);
        workspace.Undo(); AssertEqual(before, ProjectJsonSerializer.Save(workspace.Project));
        workspace.Redo(); AssertEqual(after, ProjectJsonSerializer.Save(workspace.Project));

        var json = connection.ExportPortable(new(workspace.Project, [], new([], []), "共有", "", [], false)
        { Materials = [new("block-styles", "project"), new("genre-styles", "project")], Handle = "test" });
        var package = connection.ParsePortable(json);
        var blocks = package.Materials.Single(item => item.Kind == "block-styles");
        AssertEqual("編集したコメント", blocks.Metadata().OverallComment!);
        AssertEqual("新しいブロック", blocks.Metadata().RowOrder[0]);
        var directory = Path.Combine(Path.GetTempPath(), "csc-shading-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "tables.package-csc.json");
            File.WriteAllText(path, json);
            var reader = new PackageReadDialogModel(connection.ParsePortable, "block-styles");
            reader.SelectPath(path);
            AssertEqual(true, reader.CanRead);
            AssertEqual(1, reader.Tables.Length);
            AssertEqual("block-styles", reader.Tables[0].Kind);
            var session = new PackageGenreSaveSession(path, json, package, blocks);
            var changed = blocks.WithRows(blocks.Rows().Select(row => row with { KnowledgeComment = "共有編集" }));
            // A populated change comment needs explicit overwrite approval for both table kinds.
            session.ApprovedOverwriteJson = json;
            session.Save(changed, "test", date, "", false, connection.UpdatePortableGenreTable, connection.ParsePortable);
            AssertEqual(true, PackageGenreSaveSession.HasMissingComment(session.CurrentTable));
            session.Save(changed, "test", date, "完了", false, connection.UpdatePortableGenreTable, connection.ParsePortable);
            var result = connection.ParsePortable(File.ReadAllText(path));
            AssertEqual("共有編集", result.Materials.Single(item => item.Kind == "block-styles").Rows()[0].KnowledgeComment!);
            AssertEqual(JsonSerializer.Serialize(package.Materials.Single(item => item.Kind == "genre-styles")),
                JsonSerializer.Serialize(result.Materials.Single(item => item.Kind == "genre-styles")));
            var conflict = false;
            try { session.Save(changed, "test", date, "再変更", false, connection.UpdatePortableGenreTable, connection.ParsePortable); }
            catch (PackageCommentConflictException) { conflict = true; }
            AssertEqual(true, conflict);
            session.Save(blocks, "test", date, "", true, connection.UpdatePortableGenreTable, connection.ParsePortable);
            AssertEqual(json, File.ReadAllText(path));
        }
        finally { Directory.Delete(directory, recursive: true); }
        using var receiver = connection.Open(ProjectJsonSerializer.Save(PortableExample()));
        receiver.Execute(new ImportPortableSelection(package, [new(blocks.Id, "imported", "取り込み表")]), selectedPlanEdit: false);
        AssertEqual("取り込み表", receiver.Project.GetBlockStyleTableName());
        AssertEqual("編集したコメント", receiver.Project.BlockStyleTable.OverallComment!);
        AssertEqual("新しいブロック", receiver.Project.BlockStyleTable.RowOrder[0]);
    }
}
