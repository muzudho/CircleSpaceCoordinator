namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.EditorClient;
using Microsoft.Xna.Framework;

public sealed partial class VenueEditorGame
{
    private AutoSaveSession? autoSaveSession;
    private object? autoSaveOwner;
    private (long Revision, double Zoom, double X, double Y)? autoSaveObserved;
    private double autoSaveChangedAt;
    private string? autoSaveError;
    private bool autoSavePending;
    private string BackupDirectory => settings?.Current.BackupDirectory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CircleSpaceCoordinator", "SavePoints");
    private SavePointStore SavePoints => new(BackupDirectory, projectSavePath!, settings?.Current.BackupGenerations ?? 10);

    private void InitializeAutoSave()
    {
        if (workspace is null || projectSavePath is null) return;
        autoSaveSession = new AutoSaveSession(projectSavePath);
        autoSaveOwner = workspace;
        autoSaveError = null;
        autoSavePending = false;
        autoSaveObserved = (workspace.Revision, viewport.Zoom, viewport.Origin.X, viewport.Origin.Y);
        savedProjectState = (workspace, workspace.Revision, projectSavePath, viewport.Zoom, viewport.Origin.X, viewport.Origin.Y);
    }

    private void UpdateAutoSave()
    {
        UpdateGenreScopeAutoSave();
        if (workspace is null || projectSavePath is null) return;
        if (!ReferenceEquals(autoSaveOwner, workspace)) InitializeAutoSave();
        var state = (workspace.Revision, viewport.Zoom, viewport.Origin.X, viewport.Origin.Y);
        if (autoSaveObserved != state) { autoSaveObserved = state; autoSaveChangedAt = statusHintTime; autoSavePending = false; }
        if (IsCurrentProjectSaved || autoSaveError is not null || statusHintTime - autoSaveChangedAt < 1.5) return;
        // One rendered frame announces the save before synchronous, atomic persistence.
        if (!autoSavePending) { autoSavePending = true; return; }
        SaveProject();
    }

    private string AutoSaveLabel => autoSaveError is not null ? "自動保存失敗：すぐ保存で再試行" :
        autoSavePending ? "保存中…" : IsCurrentProjectSaved ? "保存できています" : "変更あり・自動保存待ち";

    private void DrawAutoSaveStatus(int width, int top)
    {
        if (workspace is null) return;
        if (mappingKnowledgeComments && mappingDraft is not null) { DrawGenreScopeAutoSave(width, top); return; }
        textRenderer?.Draw(AutoSaveLabel, new Rectangle(Math.Max(0, width - 260), top + 5, 246, 23),
            autoSaveError is null ? new Color(160, 220, 195) : Color.OrangeRed, 14, true);
    }

    private bool FlushAutoSave()
    {
        if (workspace is null || projectSavePath is null) return true;
        if (IsCurrentProjectSaved && autoSaveError is null) return true;
        if (SaveProject().Success) return true;
        ShowInAppMessage("保存できないため閉じられません", autoSaveError ?? "［すぐ保存］で再試行してください。");
        return false;
    }
}
