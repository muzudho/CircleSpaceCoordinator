namespace CircleSpaceCoordinator.Desktop.Tests;

using System.Text.Json;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Infrastructure.Json;

internal static partial class Program
{
    private static void NamedGenreCodeTablesRoundTrip()
    {
        var source = PortableExample() with
        {
            Name = "架空イベント2026", GenreCodeTableName = null,
            GenreStyles = [new("今年あり", "blue", "white", "solid"),
                new("今年なし", "red", "black", "diagonal-up") { KnowledgeComment = "来年も残す分類" }],
            GenreStyleComment = "年度をまたいで使う表",
            GenreCodeOrder = ["今年なし", "今年あり"], GenreCodeOrderComment = "過去の分類も含む",
        };
        AssertEqual("架空イベント2026_ジャンルコード表", source.GetGenreCodeTableName());
        using var legacyDocument = JsonDocument.Parse(ProjectJsonSerializer.Save(source));
        AssertEqual(source.GetGenreCodeTableName(), legacyDocument.RootElement.GetProperty("genreCodeTableName").GetString()!);
        var legacyJson = System.Text.Json.Nodes.JsonNode.Parse(ProjectJsonSerializer.Save(source))!;
        legacyJson.AsObject().Remove("genreCodeTableName");
        AssertEqual(source.GetGenreCodeTableName(), ProjectJsonSerializer.Load(legacyJson.ToJsonString()).GetGenreCodeTableName());

        using var sender = EditorConnection.Current.Open(ProjectJsonSerializer.Save(source));
        var date = new DateOnly(2026, 9, 19);
        sender.Execute(new SetGenreStyles(sender.Project.GenreStyles)
        { GenreCodeTableName = "  通年のジャンル表😀  ", ActorHandle = "editor", WorkDate = date, ChangeLog = "表名を設定" }, selectedPlanEdit: false);
        AssertEqual("通年のジャンル表😀", sender.Project.GetGenreCodeTableName());
        AssertEqual("表名を設定", sender.Project.GenreStyleCredits!.ChangeLog!);
        sender.Undo(); AssertEqual(source.GetGenreCodeTableName(), sender.Project.GetGenreCodeTableName());
        sender.Redo(); AssertEqual("通年のジャンル表😀", sender.Project.GetGenreCodeTableName());
        var beforeInvalid = ProjectJsonSerializer.Save(sender.Project);
        foreach (var invalid in new[] { "   ", "名前\n改行", new string('a', 1001) })
            RejectPortable(() => sender.Execute(new SetGenreStyles(sender.Project.GenreStyles) { GenreCodeTableName = invalid }, selectedPlanEdit: false));
        AssertEqual(beforeInvalid, ProjectJsonSerializer.Save(sender.Project));

        var json = EditorConnection.Current.ExportPortable(new(sender.Project, [], new([], []), "単独表", "", [], false)
        { Materials = [new("genre-styles", "project")], Handle = "provider" });
        var package = EditorConnection.Current.ParsePortable(json);
        AssertEqual(0, package.Items.Count);
        AssertEqual(0, package.Knowledge.Count);
        AssertEqual(1, package.Materials.Count);
        var material = package.Materials.Single();
        AssertEqual("通年のジャンル表😀", material.Name);
        AssertEqual(2, material.GenreStyles!.Length);
        AssertEqual("来年も残す分類", material.GenreStyles.Single(row => row.GenreId == "今年なし").KnowledgeComment!);
        AssertEqual("過去の分類も含む", material.GenreCodeOrderComment!);

        using var receiver = EditorConnection.Current.Open(ProjectJsonSerializer.Save(PortableExample() with { Participants = [] }));
        var original = ProjectJsonSerializer.Save(receiver.Project);
        var layouts = JsonSerializer.Serialize(receiver.Project.DeskLayouts);
        var circles = JsonSerializer.Serialize(receiver.Project.CircleLayouts);
        var selection = new PortableImportItem(material.Id, "table-import", "次年度のジャンル表");
        var preview = EditorConnection.Current.PreviewPortable(new(receiver.Project, package, [selection]));
        AssertEqual(0, preview.LayoutCount);
        AssertEqual(1, preview.MaterialCount);
        receiver.Execute(new ImportPortableSelection(package, [selection]), selectedPlanEdit: false);
        AssertEqual("次年度のジャンル表", receiver.Project.GetGenreCodeTableName());
        AssertEqual(2, receiver.Project.GenreStyles.Count);
        AssertEqual(2, new GenreStyleDraft(receiver.Project).Build().Length);
        AssertEqual(layouts, JsonSerializer.Serialize(receiver.Project.DeskLayouts));
        AssertEqual(circles, JsonSerializer.Serialize(receiver.Project.CircleLayouts));
        AssertEqual("今年なし,今年あり", string.Join(',', receiver.Project.GenreCodeOrder));
        AssertEqual(material.Credits, receiver.Project.GenreStyleCredits);
        var saved = ProjectJsonSerializer.Save(receiver.Project);
        AssertEqual("次年度のジャンル表", ProjectJsonSerializer.Load(saved).GetGenreCodeTableName());
        receiver.Undo(); AssertEqual(original, ProjectJsonSerializer.Save(receiver.Project));
        receiver.Redo(); AssertEqual(saved, ProjectJsonSerializer.Save(receiver.Project));
        var again = EditorConnection.Current.ParsePortable(EditorConnection.Current.ExportPortable(new(receiver.Project, [], new([], []), "再配布", "", [], false)
        { Materials = [new("genre-styles", "project")], Handle = "receiver" }));
        AssertEqual("次年度のジャンル表", again.Materials.Single().Name);
        AssertEqual(2, again.Materials.Single().GenreStyles!.Length);
    }
}
