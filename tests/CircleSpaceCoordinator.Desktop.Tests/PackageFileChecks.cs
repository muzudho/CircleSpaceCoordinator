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
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
