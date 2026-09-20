namespace CircleSpaceCoordinator.Desktop.Tests;

using System.Text.Json.Nodes;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Infrastructure.Json;

internal static partial class Program
{
    private static void GenreTableDiffTradesAndPersistence()
    {
        var project = PortableExample() with
        {
            Participants = [new("p", "架空サークル", 1, new Dictionary<string, double>()) { GenreId = "B" }],
            GenreCodeTableName = "現在の表", GenreCodeOrder = ["B", "A"],
            GenreStyles = [new("B", "blue", "white", "solid"), new("A", "red", "white", "grid") { KnowledgeComment = "左の知見" }],
        };
        var material = new PortableMaterial("table", "genre-styles", "配布表", false)
        {
            GenreStyles = [new("C", "cyan", "black", "diagonal-up"), new("A", "green", "black", "grid") { KnowledgeComment = "右の知見" }],
            GenreCodeOrder = ["C", "A"], OverallComment = "パッケージの全体コメント", GenreCodeOrderComment = "意味のある並び順",
        };
        var draft = new GenreTableDiffDraft(project, material, false);
        AssertEqual("A,B,C", string.Join(',', draft.Rows.Select(row => row.Code)));
        AssertEqual("相違,左のみ,右のみ", string.Join(',', draft.Rows.Select(row => row.Difference)));
        AssertEqual(false, draft.LeftChanged || draft.RightChanged);
        draft.CopyTo(GenreTableSide.Right, "B", false);
        draft.CopyTo(GenreTableSide.Left, "C", false);
        AssertEqual(true, draft.LeftChanged && draft.RightChanged);
        draft.CopyTo(GenreTableSide.Right, "A", true);
        AssertEqual("左の知見", draft.Right.GenreStyles!.Single(row => row.GenreId == "A").KnowledgeComment!);
        draft.Undo();
        AssertEqual("右の知見", draft.Right.GenreStyles!.Single(row => row.GenreId == "A").KnowledgeComment!);
        draft.CopyTo(GenreTableSide.Left, "A", true);
        AssertEqual(false, draft.CanRedo);
        AssertEqual("green", draft.Left.GenreStyles!.Single(row => row.GenreId == "A").PrimaryColor);
        draft.Delete(GenreTableSide.Left, "B");
        draft.Delete(GenreTableSide.Right, "C");
        AssertEqual(false, draft.Left.GenreCodeOrder!.Contains("B"));
        AssertEqual(false, draft.Right.GenreCodeOrder!.Contains("C"));
        draft.Rename(GenreTableSide.Left, "C", "a");
        draft.Rename(GenreTableSide.Right, "B", "😀");
        AssertEqual("A,a,😀", string.Join(',', draft.Rows.Select(row => row.Code)));
        AssertEqual("A,😀", string.Join(',', draft.Right.GenreCodeOrder!));
        var before = draft.Rows.ToArray();
        RejectPortable(() => draft.Rename(GenreTableSide.Left, "a", "A"));
        RejectPortable(() => draft.Rename(GenreTableSide.Left, "a", "\n"));
        RejectPortable(() => draft.CopyTo(GenreTableSide.Left, "A", false));
        RejectPortable(() => draft.CopyTo(GenreTableSide.Left, "😀", true));
        RejectPortable(() => draft.Delete(GenreTableSide.Left, "missing"));
        AssertEqual(true, before.SequenceEqual(draft.Rows));
        draft.Undo(); AssertEqual(true, draft.Contains(GenreTableSide.Right, "B"));
        draft.Redo(); AssertEqual(true, draft.Contains(GenreTableSide.Right, "😀"));
        AssertEqual("B,A", string.Join(',', project.GenreStyles.Select(row => row.GenreId)));
        AssertEqual("C,A", string.Join(',', material.GenreStyles!.Select(row => row.GenreId)));

        using var owner = EditorConnection.Current.Open(ProjectJsonSerializer.Save(project));
        var original = ProjectJsonSerializer.Save(owner.Project);
        owner.Execute(new SetGenreStyles(draft.Left.GenreStyles!)
        {
            GenreCodeOrder = draft.Left.GenreCodeOrder, UpdateGenreCodeOrder = true,
            ActorHandle = "比較担当", WorkDate = new(2026, 9, 19), ChangeLog = "個別に採用・整理",
        }, selectedPlanEdit: false);
        AssertEqual("A,a", string.Join(',', owner.Project.GenreStyles.Select(row => row.GenreId).Order(StringComparer.Ordinal)));
        AssertEqual("個別に採用・整理", owner.Project.GenreStyleCredits!.ChangeLog!);
        AssertEqual("B", owner.Project.Participants.Single().GenreId!);
        draft.MarkSaved(GenreTableSide.Left);
        AssertEqual(false, draft.LeftChanged);
        AssertEqual(true, draft.RightChanged);
        owner.Undo(); AssertEqual(original, ProjectJsonSerializer.Save(owner.Project));
        owner.Redo(); AssertEqual(2, owner.Project.GenreStyles.Count);

        var secret = new GenreTableDiffDraft(project with { IsConfidential = true }, material, false);
        secret.CopyTo(GenreTableSide.Right, "B", false);
        AssertEqual(true, secret.Right.IsConfidential);
        secret.MarkSaved(GenreTableSide.Right, true);
        AssertEqual(false, secret.RightChanged);
        secret.Undo();
        secret.MarkSaved(GenreTableSide.Right, true);
        AssertEqual(true, secret.Right.IsConfidential);
        secret.CopyTo(GenreTableSide.Right, "B", false);
        var incomingSecret = new GenreTableDiffDraft(project, material, true);
        incomingSecret.CopyTo(GenreTableSide.Left, "C", false);
        AssertEqual(true, incomingSecret.Left.IsConfidential);
        owner.Execute(new SetGenreStyles(incomingSecret.Left.GenreStyles!) { MarkConfidential = true }, selectedPlanEdit: false);
        AssertEqual(true, owner.Project.IsConfidential);

        var json = ProjectPortableSerializer.Save("パッケージ全体", "残すメモ", ["知見"], false,
            [CircleSpaceCoordinator.Application.Layouts.PortableSelectionService.Extract(project, "layout", new([], []))], materials: [material,
                new("blocks", "block-styles", "別の表", false) { BlockStyles = [] }]);
        var connection = EditorConnection.Current;
        var changed = connection.UpdatePortableGenreTable(new(json, draft.Right, "整理担当", new(2026, 9, 19), "知見を追加して整理"));
        var beforeJson = JsonNode.Parse(json)!;
        var afterJson = JsonNode.Parse(changed)!;
        AssertEqual(true, JsonNode.DeepEquals(beforeJson["items"], afterJson["items"]));
        AssertEqual(true, JsonNode.DeepEquals(beforeJson["materials"]![1], afterJson["materials"]![1]));
        foreach (var key in new[] { "name", "description", "tags", "kind", "formatVersion" })
            AssertEqual(true, JsonNode.DeepEquals(beforeJson[key], afterJson[key]));
        var saved = connection.ParsePortable(changed).Materials.Single(item => item.Id == "table");
        AssertEqual("パッケージの全体コメント", saved.OverallComment!);
        AssertEqual("意味のある並び順", saved.GenreCodeOrderComment!);
        AssertEqual("A,😀", string.Join(',', saved.GenreStyles!.Select(row => row.GenreId).Order(StringComparer.Ordinal)));
        AssertEqual("整理担当", saved.Credits!.Modifier!);
        AssertEqual("知見を追加して整理", saved.Credits.ChangeLog!);
        var confidentialJson = connection.UpdatePortableGenreTable(new(json, secret.Right, "担当", new(2026, 9, 19), "マル秘の知見を追加"));
        AssertEqual(true, connection.ParsePortable(confidentialJson).IsConfidential);
        RejectPortable(() => connection.UpdatePortableGenreTable(new(json, draft.Right with { Id = "missing" }, "担当", new(2026, 9, 19), "変更")));
        RejectPortable(() => connection.UpdatePortableGenreTable(new(json, draft.Right, "担当", new(2026, 9, 19), "改行\n禁止")));

        var directory = Path.Combine(Path.GetTempPath(), "csc-diff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "test.package-csc.json");
            File.WriteAllText(path, json);
            var entry = new PortableLibraryEntry(path, json, connection.ParsePortable(json), null);
            PortableLibraryService.SaveMetadata(entry, changed);
            AssertEqual(changed, File.ReadAllText(path));
            RejectPortable(() => PortableLibraryService.SaveMetadata(entry, json));
            AssertEqual(changed, File.ReadAllText(path));
            draft.MarkSaved(GenreTableSide.Right); AssertEqual(false, draft.RightChanged);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
