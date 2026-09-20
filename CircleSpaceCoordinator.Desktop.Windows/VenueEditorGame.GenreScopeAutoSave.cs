namespace CircleSpaceCoordinator.Desktop.Windows;

using System.Text.Json;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.EditorClient;
using Microsoft.Xna.Framework;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private const double GenreSaveDelay = 1.5;
    private const string PendingGenreLog = "";
    private string genreProjectSavedContent = "";
    private string genrePackageSavedContent = "";
    private bool genreProjectRequiresComment;
    private bool genrePackageRequiresComment;
    private PackageGenreSaveSession? packageGenreSaveSession;
    private string genreProjectSavedStamp = "";
    private string genrePackageSavedStamp = "";
    private string genreObservedStamp = "";
    private double genreSaveChangedAt;
    private string? genreSaveError;
    private bool genreCloseAfterDiscard;
    private bool genreButtonsProjectChanged;
    private bool genreButtonsPackageChanged;
    private bool genrePackageOverwriteDeferred;

    private bool GenreProjectChanged => mappingDraft?.HasChanges == true ||
        mappingGenreCodeTableName != mappingAppliedGenreCodeTableName ||
        !mappingGenreCodeOrder.SequenceEqual(mappingAppliedGenreOrder) || mappingGenreCodeOrderComment != mappingAppliedGenreOrderComment;
    private bool GenrePackageChanged => packageGenreTable is not null && packageGenreSaveSession is { } session &&
        JsonSerializer.Serialize(packageGenreTable) != JsonSerializer.Serialize(session.OpeningTable);
    private bool GenreScopeChanged => GenreProjectChanged || GenrePackageChanged;
    private string GenreProjectSaveLog => mappingComposition.Length == 0 && mappingChangeTag is { ValidationError: null } tag ? tag.Text : PendingGenreLog;
    private string GenrePackageSaveLog => packageChangeTag is { ValidationError: null } tag ? tag.Text : PendingGenreLog;
    private string GenreProjectContent => JsonSerializer.Serialize(new
    {
        Styles = mappingDraft?.Build().OrderBy(row => row.Key, StringComparer.Ordinal),
        Comment = mappingDraft?.OverallComment, Name = mappingGenreCodeTableName,
        Order = mappingGenreCodeOrder, OrderComment = mappingGenreCodeOrderComment,
    });
    private string GenrePackageContent => packageGenreTable is null ? "" : JsonSerializer.Serialize(packageGenreTable);
    private string GenreProjectStamp => JsonSerializer.Serialize(new { Content = GenreProjectContent, Log = GenreProjectSaveLog });
    private string GenrePackageStamp => packageGenreTable is null ? "" : JsonSerializer.Serialize(new { Content = GenrePackageContent, Log = GenrePackageSaveLog });
    private static bool HasPendingGenreComment(PersonCredits? credits) => credits is { Modifier: not null, ChangeLog: null };
    private ChangeTagEditor CreateGenreTag(PersonCredits? credits)
    {
        var tag = new ChangeTagEditor(ValidateMappingChangeLog);
        if (credits?.ChangeLog is { } log) tag.Insert(log);
        return tag;
    }
    private static void ClearGenreTag(ChangeTagEditor? tag)
    {
        if (tag is null) return;
        tag.Editor.SelectAll();
        tag.Editor.Delete(false);
        tag.Edited();
    }
    private bool GenreSavePending => genreProjectSavedStamp != GenreProjectStamp || genrePackageSavedStamp != GenrePackageStamp;

    private void InitializeGenreScopeAutoSave()
    {
        packageGenreSaveSession = null;
        mappingChangeTag = CreateGenreTag(mappingPreviousCredits);
        genreProjectRequiresComment = HasPendingGenreComment(mappingPreviousCredits);
        genrePackageRequiresComment = false;
        packageChangeTag = new ChangeTagEditor(ValidateMappingChangeLog);
        genreSaveError = null;
        genreCloseAfterDiscard = false;
        genreProjectSavedStamp = GenreProjectStamp;
        genreProjectSavedContent = GenreProjectContent;
        genrePackageSavedContent = "";
        genrePackageSavedStamp = "";
        genreObservedStamp = genreProjectSavedStamp + genrePackageSavedStamp;
        genreSaveChangedAt = statusHintTime;
    }

    private void AttachPackageGenreSaveSession(string path, string json, CircleSpaceCoordinator.Engine.Model.PortablePackage package, PortableMaterial table)
    {
        packageGenreSaveSession = new(path, json, package, table);
        genrePackageOverwriteDeferred = false;
        packageChangeTag = CreateGenreTag(table.Credits);
        genrePackageRequiresComment = HasPendingGenreComment(table.Credits);
        genreCloseAfterDiscard = false;
        genrePackageSavedStamp = GenrePackageStamp;
        genrePackageSavedContent = GenrePackageContent;
        genreSaveError = null;
    }

    private void UpdateGenreScopeAutoSave()
    {
        if (!mappingKnowledgeComments || mappingDraft is null) return;
        SyncMappingChangeTag();
        var observed = GenreProjectStamp + GenrePackageStamp;
        if (observed != genreObservedStamp)
        {
            genreObservedStamp = observed;
            genreSaveChangedAt = statusHintTime;
            genreSaveError = null;
        }
        if (GenreSavePending && statusHintTime - genreSaveChangedAt >= GenreSaveDelay &&
            modalDialog is null && mappingComposition.Length == 0)
        {
            SaveGenreScope(automatic: true);
            genreSaveChangedAt = statusHintTime;
        }
        if (genreCloseAfterDiscard && modalDialog is null && !GenreScopeChanged && MappingCommentsCanClose && !GenreSavePending && genreSaveError is null)
            CloseStyleMappingEditor();
    }

    private bool SaveGenreScope(Action? afterSave = null, bool automatic = false)
    {
        if (mappingDraft is null || applyStyleMapping is null) return true;
        if (string.IsNullOrWhiteSpace(Handle) && (GenreScopeChanged || genreProjectRequiresComment || genrePackageRequiresComment))
        {
            genreSaveError = "作業者を設定すると自動保存できます。";
            return false;
        }
        var success = true;
        genreSaveError = null;
        var projectStamp = GenreProjectStamp;
        if (projectStamp != genreProjectSavedStamp)
        {
            try
            {
                var contentChanged = genreProjectSavedContent != GenreProjectContent;
                applyStyleMapping(mappingDraft.Build(), contentChanged ? "" : GenreProjectSaveLog, Handle, WorkDate);
                if (!FlushAutoSave()) throw new IOException(autoSaveError ?? "プロジェクトを保存できませんでした。");
                if (contentChanged && (GenreProjectChanged || genreProjectRequiresComment))
                {
                    ClearGenreTag(mappingChangeTag);
                    genreProjectRequiresComment = true;
                }
                genreProjectSavedContent = GenreProjectContent;
                genreProjectSavedStamp = GenreProjectStamp;
                mappingChangeTag?.SaveSucceeded();
            }
            catch (Exception ex)
            {
                success = false;
                genreSaveError = "プロジェクト：" + ex.Message;
                mappingChangeTag?.SaveFailed(genreSaveError);
            }
        }
        var packageStamp = GenrePackageStamp;
        if (packageStamp != genrePackageSavedStamp && packageGenreTable is { } table && packageGenreSaveSession is { } session)
        {
            if (automatic && genrePackageOverwriteDeferred) return false;
            try
            {
                if (projectSavePath is not null && string.Equals(Path.GetFullPath(projectSavePath), session.Path, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("イベントとパッケージに同じ保存先は使用できません。");
                var contentChanged = genrePackageSavedContent != GenrePackageContent;
                session.Save(table, Handle, WorkDate, contentChanged ? "" : GenrePackageSaveLog, !GenrePackageChanged && !genrePackageRequiresComment,
                    EditorConnection.Current.UpdatePortableGenreTable, EditorConnection.Current.ParsePortable);
                if (contentChanged && (GenrePackageChanged || genrePackageRequiresComment))
                {
                    ClearGenreTag(packageChangeTag);
                    genrePackageRequiresComment = true;
                }
                genrePackageSavedContent = GenrePackageContent;
                genrePackageSavedStamp = GenrePackageStamp;
                packageChangeTag?.SaveSucceeded();
                genrePackageOverwriteDeferred = false;
            }
            catch (PackageCommentConflictException ex)
            {
                genrePackageOverwriteDeferred = true;
                genreSaveError = "パッケージ：上書きの確認が必要です。";
                OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "変更コメントの上書き確認", ""), action =>
                {
                    if (action != ModalDialogAction.Accept) return;
                    session.ApprovedOverwriteJson = ex.Json;
                    if (SaveGenreScope(afterSave)) afterSave?.Invoke();
                }, [("キャンセル", ModalDialogAction.Cancel), ("上書きする", ModalDialogAction.Accept)]);
                SetViewerText("変更コメントが既に入力されています。\n作業中に他の誰かがデータを変更したのかもしれません。\n上書きしますか？\n\n保存先の変更タグ\n" + ex.Credits.AttributionText + "\n変更コメント：" + ex.Credits.ChangeLog);
                return false;
            }
            catch (Exception ex)
            {
                success = false;
                packageChangeTag?.SaveFailed("パッケージ：" + ex.Message);
                genreSaveError = (genreSaveError is null ? "" : genreSaveError + " / ") + "パッケージ：" + ex.Message;
            }
        }
        return success;
    }

    private bool FinishGenreScope()
    {
        if (mappingComposition.Length > 0) return false;
        if (!SaveGenreScope(() => FinishGenreScope())) return false;
        SyncMappingChangeTag();
        if (!MappingCommentsCanClose)
        {
            if (mappingChangeTag?.CanClose != true && modalDialog is null) OpenProjectChangeComment();
            else if (modalDialog is null) OpenPackageChangeComment();
            return false;
        }
        if (!SaveGenreScope()) return false;
        CloseStyleMappingEditor();
        return true;
    }

    private void RestoreGenreProject()
    {
        mappingDraft?.RestoreOpeningSnapshot();
        mappingGenreCodeTableName = mappingAppliedGenreCodeTableName;
        mappingGenreCodeOrder = mappingAppliedGenreOrder.ToArray();
        mappingGenreCodeOrderComment = mappingAppliedGenreOrderComment;
        genreCodeOrder = mappingGenreCodeOrder.ToArray();
        mappingOrderChanged = false;
        mappingScroll = mappingRow = 0;
        selectedGenreKey = null;
    }

    private void DiscardGenreSide(bool project)
    {
        SetMappingTextFocus(false);
        if (project)
        {
            RestoreGenreProject();
            genreProjectRequiresComment = false;
            mappingChangeTag = CreateGenreTag(mappingPreviousCredits);
        }
        else if (packageGenreSaveSession is { } session)
        {
            packageGenreTable = session.OpeningTable;
            packageGenreScroll = 0;
            selectedPackageGenreKey = null;
            genrePackageRequiresComment = false;
            packageChangeTag = CreateGenreTag(session.OpeningTable.Credits);
        }
        mappingWidth = -1;
        mappingFocus = -1;
        SyncMappingChangeTag();
        genreCloseAfterDiscard = project && !GenreScopeChanged;
    }

    private void DrawGenreScopeAutoSave(int width, int top)
    {
        var pending = GenreSavePending;
        var progress = pending ? Math.Clamp((statusHintTime - genreSaveChangedAt) / GenreSaveDelay, 0, 1) : 1;
        var bounds = new ScreenRectangle(Math.Max(0, width - 260), top + 30, 246, 5);
        DrawRectangle(bounds, new Color(48, 65, 77));
        DrawRectangle(new(bounds.X, bounds.Y, bounds.Width * progress, bounds.Height), genreSaveError is null ? new Color(99, 223, 185) : Color.OrangeRed);
        var label = genreSaveError is not null ? "自動保存失敗（再試行中）" : pending ? "自動保存まで…" : "プロジェクト・パッケージ保存済み";
        textRenderer?.Draw(label, new Rectangle((int)bounds.X, top + 5, 246, 23), genreSaveError is null ? Color.LightGreen : Color.OrangeRed, 14, true);
    }
}
