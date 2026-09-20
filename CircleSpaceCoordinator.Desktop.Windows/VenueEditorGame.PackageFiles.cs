namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.EditorClient;

public sealed partial class VenueEditorGame
{
    private void CreatePackageFromReader()
    {
        if (packageReadDialog is not { } reader) return;
        if (!Directory.Exists(reader.DirectoryPath)) ChoosePackageReadDirectory();
        if (!Directory.Exists(reader.DirectoryPath)) return;
        var directory = reader.DirectoryPath;
        var previousPath = reader.FileIndex >= 0 ? reader.Files[reader.FileIndex] : null;
        OpenUnderlineInput("パッケージの新規作成", "新しいパッケージ", name =>
        {
            var path = PackageFileOperations.CreateEmpty(directory, name, EditorConnection.Current.CreateEmptyPortable);
            OpenPackageReadDialog(path, null);
            packageReadNotice = "空のパッケージを作成しました。";
        }, "空のパッケージを作成します。\nパッケージ名を入力してください。同名ファイルは上書きせず、連番を付けます。",
            validate: PackageFileOperations.ValidateName, cancelled: () => OpenPackageReadDialog(previousPath, null), acceptLabel: "作成");
    }

    private void DeletePackageFromReader()
    {
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
}
