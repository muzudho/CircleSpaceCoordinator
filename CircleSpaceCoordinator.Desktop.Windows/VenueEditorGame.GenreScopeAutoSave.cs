namespace CircleSpaceCoordinator.Desktop.Windows;

using System.Text.Json;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.EditorClient;
using Microsoft.Xna.Framework;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private const double GenreSaveDelay = 1.5;
    private const string PendingGenreLog = "編集中（変更コメント未確定）";
    private PackageGenreSaveSession? packageGenreSaveSession;
    private string genreProjectSavedStamp = "";
    private string genrePackageSavedStamp = "";
    private string genreObservedStamp = "";
    private double genreSaveChangedAt;
    private string? genreSaveError;
    private bool genreCloseAfterDiscard;
    private bool genreButtonsProjectChanged;
    private bool genreButtonsPackageChanged;

    private bool GenreProjectChanged => mappingDraft?.HasChanges == true ||
        mappingGenreCodeTableName != mappingAppliedGenreCodeTableName ||
        !mappingGenreCodeOrder.SequenceEqual(mappingAppliedGenreOrder) || mappingGenreCodeOrderComment != mappingAppliedGenreOrderComment;
    private bool GenrePackageChanged => packageGenreTable is not null && packageGenreSaveSession is { } session &&
        JsonSerializer.Serialize(packageGenreTable) != JsonSerializer.Serialize(session.OpeningTable);
    private bool GenreScopeChanged => GenreProjectChanged || GenrePackageChanged;
    private string GenreSaveLog => mappingComposition.Length == 0 && mappingChangeTag is { ValidationError: null } tag ? tag.Text : PendingGenreLog;
    private string GenreProjectStamp => JsonSerializer.Serialize(new
    {
        Styles = mappingDraft?.Build().OrderBy(row => row.Key, StringComparer.Ordinal),
        Comment = mappingDraft?.OverallComment, Name = mappingGenreCodeTableName,
        Order = mappingGenreCodeOrder, OrderComment = mappingGenreCodeOrderComment,
        Log = GenreProjectChanged ? GenreSaveLog : "",
    });
    private string GenrePackageStamp => packageGenreTable is null ? "" : JsonSerializer.Serialize(new
    {
        Table = packageGenreTable, Log = GenrePackageChanged ? GenreSaveLog : "",
    });
    private bool GenreSavePending => genreProjectSavedStamp != GenreProjectStamp || genrePackageSavedStamp != GenrePackageStamp;

    private void InitializeGenreScopeAutoSave()
    {
        packageGenreSaveSession = null;
        genreSaveError = null;
        genreCloseAfterDiscard = false;
        genreProjectSavedStamp = GenreProjectStamp;
        genrePackageSavedStamp = "";
        genreObservedStamp = genreProjectSavedStamp + genrePackageSavedStamp;
        genreSaveChangedAt = statusHintTime;
    }

    private void AttachPackageGenreSaveSession(string path, string json, CircleSpaceCoordinator.Engine.Model.PortablePackage package, PortableMaterial table)
    {
        packageGenreSaveSession = new(path, json, package, table);
        genreCloseAfterDiscard = false;
        genrePackageSavedStamp = GenrePackageStamp;
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
            SaveGenreScope();
            genreSaveChangedAt = statusHintTime;
        }
        if (genreCloseAfterDiscard && modalDialog is null && !GenreScopeChanged && !GenreSavePending && genreSaveError is null)
            CloseStyleMappingEditor();
    }

    private bool SaveGenreScope()
    {
        if (mappingDraft is null || applyStyleMapping is null) return true;
        if (string.IsNullOrWhiteSpace(Handle) && GenreScopeChanged)
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
                applyStyleMapping(mappingDraft.Build(), GenreProjectChanged ? GenreSaveLog : "変更を破棄（編集開始時の内容に復元）", Handle, WorkDate);
                if (!FlushAutoSave()) throw new IOException(autoSaveError ?? "プロジェクトを保存できませんでした。");
                genreProjectSavedStamp = projectStamp;
            }
            catch (Exception ex) { success = false; genreSaveError = "プロジェクト：" + ex.Message; }
        }
        var packageStamp = GenrePackageStamp;
        if (packageStamp != genrePackageSavedStamp && packageGenreTable is { } table && packageGenreSaveSession is { } session)
        {
            try
            {
                if (projectSavePath is not null && string.Equals(Path.GetFullPath(projectSavePath), session.Path, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("イベントとパッケージに同じ保存先は使用できません。");
                session.Save(table, Handle, WorkDate, GenreSaveLog, !GenrePackageChanged,
                    EditorConnection.Current.UpdatePortableGenreTable, EditorConnection.Current.ParsePortable);
                genrePackageSavedStamp = packageStamp;
            }
            catch (Exception ex) { success = false; genreSaveError = (genreSaveError is null ? "" : genreSaveError + " / ") + "パッケージ：" + ex.Message; }
        }
        if (!success) mappingChangeTag?.SaveFailed(genreSaveError!);
        else mappingChangeTag?.SaveSucceeded();
        return success;
    }

    private bool FinishGenreScope()
    {
        SyncMappingChangeTag();
        if (mappingComposition.Length > 0 || mappingChangeTag?.CanClose != true)
        {
            SetMappingTextFocus(true);
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
        if (project) RestoreGenreProject();
        else if (packageGenreSaveSession is { } session)
        {
            packageGenreTable = session.OpeningTable;
            packageGenreScroll = 0;
            selectedPackageGenreKey = null;
        }
        mappingWidth = -1;
        mappingFocus = -1;
        SyncMappingChangeTag();
        genreCloseAfterDiscard = !GenreScopeChanged;
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
