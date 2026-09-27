namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.Desktop.Core.Screenshots;
using StationeryUI.Controls;
using Forms = System.Windows.Forms;

internal enum ProjectProfileTab { Screenshot, CircleData }

public sealed partial class VenueEditorGame
{
    private const ModalDialogAction ProfileScreenshotTabAction = (ModalDialogAction)110;
    private const ModalDialogAction ProfileCircleDataTabAction = (ModalDialogAction)111;
    private const ModalDialogAction ProfileOpenFolderAction = (ModalDialogAction)112;
    private const ModalDialogAction ProfileChangeFolderAction = (ModalDialogAction)113;
    private const ModalDialogAction ProfileImportAction = (ModalDialogAction)114;
    private bool projectProfileOpen;
    private ProjectProfileTab projectProfileTab;

    private string CurrentScreenshotDirectory => projectSavePath is { } path && workspace is { } owner
        ? settings?.GetScreenshotDirectory(path) ?? ScreenshotPath.ForProject(owner.Project.Id)
        : ScreenshotPath.DefaultDirectory;

    private string CurrentParticipantImportDirectory => projectSavePath is { } path
        ? settings?.GetParticipantImportDirectory(path) ?? ParticipantExampleFiles.GetDefaultDirectory()
        : ParticipantExampleFiles.GetDefaultDirectory();

    private void OpenProjectProfile(ProjectProfileTab tab = ProjectProfileTab.Screenshot)
    {
        if (workspace is null || projectSavePath is null) return;
        projectProfileTab = tab;
        var confidential = workspace.Project.IsConfidential || workspace.Project.DeskLayouts.Any(item => item.IsConfidential);
        string message;
        if (tab == ProjectProfileTab.Screenshot)
        {
            message = $"イベント：{workspace.Project.Name}\nスクリーンショット保存フォルダー：\n{CurrentScreenshotDirectory}\n" +
                (confidential ? "（秘）PNGは暗号化されません。共有・同期先に注意してください。" : "");
        }
        else
        {
            string? installError = null;
            if (settings?.GetParticipantImportDirectory(projectSavePath) is null)
                try
                {
                    ParticipantExampleFiles.InstallMissing(
                        Path.Combine(AppContext.BaseDirectory, "examples"), ParticipantExampleFiles.GetDefaultDirectory());
                }
                catch (Exception ex) { installError = ex.Message; }
            message = $"イベント：{workspace.Project.Name}\nサークルデータを開くフォルダー：\n{CurrentParticipantImportDirectory}\n" +
                (installError is null ? "［Excel / CSV を選ぶ］から一覧を取り込みます。" : $"サンプルの配置に失敗しました：{installError}");
        }
        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "プロジェクト・プロフィール", message), action =>
        {
            switch (action)
            {
                case ProfileScreenshotTabAction: OpenProjectProfile(ProjectProfileTab.Screenshot); break;
                case ProfileCircleDataTabAction: OpenProjectProfile(ProjectProfileTab.CircleData); break;
                case ProfileOpenFolderAction:
                    OpenProfileFolder(tab == ProjectProfileTab.Screenshot ? CurrentScreenshotDirectory : CurrentParticipantImportDirectory, tab);
                    break;
                case ProfileChangeFolderAction:
                    if (tab == ProjectProfileTab.Screenshot) EditScreenshotDirectory();
                    else EditParticipantImportDirectory();
                    break;
                case ProfileImportAction: OpenParticipantImport(); break;
            }
        }, tab == ProjectProfileTab.Screenshot
            ? [("スクリーンショット", ProfileScreenshotTabAction), ("サークルデータ", ProfileCircleDataTabAction),
                ("保存フォルダーを開く", ProfileOpenFolderAction), ("保存場所を変更する", ProfileChangeFolderAction),
                ("戻る", ModalDialogAction.Cancel)]
            : [("スクリーンショット", ProfileScreenshotTabAction), ("サークルデータ", ProfileCircleDataTabAction),
                ("フォルダーを開く", ProfileOpenFolderAction), ("フォルダーを変更する", ProfileChangeFolderAction),
                ("Excel / CSV を選ぶ", ProfileImportAction), ("戻る", ModalDialogAction.Cancel)]);
        projectProfileOpen = true;
    }

    private void EditScreenshotDirectory() => OpenUnderlineInput(
        "スクリーンショット保存フォルダー", CurrentScreenshotDirectory, value =>
        {
            settings!.SaveScreenshotDirectory(projectSavePath!, value.Trim('"'));
            OpenProjectProfile(ProjectProfileTab.Screenshot);
        }, "このイベントだけの保存先を絶対パスで指定します。変更前の画像は移動しません。", 32767,
        validate: value => Path.IsPathFullyQualified(value.Trim('"')) ? null : "絶対パスを入力してください。",
        cancelled: () => OpenProjectProfile(ProjectProfileTab.Screenshot));

    private void EditParticipantImportDirectory()
    {
        try
        {
            using var picker = new Forms.FolderBrowserDialog
            {
                Description = "サークルデータを開くフォルダーを選択",
                SelectedPath = CurrentParticipantImportDirectory,
                UseDescriptionForTitle = true,
            };
            if (picker.ShowDialog() == Forms.DialogResult.OK)
                settings!.SaveParticipantImportDirectory(projectSavePath!, picker.SelectedPath);
            OpenProjectProfile(ProjectProfileTab.CircleData);
        }
        catch (Exception ex) { ShowNotice("サークルデータのフォルダーを変更できません", ex.Message,
            () => OpenProjectProfile(ProjectProfileTab.CircleData)); }
    }

    private void OpenProfileFolder(string directory, ProjectProfileTab tab)
    {
        try
        {
            Directory.CreateDirectory(directory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(directory) { UseShellExecute = true });
            OpenProjectProfile(tab);
        }
        catch (Exception ex) { ShowNotice("フォルダーを開けません", ex.Message, () => OpenProjectProfile(tab)); }
    }
}
