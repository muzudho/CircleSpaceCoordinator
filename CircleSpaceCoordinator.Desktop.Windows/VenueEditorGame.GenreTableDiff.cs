namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Engine.Model;
using Forms = System.Windows.Forms;

public sealed partial class VenueEditorGame
{
    private void ShowGenreTableDiff(PortablePackage package, string materialId, string json, string? sourcePath, Forms.Form? parent = null)
    {
        if (workspace is not { } owner) return;
        var draft = new GenreTableDiffDraft(owner.Project, package.Materials.Single(item => item.Id == materialId), package.IsConfidential);
        var revision = owner.Revision;
        GenreTableDiffForm? form = null;
        void ApplyLeft(PortableMaterial table, string log)
        {
            if (owner.Revision != revision) throw new InvalidOperationException("イベントが変更されました。比較画面を開き直してください。");
            owner.Execute(new SetGenreStyles(table.GenreStyles!)
            {
                GenreCodeTableName = table.Name, GenreCodeOrder = table.GenreCodeOrder ?? [], UpdateGenreCodeOrder = true,
                GenreCodeOrderComment = table.GenreCodeOrderComment, MarkConfidential = table.IsConfidential,
                ActorHandle = Handle, WorkDate = WorkDate, ChangeLog = log,
            }, selectedPlanEdit: false);
            revision = owner.Revision;
            if (projectSavePath is not null && !SaveProject().Success)
            {
                owner.Undo(); revision = owner.Revision;
                throw new IOException("イベントを保存できなかったため反映を戻しました。比較中の入力は残しています。\n" + autoSaveError);
            }
            draft.MarkSaved(GenreTableSide.Left, owner.Project.IsConfidential);
        }
        bool SaveRight(PortableMaterial table, string log, bool saveAs)
        {
            var output = sourcePath;
            if (saveAs || output is null)
            {
                using var dialog = new Forms.SaveFileDialog { Title = "整理したパッケージの保存先", DefaultExt = "package-csc.json",
                    Filter = "パッケージ (*.package-csc.json)|*.package-csc.json", OverwritePrompt = true,
                    FileName = sourcePath is null ? "genre-codes.package-csc.json" : Path.GetFileNameWithoutExtension(sourcePath) + "-edited.package-csc.json",
                    InitialDirectory = sourcePath is null ? "" : Path.GetDirectoryName(sourcePath) };
                if (dialog.ShowDialog(form) != Forms.DialogResult.OK) return false;
                output = dialog.FileName;
            }
            if (projectSavePath is not null && string.Equals(Path.GetFullPath(output), Path.GetFullPath(projectSavePath), StringComparison.OrdinalIgnoreCase))
                throw new IOException("イベントファイルとは別の保存先を指定してください。");
            var updated = EditorConnection.Current.UpdatePortableGenreTable(new(json, table, Handle, WorkDate, log));
            var updatedPackage = EditorConnection.Current.ParsePortable(updated);
            if (sourcePath is not null && string.Equals(Path.GetFullPath(output), Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase))
                PortableLibraryService.SaveMetadata(new(sourcePath, json, package, null), updated);
            else FrameLayoutPortableService.SaveDocument(output, updated, overwrite: true);
            json = updated; sourcePath = output; package = updatedPackage;
            draft.MarkSaved(GenreTableSide.Right, package.IsConfidential);
            return true;
        }
        using (form = new GenreTableDiffForm(draft, owner.Project.Name, Handle, ApplyLeft, SaveRight, sourcePath is not null))
        {
            if (parent is null) form.ShowDialog(); else form.ShowDialog(parent);
        }
        CancelInProgressPointerInteraction();
        CreateToolbar();
    }
}
