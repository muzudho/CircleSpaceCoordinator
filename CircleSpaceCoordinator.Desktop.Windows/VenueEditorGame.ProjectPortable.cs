namespace CircleSpaceCoordinator.Desktop.Windows;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Engine.Model;
using Forms = System.Windows.Forms;

public sealed partial class VenueEditorGame
{
    private void ExportPortable()
    {
        if (workspace is null) return;
        try
        {
            var snapshot = workspace.Project;
            using var form = PortableForm("部分書出し — 配置案・知見を選択");
            var rows = PortableGrid(form);
            foreach (var layout in snapshot.DeskLayouts)
                rows.Rows.Add(layout.Id == workspace.SelectedDeskLayoutId, layout.Name, layout.Description ?? "", layout.Id);
            foreach (var item in snapshot.ChannelKnowledge)
                rows.Rows[rows.Rows.Add(false, "知見：" + item.Name, item.Purpose, item.Id)].Tag = "knowledge";
            var library = new Forms.Button { Text = "知見の保存・列対応", Left = 525, Top = 10, Width = 190 };
            library.Click += (_, _) => { form.Close(); ManageChannelKnowledge(); };
            form.Controls.Add(library);
            var name = PortableText(form, "ファイルの名前", 340, "配置の提案");
            var description = PortableText(form, "ファイル全体のメモ", 372, "");
            var tags = PortableText(form, "タグ（カンマ区切り）", 404, "");
            var secret = new Forms.CheckBox { Text = "マル秘として書き出す", Left = 16, Top = 442, Width = 300,
                Checked = snapshot.IsConfidential, Enabled = !snapshot.IsConfidential };
            form.Controls.Add(secret);
            var template = new Forms.CheckBox { Text = "知見はひな形のみ（座標を除く）", Left = 350, Top = 442, Width = 360 };
            form.Controls.Add(template);
            var status = PortableStatus(form);
            var accept = PortableButtons(form, rows, "内容を確認");
            accept.Click += (_, _) =>
            {
                try
                {
                    rows.EndEdit();
                    var selected = rows.Rows.Cast<Forms.DataGridViewRow>().Where(row => Equals(row.Cells[0].Value, true)).ToArray();
                    var layoutIds = selected.Where(row => row.Tag is null).Select(row => (string)row.Cells[3].Value!).ToArray();
                    var definitions = snapshot.DeskLayouts.Any(layout => layoutIds.Contains(layout.Id) && layout.Definitions is null)
                        ? SpaceDefinitions.Current : new CircleSpaceCoordinator.Core.Model.SpaceDefinitionCatalog([], []);
                    var json = EditorConnection.Current.ExportPortable(new(snapshot, layoutIds, definitions,
                        name.Text.Trim(), description.Text, tags.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries), secret.Checked)
                    { KnowledgeIds = selected.Where(row => Equals(row.Tag, "knowledge")).Select(row => (string)row.Cells[3].Value!).ToArray(), TemplateOnly = template.Checked });
                    var package = EditorConnection.Current.ParsePortable(json);
                    var summary = string.Join("\n", package.Items.Select(item => $"・{item.Name}（型 {item.Project.DeskTypes.Count} 件、申込定義 {item.Project.DeskLayouts[0].Definitions!.Requests.Count} 件）")
                        .Concat(package.Knowledge.Select(item => $"・知見：{item.Name}（{(item.Venue is null ? "ひな形" : "会場の重み付き")}）\n{item.InputRule.Meaning}／{item.InputRule.ValueMeanings}")));
                    if (Forms.MessageBox.Show(form, $"{(package.IsConfidential ? "【マル秘】受渡し先を確認してください。\n" : "")}{summary}\n{(package.Items.Count > 0 ? "配置案には会場・定義・番号・島・必要なブロック表示を同梱します。\n" : "")}サークル実データは含みません。\n\n{package.Description}",
                        "書き出す内容の確認", Forms.MessageBoxButtons.OKCancel) != Forms.DialogResult.OK) return;
                    using var dialog = new Forms.SaveFileDialog { Title = "部分書出しの保存先", DefaultExt = "project-portable.json",
                        Filter = "ポータブルデータ (*.project-portable.json)|*.project-portable.json", FileName = "proposal.project-portable.json", OverwritePrompt = true };
                    if (dialog.ShowDialog(form) != Forms.DialogResult.OK) return;
                    if (projectSavePath is not null && string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(projectSavePath), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("編集中のイベントとは別のファイルを指定してください。");
                    FrameLayoutPortableService.SaveDocument(dialog.FileName, json, overwrite: true);
                    form.DialogResult = Forms.DialogResult.OK;
                }
                catch (Exception ex) { status.Text = ex.Message; }
            };
            if (form.ShowDialog() == Forms.DialogResult.OK)
                ShowInAppMessage("部分書出しが完了しました", "選択した項目を保存しました。");
        }
        catch (Exception ex) { ShowInAppMessage("書き出せません", ex.Message); }
        finally { modalInputDrain = true; }
    }

    private void ImportPortable()
    {
        if (workspace is null) return;
        var owner = workspace;
        try
        {
            using var dialog = new Forms.OpenFileDialog { Title = "部分読込み", CheckFileExists = true,
                Filter = "ポータブル・旧フレーム配置 (*.json)|*.project-portable.json;*.frame-layout.json|JSON (*.json)|*.json" };
            if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
            if (new FileInfo(dialog.FileName).Length > 16 * 1024 * 1024)
                throw new InvalidDataException("ファイルは16 MiB以内にしてください。");
            // Capture once. Confirmation and application use this exact payload and revision.
            var package = EditorConnection.Current.ParsePortable(File.ReadAllText(dialog.FileName));
            var revision = owner.Revision;
            using var form = PortableForm("部分読込み — 配置案・知見を選択・改名");
            var rows = PortableGrid(form);
            rows.Columns[1].ReadOnly = false;
            foreach (var item in package.Items)
                rows.Rows.Add(false, item.Name, item.Project.DeskLayouts[0].Description ?? "", item.Id);
            foreach (var item in package.Knowledge)
                rows.Rows.Add(false, item.Name, $"知見・未対応付け：{item.Purpose}／{item.InputRule.ValueMeanings}", "knowledge:" + item.Id);
            var details = new Forms.TextBox { Left = 16, Top = 340, Width = 700, Height = 118,
                Multiline = true, ReadOnly = true, ScrollBars = Forms.ScrollBars.Vertical,
                        Text = $"{(package.IsConfidential ? "【マル秘】\r\n" : "")}{package.Name}\r\n{package.Description}\r\nタグ：{string.Join(", ", package.Tags)}\r\n知見は未対応付けで保存します。列へ対応付ける際に会場を検証します。" };
            form.Controls.Add(details);
            rows.SelectionChanged += (_, _) =>
            {
                if (rows.CurrentRow?.Cells[3].Value is not string id) return;
                var item = package.Knowledge.FirstOrDefault(knowledge => "knowledge:" + knowledge.Id == id);
                if (item is null) return;
                details.Text = $"{(package.IsConfidential ? "【マル秘】" : "")}知見：{item.Name}（未対応付けで保存）\r\n{item.Description}\r\n狙い：{item.Purpose}\r\n推奨列：{item.InputRule.RecommendedColumn}／{item.InputRule.Meaning}\r\n{item.InputRule.ValueMeanings}／空欄：{(item.InputRule.BlankIsZero ? "0" : "エラー")}\r\n係数：{item.Scale}, {item.Offset}, {item.OverallWeight}／既定重み：{item.DefaultWeight}\r\n{(item.Venue is null ? "ひな形（座標なし）" : $"会場：{item.Venue.Name}／重み {item.Cells.Length} セル。列対応時に会場を検証します。")}";
            };
            var status = PortableStatus(form);
            var accept = PortableButtons(form, rows, "取込みを確認");
            accept.Click += (_, _) =>
            {
                try
                {
                    rows.EndEdit();
                    var selection = rows.Rows.Cast<Forms.DataGridViewRow>().Where(row => Equals(row.Cells[0].Value, true))
                        .Select(row => new PortableImportItem((string)row.Cells[3].Value!, "desk-import-" + Guid.NewGuid().ToString("N"),
                            Convert.ToString(row.Cells[1].Value)?.Trim() ?? "")).ToArray();
                    if (owner.Revision != revision) throw new InvalidOperationException("イベントが変更されました。閉じて、もう一度読み込んでください。");
                    var preview = EditorConnection.Current.PreviewPortable(new(owner.Project, package, selection));
                    var summary = string.Join("\n", selection.Select(item => "・" + item.Name));
                    if (Forms.MessageBox.Show(form, $"{(preview.IsConfidential ? "【マル秘】取込先にも引き継ぎます。\n" : "")}{summary}\n追加：{preview.LayoutCount} 案、知見 {preview.KnowledgeCount} 件、型 {preview.TypeCount} 件\n配置用定義 {preview.DefinitionCount} 件・申込定義 {preview.RequestCount} 件\n会場：{preview.VenueAction}（{preview.VenueName}）\n配置の会場形状とブロック表示は衝突検査済み。\n知見は未対応付けで保存し、列対応時に会場を検証します。\n１回のUndoで戻せます。",
                        "追加内容の確認", Forms.MessageBoxButtons.OKCancel) != Forms.DialogResult.OK) return;
                    if (owner.Revision != revision) throw new InvalidOperationException("イベントが変更されました。再確認が必要です。");
                    owner.Execute(new ImportPortableSelection(package, selection), selectedPlanEdit: false);
                    var layoutSelection = selection.FirstOrDefault(item => package.Items.Any(source => source.Id == item.ItemId));
                    if (layoutSelection is not null) owner.SelectDeskLayout(layoutSelection.NewId);
                    form.DialogResult = Forms.DialogResult.OK;
                }
                catch (Exception ex) { status.Text = ex.Message; }
            };
            if (form.ShowDialog() == Forms.DialogResult.OK)
            {
                CancelInProgressPointerInteraction();
                planScroll = 0;
                CreateToolbar();
                ShowInAppMessage("部分読込みが完了しました", "選択した項目を追加しました。イベントを保存するとファイルに残ります。");
            }
        }
        catch (Exception ex) { ShowInAppMessage("読み込めません", ex.Message); }
        finally { modalInputDrain = true; }
    }

    private static Forms.Form PortableForm(string title) => new()
    {
        Text = title, ClientSize = new System.Drawing.Size(740, 600), StartPosition = Forms.FormStartPosition.CenterScreen,
        FormBorderStyle = Forms.FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, AutoScaleMode = Forms.AutoScaleMode.Dpi,
    };

    private static Forms.DataGridView PortableGrid(Forms.Form form)
    {
        var grid = new Forms.DataGridView { Left = 16, Top = 44, Width = 700, Height = 280, AllowUserToAddRows = false,
            AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = Forms.DataGridViewAutoSizeColumnsMode.Fill };
        grid.Columns.Add(new Forms.DataGridViewCheckBoxColumn { HeaderText = "選択", FillWeight = 18 });
        grid.Columns.Add(new Forms.DataGridViewTextBoxColumn { HeaderText = "名前（読込み時は改名可）", ReadOnly = true });
        grid.Columns.Add(new Forms.DataGridViewTextBoxColumn { HeaderText = "説明（表示のみ）", ReadOnly = true });
        grid.Columns.Add(new Forms.DataGridViewTextBoxColumn { Visible = false });
        form.Controls.Add(grid);
        return grid;
    }

    private static Forms.TextBox PortableText(Forms.Form form, string label, int top, string value)
    {
        form.Controls.Add(new Forms.Label { Text = label, Left = 16, Top = top + 3, Width = 155 });
        var text = new Forms.TextBox { Text = value, Left = 175, Top = top, Width = 540 };
        form.Controls.Add(text);
        return text;
    }

    private static Forms.Label PortableStatus(Forms.Form form)
    {
        var label = new Forms.Label { Left = 16, Top = 478, Width = 700, Height = 65, ForeColor = System.Drawing.Color.DarkRed };
        form.Controls.Add(label);
        return label;
    }

    private static Forms.Button PortableButtons(Forms.Form form, Forms.DataGridView grid, string caption)
    {
        var all = new Forms.Button { Text = "すべて選択", Left = 16, Top = 10, Width = 100 };
        var none = new Forms.Button { Text = "選択解除", Left = 125, Top = 10, Width = 100 };
        var count = new Forms.Label { Left = 245, Top = 15, Width = 200 };
        var accept = new Forms.Button { Text = caption, Left = 440, Top = 558, Width = 130 };
        var cancel = new Forms.Button { Text = "キャンセル", Left = 580, Top = 558, Width = 135, DialogResult = Forms.DialogResult.Cancel };
        void Update()
        {
            var selected = grid.Rows.Cast<Forms.DataGridViewRow>().Count(row => Equals(row.Cells[0].Value, true));
            count.Text = $"{selected} / {grid.Rows.Count} 項目を選択";
            accept.Enabled = selected > 0;
        }
        all.Click += (_, _) => { foreach (Forms.DataGridViewRow row in grid.Rows) row.Cells[0].Value = true; Update(); };
        none.Click += (_, _) => { foreach (Forms.DataGridViewRow row in grid.Rows) row.Cells[0].Value = false; Update(); };
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(Forms.DataGridViewDataErrorContexts.Commit); };
        grid.CellValueChanged += (_, _) => Update();
        form.Controls.AddRange([all, none, count, accept, cancel]);
        form.CancelButton = cancel;
        Update();
        return accept;
    }
}
