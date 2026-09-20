namespace CircleSpaceCoordinator.Desktop.Tests;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Engine.Model;

internal static partial class Program
{
    private static void PackageReaderSelection()
    {
        var directory = Path.Combine(Path.GetTempPath(), "csc-package-reader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var json = EditorConnection.Current.ExportPortable(new(PortableExample(), [], new([], []), "テスト", "", [], false)
            { Materials = [new("genre-styles", "project")], Handle = "test" });
            File.WriteAllText(Path.Combine(directory, "a.package-csc.json"), json);
            File.WriteAllText(Path.Combine(directory, "b.package-csc.json"), "invalid");
            File.WriteAllText(Path.Combine(directory, "other.json"), json);
            File.WriteAllText(Path.Combine(directory, "legacy.project-portable.json"), json);
            Directory.CreateDirectory(Path.Combine(directory, "nested"));
            File.WriteAllText(Path.Combine(directory, "nested", "nested.package-csc.json"), json);
            var model = new PackageReadDialogModel(EditorConnection.Current.ParsePortable);
            model.SetDirectory(directory);
            AssertEqual(2, model.Files.Length);
            AssertEqual(false, model.CanRead);
            model.SelectFile(0);
            AssertEqual(1, model.Tables.Length);
            AssertEqual(EditorConnection.Current.ParsePortable(json).Materials.Single().Name, model.Tables[0].Name);
            model.SelectTable(0);
            AssertEqual(true, model.CanRead);
            model.SelectFile(1);
            AssertEqual(0, model.Tables.Length);
            AssertEqual(false, model.CanRead);
            AssertEqual(true, model.Error is not null);
            model.SelectFile(0);
            model.SelectTable(0);
            model.SetDirectory(Path.Combine(directory, "nested"));
            AssertEqual(-1, model.FileIndex);
            AssertEqual(false, model.CanRead);
            model.SetDirectory(Path.Combine(directory, "missing"));
            AssertEqual(0, model.Files.Length);
            AssertEqual(true, model.Error is not null);
            AssertEqual(json, File.ReadAllText(Path.Combine(directory, "a.package-csc.json")));

            model.SelectPath(Path.Combine(directory, "a.package-csc.json"));
            AssertEqual(2, model.Files.Length);
            AssertEqual(true, model.CanRead);
            model.SelectPath(Path.Combine(directory, "legacy.project-portable.json"));
            AssertEqual(3, model.Files.Length);
            AssertEqual("legacy.project-portable.json", Path.GetFileName(model.Files[model.FileIndex]));
            AssertEqual(true, model.CanRead);
            model.SelectPath(Path.Combine(directory, "b.package-csc.json"));
            AssertEqual(false, model.CanRead);
            AssertEqual(true, model.Error is not null);

            var mixed = new PackageReadDialogModel(_ => new PortablePackage("mixed", "", [], false, [])
            {
                Materials = [new("z", "genre-styles", "表Z", false), new("block", "block-styles", "ブロック", false),
                    new("a", "genre-styles", "表A", false)],
            });
            mixed.SetDirectory(directory);
            mixed.SelectFile(0);
            AssertEqual("表A,表Z", string.Join(',', mixed.Tables.Select(table => table.Name)));
            mixed.SelectTable(2);
            AssertEqual(false, mixed.CanRead);
            mixed.SelectPath(Path.Combine(directory, "other.json"), "z");
            AssertEqual("other.json", Path.GetFileName(mixed.Files[mixed.FileIndex]));
            AssertEqual("z", mixed.Tables[mixed.TableIndex].Id);
            mixed.SelectPath(Path.Combine(directory, "other.json"));
            AssertEqual(false, mixed.CanRead);
            mixed.SelectPath(Path.Combine(directory, "other.json"), "missing");
            AssertEqual(false, mixed.CanRead);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
