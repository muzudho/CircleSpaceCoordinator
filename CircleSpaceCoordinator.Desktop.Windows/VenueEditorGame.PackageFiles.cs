namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.EditorClient;

public sealed partial class VenueEditorGame
{
    private void RenamePackageFromReader()
    {
        if (packageReadDialog is not { CanOperate: true }) return;
        if (packageReadDialog.OperationTarget == PackageReadTarget.Table) { RenameTableFromReader(); return; }
        if (packageReadDialog is not { SelectedPackage: not null, SelectedJson: { } json, FileIndex: >= 0 } reader) return;
        var path = Path.GetFullPath(reader.Files[reader.FileIndex]);
        var tableId = reader.CanRead ? reader.Tables[reader.TableIndex].Id : null;
        const string extension = ".package-csc.json";
        var fileName = Path.GetFileName(path);
        var stem = fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? fileName[..^extension.Length] : Path.GetFileNameWithoutExtension(fileName);
        OpenUnderlineInput("パッケージファイルのリネーム", stem, name =>
        {
            string renamed;
            if (packageGenreSaveSession is { } session && string.Equals(session.Path, path, StringComparison.OrdinalIgnoreCase))
            {
                session.RenameFile(json, name);
                renamed = session.Path;
            }
            else renamed = PackageFileOperations.Rename(path, json, name);
            OpenPackageReadDialog(renamed, tableId);
            packageReadDialog?.TargetFile();
            packageReadNotice = "ファイル名を変更しました。";
        }, $"現在のファイル：{fileName}\n新しいファイル名を入力してください。拡張子 .package-csc.json は自動で付きます。\nパッケージ内の名前と内容は変更しません。同名ファイルには上書きしません。",
            maxLength: 117, cancelled: () => { OpenPackageReadDialog(path, tableId); packageReadDialog?.TargetFile(); }, acceptLabel: "リネーム");
    }

    private void CreatePackageFromReader()
    {
        if (packageReadDialog is not { } reader) return;
        if (!Directory.Exists(reader.DirectoryPath)) return;
        var directory = reader.DirectoryPath;
        var previousPath = reader.FileIndex >= 0 ? reader.Files[reader.FileIndex] : null;
        OpenUnderlineInput("パッケージの新規作成", "新しいパッケージ", name =>
        {
            var path = PackageFileOperations.CreateEmpty(directory, name, EditorConnection.Current.CreateEmptyPortable);
            OpenPackageReadDialog(path, null);
            packageReadNotice = "空のパッケージを作成しました。［網掛け表を新規作成］で表を追加できます。";
        }, "空のパッケージを作成します。\nパッケージ名を入力してください。同名ファイルは上書きせず、連番を付けます。",
            validate: PackageFileOperations.ValidateName, cancelled: () => OpenPackageReadDialog(previousPath, null), acceptLabel: "作成");
    }

    private void DeletePackageFromReader()
    {
        if (packageReadDialog is not { CanOperate: true }) return;
        if (packageReadDialog.OperationTarget == PackageReadTarget.Table) { DeleteTableFromReader(); return; }
        if (packageReadDialog is not { SelectedPackage: { } package, SelectedJson: { } json, FileIndex: >= 0 } reader) return;
        var path = Path.GetFullPath(reader.Files[reader.FileIndex]);
        var name = package.Name;
        OpenUnderlineInput("パッケージの削除", "", entered =>
        {
            PackageFileOperations.Delete(path, json, name, entered);
            // The open right pane must not try to autosave or restore a deleted package.
            if (packageGenreSaveSession is { } session && string.Equals(session.Path, path, StringComparison.OrdinalIgnoreCase))
            {
                packageGenreSaveSession = null;
                packageGenreTable = null;
                packageChangeTag = null;
                selectedPackageGenreKey = null;
                genrePackageRequiresComment = genrePackageOverwriteDeferred = false;
                genrePackageSavedContent = genrePackageSavedStamp = "";
                packageGenreScroll = 0;
                mappingWidth = -1;
            }
            OpenPackageReadDialog();
            packageReadNotice = "パッケージを削除しました。";
        }, $"削除するパッケージ名：{name}\nファイル：{Path.GetFileName(path)}\n保存場所：{Path.GetDirectoryName(path)}\nパッケージ内の全データを削除します。元に戻せません。\n確認のため、上のパッケージ名を正確に入力して［削除］を押してください。",
            maxLength: Math.Max(1000, name.Length),
            validate: value => PackageFileOperations.NameMatches(name, value) ? null : "パッケージ名が一致しません。",
            cancelled: () => OpenPackageReadDialog(path, null), trim: false, acceptLabel: "削除", requireValidInput: true);
    }

    private void RenameTableFromReader()
    {
        if (packageReadDialog is not { CanRead: true, SelectedJson: { } json, SelectedPackage: { } package } reader || !EnsureHandle()) return;
        var path = reader.Files[reader.FileIndex];
        var table = reader.Tables[reader.TableIndex];
        string? Validate(string value)
        {
            try
            {
                var name = GenreStyleDefinition.NormalizeTableName(value);
                return package.Materials.Any(item => item.Id != table.Id && item.Kind == table.Kind && item.Name == name)
                    ? "同じ名前の網掛け表が既にあります。" : null;
            }
            catch (ArgumentException ex) { return ex.Message; }
        }
        OpenUnderlineInput("網掛け表のリネーム", table.Name, value =>
        {
            var name = GenreStyleDefinition.NormalizeTableName(value);
            if (name != table.Name)
            {
                var renamed = table with { Name = name, TableMetadata = table.TableMetadata is { } metadata ? metadata with { Name = name } : null };
                var updated = EditorConnection.Current.UpdatePortableGenreTable(new(json, renamed, Handle, WorkDate, "網掛け表の名前を変更"));
                PortableLibraryService.SaveMetadata(new(path, json, package, null), updated);
                RefreshReaderPackageSession(path, updated);
            }
            OpenPackageReadDialog(path, table.Id);
            packageReadNotice = "網掛け表の名前を変更しました。";
        }, $"操作対象の網掛け表：{table.Name}\nパッケージファイル：{Path.GetFileName(path)}",
            maxLength: 1000, validate: Validate, cancelled: () => OpenPackageReadDialog(path, table.Id), acceptLabel: "リネーム", requireValidInput: true);
    }

    private void DeleteTableFromReader()
    {
        if (packageReadDialog is not { CanRead: true, SelectedJson: { } json } reader) return;
        var path = reader.Files[reader.FileIndex];
        var table = reader.Tables[reader.TableIndex];
        OpenUnderlineInput("網掛け表の削除", "", entered =>
        {
            var updated = PackageFileOperations.DeleteTable(path, json, table.Id, entered, EditorConnection.Current.ParsePortable);
            RefreshReaderPackageSession(path, updated);
            OpenPackageReadDialog(path, table.Id);
            packageReadNotice = "網掛け表を削除しました。";
        }, $"削除する網掛け表名：{table.Name}\nパッケージファイル：{Path.GetFileName(path)}\nこの表を削除します。元に戻せません。\n確認のため、上の網掛け表名を正確に入力して［削除］を押してください。",
            maxLength: Math.Max(1000, table.Name.Length), trim: false,
            validate: value => PackageFileOperations.NameMatches(table.Name, value) ? null : "網掛け表名が一致しません。",
            cancelled: () => OpenPackageReadDialog(path, table.Id), acceptLabel: "削除", requireValidInput: true);
    }

    private void RefreshReaderPackageSession(string path, string json)
    {
        if (packageGenreSaveSession is not { } session || !string.Equals(Path.GetFullPath(session.Path), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)) return;
        var package = EditorConnection.Current.ParsePortable(json);
        var current = package.Materials.SingleOrDefault(item => item.Id == packageGenreTable?.Id);
        if (current is not null)
        {
            packageGenreTable = current;
            AttachPackageGenreSaveSession(path, json, package, current);
        }
        else
        {
            packageGenreSaveSession = null;
            packageGenreTable = null;
            packageChangeTag = null;
            selectedPackageGenreKey = null;
            shadingSelection.Clear();
            genrePackageRequiresComment = genrePackageOverwriteDeferred = false;
            genrePackageSavedContent = genrePackageSavedStamp = "";
            packageGenreScroll = 0;
        }
        mappingWidth = -1;
    }
}
