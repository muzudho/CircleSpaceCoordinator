namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Desktop.Windows.Persistence;
using StationeryUI.Controls;
using Forms = System.Windows.Forms;

public sealed partial class VenueEditorGame
{
    private void ExportFrameLayout()
    {
        if (workspace is null) return;
        var owner = workspace;
        var layout = owner.Project.DeskLayouts.Single(item => item.Id == owner.SelectedDeskLayoutId);
        using var form = new Forms.Form { Text = "フレーム配置データを書き出す", Width = 540, Height = 295,
            StartPosition = Forms.FormStartPosition.CenterScreen, FormBorderStyle = Forms.FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false };
        var label = new Forms.Label { Text = $"フレーム配置：{layout.Name}\n会場名：{owner.Project.Venue.Name}", Left = 16, Top = 16, Width = 490, Height = 65 };
        var secret = owner.Project.IsConfidential || layout.IsConfidential;
        var confidential = new Forms.CheckBox { Text = "マル秘（受渡し時に警告する）", Checked = secret,
            Enabled = !secret, Left = 16, Top = 100, Width = 490 };
        var detail = new Forms.Label { Text = "会場・フレーム定義・申込スペース定義・島定義を含めます。\nサークルデータとサークル配置案は含めません。", Left = 16, Top = 136, Width = 490, Height = 42 };
        var export = new Forms.Button { Text = "書き出す", Left = 290, Top = 198, Width = 100, DialogResult = Forms.DialogResult.OK };
        var cancel = new Forms.Button { Text = "キャンセル", Left = 400, Top = 198, Width = 100, DialogResult = Forms.DialogResult.Cancel };
        form.Controls.AddRange([label, confidential, detail, export, cancel]);
        form.AcceptButton = export;
        form.CancelButton = cancel;
        if (form.ShowDialog() != Forms.DialogResult.OK) { modalInputDrain = true; return; }
        modalInputDrain = true;
        var isConfidential = confidential.Checked;
        var project = owner.Project with {
            DeskLayouts = owner.Project.DeskLayouts.Select(item => item.Id == layout.Id
                ? item with { IsConfidential = isConfidential } : item).ToArray() };
        void Write()
        {
            try
            {
                var json = FrameLayoutPortableService.Export(project, layout.Id, CurrentSpaceDefinitions);
                using var dialog = new Forms.SaveFileDialog { Title = "フレーム配置データの書き出し先", DefaultExt = "frame-layout.json",
                    Filter = "フレーム配置データ (*.frame-layout.json)|*.frame-layout.json", FileName = "layout.frame-layout.json", OverwritePrompt = true };
                if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
                if (string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(projectSavePath!), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("編集中のイベントとは別のファイルを指定してください。");
                // Validate the complete output before any write or workspace mutation.
                var snapshot = FrameLayoutPortableService.Import(json).DeskLayouts[0].Definitions!;
                FrameLayoutPortableService.SaveDocument(dialog.FileName, json, overwrite: true);
                if (isConfidential) owner.Execute(new SetFrameLayoutConfidential(layout.Id), selectedPlanEdit: false);
                var current = CurrentSpaceDefinitions;
                owner.Execute(new SetFrameLayoutDefinitions(layout.Id, current with
                { Types = current.Types.Concat(snapshot.Types.Where(type => current.Types.All(existing => existing.Id != type.Id))).ToArray() }), selectedPlanEdit: false);
                ShowInAppMessage("フレーム配置データを書き出しました", "定義をこのフレーム配置に保存しました。\nイベントを保存すると、次回も同じ定義を使えます。");
            }
            catch (Exception ex) { ShowInAppMessage("書き出せません", ex.Message); }
            finally { modalInputDrain = true; }
        }
        if (isConfidential)
            OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "マル秘データの書き出し",
                "このフレーム配置データはマル秘です。\n受渡し先を確認してから書き出してください。続けますか？"),
                action => { if (action == ModalDialogAction.Accept) Write(); },
                [("書き出す", ModalDialogAction.Accept), ("キャンセル", ModalDialogAction.Cancel)]);
        else Write();
    }

    private void ImportFrameLayout()
    {
        try
        {
            using var dialog = new Forms.OpenFileDialog { Title = "フレーム配置データを読み込む", CheckFileExists = true,
                Filter = "フレーム配置データ (*.frame-layout.json)|*.frame-layout.json|JSON (*.json)|*.json" };
            if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
            var project = FrameLayoutPortableService.Import(File.ReadAllText(dialog.FileName));
            var existingNames = workspace?.Project.DeskLayouts.Select(layout => layout.Name).ToHashSet(StringComparer.Ordinal)
                ?? new HashSet<string>(StringComparer.Ordinal);
            var originalName = project.DeskLayouts[0].Name;
            var nameExists = existingNames.Contains(originalName);
            void AddLayout()
            {
                try
                {
                    if (workspace is null) return;
                    var id = "desk-import-" + Guid.NewGuid().ToString("N");
                    workspace.Execute(new CircleSpaceCoordinator.Engine.Model.ImportFrameLayout(project, id, project.DeskLayouts[0].Name), selectedPlanEdit: false);
                    workspace.SelectDeskLayout(id);
                    CancelInProgressPointerInteraction();
                    planScroll = 0;
                    CreateToolbar();
                    ShowInAppMessage("フレーム配置案を読み込みました", "現在のイベントに追加しました。イベントを保存するとファイルに残ります。");
                }
                catch (Exception ex) { ShowInAppMessage("読み込めません", ex.Message); }
                finally { modalInputDrain = true; }
            }
            var title = project.IsConfidential ? "マル秘データの読込み" : "フレーム配置データの読込み";
            var warning = project.IsConfidential ? "マル秘のデータです。取扱いに注意してください。\n" : "";
            void ReadWithName()
            {
                var suggested = originalName.Length > 85 ? originalName[..85] : originalName;
                if (existingNames.Contains(suggested))
                {
                    var stem = suggested;
                    var suffix = 2;
                    do { suggested = $"{stem}（読込{suffix++}）"; } while (existingNames.Contains(suggested));
                }
                OpenUnderlineInput("名前を付けて読込", suggested, name =>
                {
                    project = project with { DeskLayouts = [project.DeskLayouts[0] with { Name = name }] };
                    AddLayout();
                }, "読み込むフレーム配置案の名前を入力してください。\n既存の配置案は変更せず、現在のイベントにフレーム配置案として追加します。",
                    validate: name => CircleSpaceCoordinator.Desktop.Core.Interaction.EditorDialogValidation.Name(name)
                        ?? (existingNames.Contains(name.Trim()) ? "同名のフレーム配置案があります。別の名前を入力してください。" : null));
            }
            var duplicateWarning = nameExists ? "\n現在のイベントに同名のフレーム配置案があります。別の名前を付けて読み込んでください。" : "";
            OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, title,
                $"{warning}会場名：{project.Venue.Name}\nフレーム配置：{originalName}\n現在のイベントにフレーム配置案として追加します。{duplicateWarning}"),
                action =>
                {
                    if (action == ModalDialogAction.Increase || action == ModalDialogAction.Accept && nameExists) ReadWithName();
                    else if (action == ModalDialogAction.Accept) AddLayout();
                }, nameExists
                    ? [("名前を付けて読込", ModalDialogAction.Accept), ("キャンセル", ModalDialogAction.Cancel)]
                    : [("読込", ModalDialogAction.Accept), ("名前を付けて読込", ModalDialogAction.Increase), ("キャンセル", ModalDialogAction.Cancel)]);
        }
        catch (Exception ex) { ShowInAppMessage("読み込めません", ex.Message); }
        finally { modalInputDrain = true; }
    }
}
