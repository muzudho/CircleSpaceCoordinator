namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.EditorClient;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private void OpenSavePoints()
    {
        if (workspace is null || projectSavePath is null) return;
        OpenSelection("セーブポイント", ["一覧・復元・保護", "現在の状態を作成", $"保存先：{BackupDirectory}",
            $"通常の保持数：{settings?.Current.BackupGenerations ?? 10}（保護は別枠）"], 0, index =>
        {
            switch (index)
            {
                case 0: ShowSavePointList(); break;
                case 1:
                    OpenUnderlineInput("セーブポイントの名前", DateTime.Now.ToString("yyyy-MM-dd HH:mm"), name =>
                    {
                        SavePoints.Create(EditorConnection.Current.Encode(workspace!.Project), name);
                        ShowSavePointList();
                    }, "現在の確定済み編集を保存します。大切なものは一覧から保護してください。", cancelled: OpenSavePoints);
                    break;
                case 2:
                    OpenUnderlineInput("バックアップの保存フォルダー", BackupDirectory, path =>
                    {
                        settings!.ConfigureBackups(path, settings.Current.BackupGenerations);
                        OpenSavePoints();
                    }, "保存先変更後も古いフォルダーのバックアップは残ります。\nWindowsの同じユーザーでのみ復号できます。通常・マル秘とも暗号化します。", 32767, cancelled: OpenSavePoints);
                    break;
                case 3:
                    OpenUnderlineInput("通常の保持数", (settings?.Current.BackupGenerations ?? 10).ToString(), value =>
                    {
                        settings!.ConfigureBackups(BackupDirectory, int.Parse(value));
                        OpenSavePoints();
                    }, "1～1000件。保護したセーブポイントはこの枠に含みません。\n次のセーブポイント作成時に古い通常分を整理します。",
                        validate: value => int.TryParse(value, out var count) && count is >= 1 and <= 1000 ? null : "1～1000の整数を入力してください。", cancelled: OpenSavePoints);
                    break;
            }
        });
    }

    private void ShowSavePointList()
    {
        try
        {
            var store = SavePoints;
            var points = store.List();
            if (points.Count == 0) { ShowNotice("セーブポイント", "まだありません。最初の自動保存前、または手動で作成します。", OpenSavePoints); return; }
            OpenSelection("セーブポイント（新しい順）", points.Select(point =>
                $"{(point.Protected ? "［保護］" : "［通常］")} {point.Created.LocalDateTime:yyyy-MM-dd HH:mm:ss}　{point.Name}").ToArray(), 0, index =>
            {
                var selected = points[index];
                OpenSelection(selected.Name, ["この状態に戻す", selected.Protected ? "保護を解除" : "自動削除から保護"], 0, action =>
                {
                    if (action == 1) { store.SetProtected(selected.Id, !selected.Protected); ShowSavePointList(); return; }
                    OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "セーブポイントに戻す",
                        $"{selected.Created.LocalDateTime:yyyy-MM-dd HH:mm:ss} の状態に戻します。\n現在の状態は保護付きのセーブポイントに退避します。\n復元すると現在のUndo履歴は終了します。"), decision =>
                    {
                        if (decision != ModalDialogAction.Accept) { ShowSavePointList(); return; }
                        try { RestoreSavePoint(store, selected.Id); }
                        catch (Exception ex) { ShowInAppMessage("復元できません", ex.Message); }
                    }, [("この状態に戻す", ModalDialogAction.Accept), ("キャンセル", ModalDialogAction.Cancel)]);
                }, ShowSavePointList);
            }, OpenSavePoints);
        }
        catch (Exception ex) { ShowNotice("セーブポイントを読めません", ex.Message, OpenSavePoints); }
    }

    private void RestoreSavePoint(SavePointStore store, string id)
    {
        if (workspace is null || projectSavePath is null) return;
        var candidate = EditorConnection.Current.Decode(store.Read(id));
        candidate = candidate with { IsConfidential = candidate.IsConfidential || workspace.Project.IsConfidential ||
            workspace.Project.DeskLayouts.Any(layout => layout.IsConfidential) || workspace.Project.ChannelKnowledge.Any(item => item.IsConfidential) };
        var json = EditorConnection.Current.Encode(candidate);
        store.Create(EditorConnection.Current.Encode(workspace.Project), "復元直前 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), protect: true);
        autoSaveSession!.Save(json, SavePoints, DateOnly.FromDateTime(DateTime.Now));
        var path = projectSavePath;
        CloseEventProject();
        OpenEventProject(path);
        if (workspace is not null) ShowInAppMessage("復元しました", "復元直前の状態は保護付きセーブポイントに残っています。");
    }
}
