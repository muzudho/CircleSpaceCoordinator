namespace CircleSpaceCoordinator.Desktop.Tests;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Infrastructure.Json;

internal static partial class Program
{
    private static void GenreAutoSaveRestoration()
    {
        var connection = EditorConnection.Current;
        var project = PortableExample() with { GenreStyles = [new("A", "red", "white", "solid")] };
        var json = connection.ExportPortable(new(project, [], new([], []), "バックアップ検証", "メモ", [], false)
        { Materials = [new("genre-styles", "project")], Handle = "test" });
        var package = connection.ParsePortable(json);
        var original = package.Materials.Single();
        var directory = Path.Combine(Path.GetTempPath(), "csc-genre-autosave-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "test.package-csc.json");
            File.WriteAllText(path, json);
            var session = new PackageGenreSaveSession(path, json, package, original);
            var edited = original with { GenreStyles = [.. original.GenreStyles!, new("B", "blue", "white", "grid")] };
            var date = new DateOnly(2026, 9, 20);
            session.Save(edited, "test", date, "", false, connection.UpdatePortableGenreTable, connection.ParsePortable);
            var saved = connection.ParsePortable(File.ReadAllText(path));
            AssertEqual(2, saved.Materials.Single().GenreStyles!.Length);
            AssertEqual("メモ", saved.Description);
            AssertEqual(true, saved.Materials.Single().Credits!.ChangeLog is null);
            AssertEqual("test", session.CurrentTable.Credits!.Modifier!);
            AssertEqual(date, session.CurrentTable.Credits!.ModifiedOn!.Value);
            AssertEqual(true, session.CurrentTable.Credits!.ModifiedAt is not null);
            session.Save(edited, "test", date, "ジャンルを追加", false, connection.UpdatePortableGenreTable, connection.ParsePortable);
            AssertEqual("ジャンルを追加", connection.ParsePortable(File.ReadAllText(path)).Materials.Single().Credits!.ChangeLog!);
            session.Save(edited with { OverallComment = "再編集" }, "second", date, "", false, connection.UpdatePortableGenreTable, connection.ParsePortable);
            AssertEqual(true, session.CurrentTable.Credits!.ChangeLog is null);
            AssertEqual("second", session.CurrentTable.Credits!.Modifier!);
            session.Save(original, "test", date, "unused", true, connection.UpdatePortableGenreTable, connection.ParsePortable);
            AssertEqual(json, File.ReadAllText(path));
            AssertEqual(json, session.CurrentJson);
            File.WriteAllText(path, json + " ");
            var rejected = false;
            try { session.Save(edited, "test", date, "競合", false, connection.UpdatePortableGenreTable, connection.ParsePortable); }
            catch (IOException) { rejected = true; }
            AssertEqual(true, rejected);
            AssertEqual(json + " ", File.ReadAllText(path));
            AssertEqual(json, session.CurrentJson);
            AssertEqual(json, session.OpeningJson);

            using var workspace = connection.Open(ProjectJsonSerializer.Save(project));
            var openingCredits = workspace.Project.GenreStyleCredits;
            var draftCredits = new PersonCredits().WrittenBy("test", date) with { ModifiedAt = DateTimeOffset.Now };
            workspace.Execute(new SetGenreStyles(edited.GenreStyles!) { UpdateCredits = true, Credits = draftCredits }, selectedPlanEdit: false);
            AssertEqual(true, workspace.Project.GenreStyleCredits!.ChangeLog is null);
            AssertEqual(draftCredits.ModifiedAt, workspace.Project.GenreStyleCredits!.ModifiedAt);
            workspace.Execute(new SetGenreStyles(edited.GenreStyles!)
            { UpdateCredits = true, Credits = draftCredits.WrittenBy("test", date, "確定した変更コメント") }, selectedPlanEdit: false);
            AssertEqual("確定した変更コメント", workspace.Project.GenreStyleCredits!.ChangeLog!);
            // Finalizing or revising the package comment must not overwrite the project's comment.
            File.WriteAllText(path, json);
            session.Save(edited, "test", date, "パッケージ専用の変更コメント", false, connection.UpdatePortableGenreTable, connection.ParsePortable);
            AssertEqual("パッケージ専用の変更コメント", connection.ParsePortable(File.ReadAllText(path)).Materials.Single().Credits!.ChangeLog!);
            AssertEqual("確定した変更コメント", workspace.Project.GenreStyleCredits!.ChangeLog!);
            session.Save(edited, "test", date, "パッケージだけ追記", false, connection.UpdatePortableGenreTable, connection.ParsePortable);
            AssertEqual("パッケージだけ追記", connection.ParsePortable(File.ReadAllText(path)).Materials.Single().Credits!.ChangeLog!);
            AssertEqual("確定した変更コメント", workspace.Project.GenreStyleCredits!.ChangeLog!);
            workspace.Execute(new SetGenreStyles(project.GenreStyles) { UpdateCredits = true, Credits = openingCredits }, selectedPlanEdit: false);
            AssertEqual(true, project.GenreStyles.SequenceEqual(workspace.Project.GenreStyles));
            AssertEqual(openingCredits, workspace.Project.GenreStyleCredits);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
