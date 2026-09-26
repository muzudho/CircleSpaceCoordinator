namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Screenshots;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private string CurrentScreenshotDirectory => projectSavePath is { } path && workspace is { } owner
        ? settings?.GetScreenshotDirectory(path) ?? ScreenshotPath.ForProject(owner.Project.Id)
        : ScreenshotPath.DefaultDirectory;

    private void OpenProjectProfile()
    {
        if (workspace is null || projectSavePath is null) return;
        var confidential = workspace.Project.IsConfidential || workspace.Project.DeskLayouts.Any(item => item.IsConfidential);
        var message = $"イベント：{workspace.Project.Name}\nスクリーンショット保存フォルダー：\n{CurrentScreenshotDirectory}\n" +
            (confidential ? "（秘）PNGは暗号化されません。共有・同期先に注意してください。\n" : "") +
            "以前に撮った画像は、従来の共通フォルダーに残っています。";
        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "プロジェクト・プロフィール", message), action =>
        {
            switch (action)
            {
                case ModalDialogAction.Accept:
                    OpenScreenshotDirectory(CurrentScreenshotDirectory);
                    break;
                case ModalDialogAction.Increase:
                    EditScreenshotDirectory();
                    break;
                case ModalDialogAction.Decrease:
                    OpenScreenshotDirectory(ScreenshotPath.DefaultDirectory);
                    break;
            }
        }, [("保存フォルダーを開く", ModalDialogAction.Accept), ("保存先を変更", ModalDialogAction.Increase),
            ("以前の保存先", ModalDialogAction.Decrease), ("戻る", ModalDialogAction.Cancel)]);
    }

    private void EditScreenshotDirectory() => OpenUnderlineInput(
        "スクリーンショット保存フォルダー", CurrentScreenshotDirectory, value =>
        {
            settings!.SaveScreenshotDirectory(projectSavePath!, value.Trim('"'));
            OpenProjectProfile();
        }, "このイベントだけの保存先を絶対パスで指定します。変更前の画像は移動しません。", 32767,
        validate: value => Path.IsPathFullyQualified(value.Trim('"')) ? null : "絶対パスを入力してください。",
        cancelled: OpenProjectProfile);

    private void OpenScreenshotDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(directory) { UseShellExecute = true });
            OpenProjectProfile();
        }
        catch (Exception ex) { ShowNotice("スクリーンショット保存フォルダーを開けません", ex.Message, OpenProjectProfile); }
    }
}
