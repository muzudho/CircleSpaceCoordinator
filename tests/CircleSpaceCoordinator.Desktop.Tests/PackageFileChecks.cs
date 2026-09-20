namespace CircleSpaceCoordinator.Desktop.Tests;

using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.EditorClient;

internal static partial class Program
{
    private static void PackageCreationAndConfirmedDeletion()
    {
        var directory = Path.Combine(Path.GetTempPath(), "csc-package-files-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var connection = EditorConnection.Current;
            var path = PackageFileOperations.CreateEmpty(directory, "空のパッケージ", connection.CreateEmptyPortable);
            var json = File.ReadAllText(path);
            var package = connection.ParsePortable(json);
            AssertEqual("空のパッケージ", package.Name);
            AssertEqual(0, package.Items.Count + package.Knowledge.Count + package.Materials.Count);
            var reader = new PackageReadDialogModel(connection.ParsePortable);
            reader.SelectPath(path);
            AssertEqual(true, reader.SelectedPackage is not null);
            AssertEqual(false, reader.CanRead);
            var second = PackageFileOperations.CreateEmpty(directory, package.Name, connection.CreateEmptyPortable);
            AssertEqual("空のパッケージ_2.package-csc.json", Path.GetFileName(second));
            AssertEqual(json, File.ReadAllText(path));
            var originalPath = path;
            path = PackageFileOperations.Rename(path, json, "renamed.package-csc.json");
            AssertEqual(false, File.Exists(originalPath));
            AssertEqual(json, File.ReadAllText(path));
            AssertEqual(package.Name, connection.ParsePortable(File.ReadAllText(path)).Name);
            path = PackageFileOperations.Rename(path, json, "RENAMED");
            AssertEqual("RENAMED.package-csc.json", Path.GetFileName(path));
            AssertEqual(json, File.ReadAllText(path));
            var renameRejected = false;
            try { PackageFileOperations.Rename(path, json, Path.GetFileName(second)); }
            catch (IOException) { renameRejected = true; }
            AssertEqual(true, renameRejected);
            AssertEqual(json, File.ReadAllText(path));
            AssertEqual(json, File.ReadAllText(second));
            AssertEqual(false, PackageFileOperations.NameMatches(package.Name, ""));
            AssertEqual(false, PackageFileOperations.NameMatches(package.Name, package.Name + " "));
            AssertEqual(false, PackageFileOperations.NameMatches("ABC", "abc"));
            AssertEqual(true, PackageFileOperations.NameMatches(package.Name, package.Name));
            var rejected = false;
            try { PackageFileOperations.Delete(path, json, package.Name, "違うパッケージ"); }
            catch (InvalidOperationException) { rejected = true; }
            AssertEqual(true, rejected);
            AssertEqual(json, File.ReadAllText(path));
            File.WriteAllText(path, json + " ");
            renameRejected = false;
            try { PackageFileOperations.Rename(path, json, "stale"); }
            catch (IOException) { renameRejected = true; }
            AssertEqual(true, renameRejected);
            rejected = false;
            try { PackageFileOperations.Delete(path, json, package.Name, package.Name); }
            catch (IOException) { rejected = true; }
            AssertEqual(true, rejected);
            AssertEqual(json + " ", File.ReadAllText(path));
            PackageFileOperations.Delete(path, json + " ", package.Name, package.Name);
            AssertEqual(false, File.Exists(path));
            AssertEqual(true, File.Exists(second));
            reader.SetDirectory(directory);
            AssertEqual(-1, reader.FileIndex);
            AssertEqual(true, reader.SelectedPackage is null);
            AssertEqual(1, reader.Files.Length);
            AssertEqual(true, PackageFileOperations.ValidateName("../escape") is not null);

            var tableJson = connection.ExportPortable(new(PortableExample(), [], new([], []), "保存中", "", [], false)
                { Materials = [new("genre-styles", "project")], Handle = "test" });
            var tablePackage = connection.ParsePortable(tableJson);
            var tablePath = Path.Combine(directory, "editing.package-csc.json");
            File.WriteAllText(tablePath, tableJson);
            var session = new PackageGenreSaveSession(tablePath, tableJson, tablePackage, tablePackage.Materials.Single());
            session.RenameFile(tableJson, "editing-renamed");
            AssertEqual(false, File.Exists(tablePath));
            AssertEqual(tableJson, session.OpeningJson);
            session.Save(session.OpeningTable, "test", new DateOnly(2026, 9, 20), "", true,
                connection.UpdatePortableGenreTable, connection.ParsePortable);
            AssertEqual(false, File.Exists(tablePath));
            AssertEqual(tableJson, File.ReadAllText(session.Path));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
