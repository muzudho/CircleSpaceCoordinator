namespace CircleSpaceCoordinator.Desktop.Tests;

using System.Text.Json;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.EditorClient;

internal static partial class Program
{
    private static void EmptyShadingTableCreation()
    {
        var connection = EditorConnection.Current;
        var date = new DateOnly(2026, 9, 20);
        var empty = connection.CreateEmptyPortable("新規パッケージ");
        var genreJson = connection.CreatePortableShadingTable(new(empty, "genre-styles", "ジャンル表", "分類の補足", "test", date));
        var genre = connection.ParsePortable(genreJson);
        var table = genre.Materials.Single();
        AssertEqual("ジャンル表", table.Name);
        AssertEqual("分類の補足", table.OverallComment!);
        AssertEqual(0, table.GenreStyles!.Length);
        AssertEqual(false, PackageGenreSaveSession.HasMissingComment(table));
        var blockJson = connection.CreatePortableShadingTable(new(genreJson, "block-styles", "ブロック表", "", "test", date));
        var blocks = connection.ParsePortable(blockJson);
        AssertEqual(2, blocks.Materials.Count);
        AssertEqual(JsonSerializer.Serialize(table), JsonSerializer.Serialize(blocks.Materials.Single(item => item.Kind == "genre-styles")));
        var block = blocks.Materials.Single(item => item.Kind == "block-styles");
        AssertEqual(0, block.BlockStyles!.Length);
        AssertEqual("ブロック表", block.Metadata().Name!);
        AssertEqual(true, block.Metadata().OverallComment is null);
        RejectPortable(() => connection.CreatePortableShadingTable(new(blockJson, "genre-styles", "ジャンル表", "", "test", date)));
        RejectPortable(() => connection.CreatePortableShadingTable(new(empty, "venue", "無効", "", "test", date)));
        RejectPortable(() => connection.CreatePortableShadingTable(new(empty, "genre-styles", "", "", "test", date)));

        var directory = Path.Combine(Path.GetTempPath(), "csc-new-table-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "new.package-csc.json");
            File.WriteAllText(path, empty);
            var opening = connection.ParsePortable(empty);
            PortableLibraryService.SaveMetadata(new(path, empty, opening, null), genreJson);
            var reader = new PackageReadDialogModel(connection.ParsePortable);
            reader.SelectPath(path, table.Id);
            AssertEqual(true, reader.CanRead);
            AssertEqual(table.Id, reader.Tables[reader.TableIndex].Id);
            var session = new PackageGenreSaveSession(path, genreJson, genre, table);
            session.Save(table.WithRows([new("追加行", "red", "white", "solid")]), "test", date, "", false,
                connection.UpdatePortableGenreTable, connection.ParsePortable);
            AssertEqual(1, connection.ParsePortable(File.ReadAllText(path)).Materials.Single().Rows().Length);
            var current = File.ReadAllText(path);
            RejectPortable(() => PortableLibraryService.SaveMetadata(new(path, empty, opening, null), blockJson));
            AssertEqual(current, File.ReadAllText(path));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
