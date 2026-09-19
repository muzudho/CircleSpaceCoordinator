namespace CircleSpaceCoordinator.Desktop.Windows;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Engine.Model;
using Forms = System.Windows.Forms;

public sealed partial class VenueEditorGame
{
    private void ExportPortable(bool genreTableOnly = false)
    {
        if (workspace is null || !EnsureHandle()) return;
        try
        {
            var snapshot = workspace.Project;
            using var form = PortableForm(genreTableOnly ? "ジャンルコード表をパッケージ直下へ書き出し" : "部分書出し — 配置案・知見・素材を選択");
            form.ClientSize = new System.Drawing.Size(740, 640);
            var rows = PortableGrid(form);
            foreach (var layout in snapshot.DeskLayouts.Where(_ => !genreTableOnly))
                rows.Rows.Add(false, layout.Name, layout.Description ?? "", layout.Id, "フレーム配置案");
            foreach (var item in snapshot.ChannelKnowledge.Where(_ => !genreTableOnly))
                rows.Rows[rows.Rows.Add(false, item.Name, item.Purpose, item.Id, "チャンネルの知見")].Tag = "knowledge";
            if (!genreTableOnly) rows.Rows[rows.Rows.Add(false, snapshot.Venue.Name, "会場の寸法・障害物・ゾーン", snapshot.Venue.Id, "会場")].Tag =
                new PortableMaterialSelection("venue", snapshot.Venue.Id);
            var sourceLayout = snapshot.DeskLayouts.FirstOrDefault(layout => layout.Id == workspace.SelectedDeskLayoutId);
            rows.Rows[rows.Rows.Add(genreTableOnly, snapshot.GetGenreCodeTableName(), "パッケージ直下。未使用ジャンル・コメント・並び順・変更タグを含む表全体", "project", "ジャンルコード表")].Tag =
                new PortableMaterialSelection("genre-styles", "project");
            if (!genreTableOnly) rows.Rows[rows.Rows.Add(false, "ブロック色の対応表", "対応表全体と変更タグ", "project", "ブロック色対応表")].Tag =
                new PortableMaterialSelection("block-styles", "project");
            var sourceDefinitions = genreTableOnly ? new CircleSpaceCoordinator.Core.Model.SpaceDefinitionCatalog([], []) : sourceLayout?.Definitions ?? SpaceDefinitions.Current;
            foreach (var type in sourceDefinitions.Types)
                rows.Rows[rows.Rows.Add(false, type.Name, $"元：{sourceLayout?.Name ?? "共通カタログ"}／{type.Width}×{type.Height}", type.Id, "フレーム定義")].Tag =
                    new PortableMaterialSelection("frame-definition", type.Id, sourceLayout?.Id);
            foreach (var request in sourceDefinitions.Requests)
                rows.Rows[rows.Rows.Add(false, request.Value, $"元：{sourceLayout?.Name ?? "共通カタログ"}／{request.Description}（参照先フレーム定義を同梱）", request.Id, "申込定義")].Tag =
                    new PortableMaterialSelection("request-definition", request.Id, sourceLayout?.Id);
            var library = new Forms.Button { Text = "知見・ライブラリー ▼", Left = 525, Top = 10, Width = 190 };
            using var libraryMenu = new Forms.ContextMenuStrip();
            libraryMenu.Items.Add("知見の保存・列対応", null, (_, _) => { form.Close(); ManageChannelKnowledge(); });
            libraryMenu.Items.Add("ファイルライブラリー", null, (_, _) => { form.Close(); ShowPortableLibrary(); });
            library.Click += (_, _) => libraryMenu.Show(library, new System.Drawing.Point(0, library.Height));
            form.Controls.Add(library);
            var name = PortableText(form, "パッケージのタイトル", 340, genreTableOnly ? snapshot.GetGenreCodeTableName() : "配置の提案");
            var description = PortableText(form, "ファイル全体のメモ", 372, "");
            var tags = PortableText(form, "タグ（カンマ区切り）", 404, "");
            var secret = new Forms.CheckBox { Text = "マル秘として書き出す", Left = 16, Top = 442, Width = 300,
                Checked = snapshot.IsConfidential, Enabled = !snapshot.IsConfidential };
            form.Controls.Add(secret);
            var template = new Forms.CheckBox { Text = "知見はひな形のみ（座標を除く）", Left = 350, Top = 442, Width = 360 };
            form.Controls.Add(template);
            template.Visible = !genreTableOnly;
            library.Visible = !genreTableOnly;
            var frameIds = selectedFrameIds.ToArray();
            if (!genreTableOnly && sourceLayout is not null && frameIds.Length == 0 && selectedCellRange is { } range)
            {
                var types = snapshot.DeskTypes.ToDictionary(type => type.Id);
                frameIds = sourceLayout.DeskPlacements.Where(desk => desk.GetOccupiedCells(types[desk.DeskTypeId]).All(range.Contains)).Select(desk => desk.Id).ToArray();
            }
            var fragment = new Forms.CheckBox { Text = $"現在の案は選択範囲のフレームだけを書き出す（{frameIds.Length} 件）",
                Left = 16, Top = 480, Width = 700, Enabled = frameIds.Length > 0 };
            form.Controls.Add(fragment);
            fragment.Visible = !genreTableOnly;
            var status = PortableStatus(form);
            var accept = PortableButtons(form, rows, "内容を確認");
            accept.Click += (_, _) =>
            {
                try
                {
                    rows.EndEdit();
                    var selected = rows.Rows.Cast<Forms.DataGridViewRow>().Where(row => Equals(row.Cells[0].Value, true)).ToArray();
                    var layoutIds = selected.Where(row => row.Tag is null).Select(row => (string)row.Cells[3].Value!).ToArray();
                    var definitions = snapshot.DeskLayouts.Any(layout => layoutIds.Contains(layout.Id) && layout.Definitions is null) ||
                        selected.Any(row => row.Tag is PortableMaterialSelection { Kind: "frame-definition" or "request-definition" }) && sourceLayout?.Definitions is null
                        ? SpaceDefinitions.Current : new CircleSpaceCoordinator.Core.Model.SpaceDefinitionCatalog([], []);
                    var fragments = new Dictionary<string, IReadOnlyList<string>>();
                    if (fragment.Checked && sourceLayout is not null && layoutIds.Contains(sourceLayout.Id)) fragments.Add(sourceLayout.Id, frameIds);
                    var json = EditorConnection.Current.ExportPortable(new(snapshot, layoutIds, definitions,
                        name.Text.Trim(), description.Text, tags.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries), secret.Checked)
                    { Handle = Handle, KnowledgeIds = selected.Where(row => Equals(row.Tag, "knowledge")).Select(row => (string)row.Cells[3].Value!).ToArray(), TemplateOnly = template.Checked,
                        Materials = selected.Select(row => row.Tag).OfType<PortableMaterialSelection>().ToArray(), Fragments = fragments });
                    var package = EditorConnection.Current.ParsePortable(json);
                    var summary = string.Join("\n", package.Items.Select(item => $"・{item.Name}（型 {item.Project.DeskTypes.Count} 件、申込定義 {item.Project.DeskLayouts[0].Definitions!.Requests.Count} 件）")
                        .Concat(package.Knowledge.Select(item => $"・知見：{item.Name}（{(item.Venue is null ? "ひな形" : "会場の重み付き")}）\n{item.InputRule.Meaning}／{item.InputRule.ValueMeanings}"))
                        .Concat(package.Materials.Select(item => $"・{MaterialKindName(item.Kind)}：{item.Name}（参照先 {item.Definitions?.Types.Count ?? 0} 件）")));
                    if (fragments.Count > 0) summary += "\n部分配置：元の座標・番号を保持します。境界をまたぐ接続と向かい合わせ領域は同梱しません。";
                    if (!ShowPortableReview(form, package, summary + "\nサークル実データは含みません。\n" + package.Description,
                        "書き出す内容の確認")) return;
                    using var dialog = new Forms.SaveFileDialog { Title = "パッケージの保存先", DefaultExt = "package-csc.json",
                        Filter = "パッケージ (*.package-csc.json)|*.package-csc.json", FileName = "proposal.package-csc.json", OverwritePrompt = true };
                    if (dialog.ShowDialog(form) != Forms.DialogResult.OK) return;
                    if (projectSavePath is not null && string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(projectSavePath), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("編集中のイベントとは別のファイルを指定してください。");
                    FrameLayoutPortableService.SaveDocument(dialog.FileName, json, overwrite: true);
                    workspace.Execute(new RecordPortableProviders(package,
                        selected.Select(row => row.Tag).OfType<PortableMaterialSelection>().ToArray(), definitions), selectedPlanEdit: false);
                    form.DialogResult = Forms.DialogResult.OK;
                }
                catch (Exception ex) { status.Text = ex.Message; }
            };
            if (ShowEditorDialog(form) == Forms.DialogResult.OK)
                ShowInAppMessage("部分書出しが完了しました", "選択した項目を保存しました。");
        }
        catch (Exception ex) { ShowInAppMessage("書き出せません", ex.Message); }
        finally { modalInputDrain = true; }
    }

    private void ImportPortable(string? libraryJson = null, bool genreTableOnly = false)
    {
        if (workspace is null) return;
        var owner = workspace;
        try
        {
            using var dialog = new Forms.OpenFileDialog { Title = "部分読込み", CheckFileExists = true,
                Filter = "パッケージ（旧形式を含む）|*.package-csc.json;*.project-portable.json;*.frame-layout.json|JSON (*.json)|*.json" };
            if (libraryJson is null)
            {
                if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
                if (new FileInfo(dialog.FileName).Length > 16 * 1024 * 1024)
                    throw new InvalidDataException("ファイルは16 MiB以内にしてください。");
                libraryJson = File.ReadAllText(dialog.FileName);
            }
            // Capture once. Confirmation and application use this exact payload and revision.
            var package = EditorConnection.Current.ParsePortable(libraryJson);
            genreTableOnly |= package.Items.Count == 0 && package.Knowledge.Count == 0 &&
                package.Materials.Count > 0 && package.Materials.All(item => item.Kind == "genre-styles");
            if (genreTableOnly && !package.Materials.Any(item => item.Kind == "genre-styles"))
                throw new InvalidDataException("このパッケージには独立したジャンルコード表がありません。");
            var revision = owner.Revision;
            using var form = PortableForm(genreTableOnly ? "ジャンルコード表をイベントプロジェクト直下へ読み込み" : "部分読込み — 配置案・知見・素材を選択・改名");
            form.ClientSize = new System.Drawing.Size(740, 730);
            var rows = PortableGrid(form);
            rows.Columns[1].ReadOnly = false;
            foreach (var item in package.Items.Where(_ => !genreTableOnly))
                rows.Rows.Add(false, item.Name, item.Project.DeskLayouts[0].Description ?? "", item.Id, item.Kind == "frame-fragment" ? "部分配置" : "フレーム配置案");
            foreach (var item in package.Knowledge.Where(_ => !genreTableOnly))
                rows.Rows.Add(false, item.Name, $"知見・未対応付け：{item.Purpose}／{item.InputRule.ValueMeanings}", "knowledge:" + item.Id, "チャンネルの知見");
            foreach (var material in package.Materials.Where(item => !genreTableOnly || item.Kind == "genre-styles"))
                rows.Rows.Add(genreTableOnly && package.Materials.Count(item => item.Kind == "genre-styles") == 1, material.Name, material.Kind is "genre-styles" or "block-styles" ? "イベント直下の表全体を置換。配置案の選択は不要。未使用ジャンルも保持。" : material.Definitions is { } defs ? $"参照先フレーム定義 {defs.Types.Count} 件。配置案または共通カタログへ追加。" : "既存配置がある場合は同じ形状の会場だけ採用可能。",
                    material.Id, MaterialKindName(material.Kind));
            var details = new Forms.TextBox { Left = 16, Top = 340, Width = 700, Height = 84,
                Multiline = true, ReadOnly = true, ScrollBars = Forms.ScrollBars.Vertical,
                        Text = $"{(package.IsConfidential ? "【マル秘】\r\n" : "")}{package.Name}\r\n{package.Description}\r\nタグ：{string.Join(", ", package.Tags)}\r\n知見は未対応付けで保存します。列へ対応付ける際に会場を検証します。" };
            form.Controls.Add(details);
            var target = new Forms.ComboBox { Left = 175, Top = 434, Width = 540, DropDownStyle = Forms.ComboBoxStyle.DropDownList };
            var targets = owner.Project.DeskLayouts.ToArray();
            target.Items.AddRange(targets.Select(layout => (object)layout.Name).ToArray());
            target.SelectedIndex = Array.FindIndex(targets, layout => layout.Id == owner.SelectedDeskLayoutId);
            var mode = new Forms.ComboBox { Left = 175, Top = 470, Width = 540, DropDownStyle = Forms.ComboBoxStyle.DropDownList };
            mode.Items.AddRange(["新規追加（定義は指定配置案へ／会場は採用）", "指定配置案へフレームを挿入", "指定配置案を置換（サークル配置からの参照なしのみ）", "共通カタログへ定義を登録（イベント外）"]);
            mode.SelectedIndex = 0;
            var offsetX = new Forms.NumericUpDown { Left = 175, Top = 506, Width = 110, Minimum = -10000, Maximum = 10000 };
            var offsetY = new Forms.NumericUpDown { Left = 355, Top = 506, Width = 110, Minimum = -10000, Maximum = 10000 };
            form.Controls.AddRange([target, mode, offsetX, offsetY,
                new Forms.Label { Text = "対象フレーム配置案", Left = 16, Top = 437, Width = 155 },
                new Forms.Label { Text = "取込み方法", Left = 16, Top = 473, Width = 155 },
                new Forms.Label { Text = "配置の移動量 X", Left = 16, Top = 509, Width = 155 },
                new Forms.Label { Text = "Y", Left = 320, Top = 509, Width = 30 },
                new Forms.Label { Text = "ジャンルコード表名・申込定義の読込み値は、一覧の名前を編集できます。", Left = 16, Top = 545, Width = 700 }]);
            void UpdateImportTargets()
            {
                var ids = rows.Rows.Cast<Forms.DataGridViewRow>().Where(row => Equals(row.Cells[0].Value, true))
                    .Select(row => (string)row.Cells[3].Value!).ToHashSet();
                var layouts = package.Items.Any(item => ids.Contains(item.Id));
                var definitions = package.Materials.Any(item => ids.Contains(item.Id) && item.Kind is "frame-definition" or "request-definition");
                mode.Enabled = layouts || definitions;
                if (!mode.Enabled) mode.SelectedIndex = 0;
                target.Enabled = definitions && mode.SelectedIndex != 3 || layouts && mode.SelectedIndex is 1 or 2;
                offsetX.Enabled = offsetY.Enabled = layouts && mode.SelectedIndex is 1 or 2;
                if (!target.Enabled) target.SelectedIndex = -1;
                else if (target.SelectedIndex < 0) target.SelectedIndex = Array.FindIndex(targets, layout => layout.Id == owner.SelectedDeskLayoutId);
            }
            rows.CellValueChanged += (_, _) => UpdateImportTargets();
            mode.SelectedIndexChanged += (_, _) => UpdateImportTargets();
            UpdateImportTargets();
            if (genreTableOnly)
            {
                foreach (Forms.Control control in form.Controls)
                    if (control.Top >= 434 && control.Top < 545) control.Visible = false;
                form.Controls.Add(new Forms.Label { Text = $"保存先：イベントプロジェクト「{owner.Project.Name}」直下\n既存のジャンルコード表全体を置き換えます。配置案は変更しません。",
                    Left = 16, Top = 434, Width = 700, Height = 64 });
            }
            rows.SelectionChanged += (_, _) =>
            {
                if (rows.CurrentRow?.Cells[3].Value is not string id) return;
                var material = package.Materials.FirstOrDefault(value => value.Id == id);
                if (material is not null)
                {
                    details.Text = $"{material.Name}\r\n{material.Credits?.AttributionText ?? "変更者・日付不明"}\r\n{material.Credits?.ChangeLog ?? "チェンジログ未記録"}";
                    return;
                }
                var item = package.Knowledge.FirstOrDefault(knowledge => "knowledge:" + knowledge.Id == id);
                if (item is null) return;
                details.Text = $"{(package.IsConfidential ? "【マル秘】" : "")}知見：{item.Name}（未対応付けで保存）\r\n{item.Description}\r\n狙い：{item.Purpose}\r\n推奨列：{item.InputRule.RecommendedColumn}／{item.InputRule.Meaning}\r\n{item.InputRule.ValueMeanings}／空欄：{(item.InputRule.BlankIsZero ? "0" : "エラー")}\r\n係数：{item.Scale}, {item.Offset}, {item.OverallWeight}／既定重み：{item.DefaultWeight}\r\n{(item.Venue is null ? "ひな形（座標なし）" : $"会場：{item.Venue.Name}／重み {item.Cells.Length} セル。列対応時に会場を検証します。")}";
            };
            var status = PortableStatus(form);
            var accept = PortableButtons(form, rows, "取込みを確認");
            var catalogApplied = false;
            accept.Click += (_, _) =>
            {
                try
                {
                    rows.EndEdit();
                    var selection = rows.Rows.Cast<Forms.DataGridViewRow>().Where(row => Equals(row.Cells[0].Value, true))
                        .Select(row =>
                        {
                            var id = (string)row.Cells[3].Value!;
                            var name = Convert.ToString(row.Cells[1].Value)?.Trim() ?? "";
                            var isLayout = package.Items.Any(item => item.Id == id);
                            var needsTarget = isLayout && mode.SelectedIndex is 1 or 2 ||
                                package.Materials.Any(item => item.Id == id && item.Kind is "frame-definition" or "request-definition");
                            return new PortableImportItem(id, "portable-" + Guid.NewGuid().ToString("N"), name)
                            {
                                TargetLayoutId = !needsTarget || target.SelectedIndex < 0 ? null : targets[target.SelectedIndex].Id,
                                Mode = isLayout && mode.SelectedIndex is 1 or 2 ? mode.SelectedIndex == 1 ? "insert" : "replace" : "add",
                                OffsetX = isLayout ? (int)offsetX.Value : 0, OffsetY = isLayout ? (int)offsetY.Value : 0,
                                RequestValue = package.Materials.Any(item => item.Id == id && item.Kind == "request-definition") ? name : null,
                                FallbackDefinitions = needsTarget && target.SelectedIndex >= 0 && targets[target.SelectedIndex].Definitions is null ? SpaceDefinitions.Current : null,
                            };
                        }).ToArray();
                    if (owner.Revision != revision) throw new InvalidOperationException("イベントが変更されました。閉じて、もう一度読み込んでください。");
                    if (mode.SelectedIndex == 3)
                    {
                        var catalog = EditorConnection.Current.PreviewPortableCatalog(new(SpaceDefinitions.Current, package, selection));
                        var selectedPackage = package with { Items = [], Knowledge = [], Materials = package.Materials.Where(item => selection.Any(selected => selected.ItemId == item.Id)).ToArray() };
                        if (!ShowPortableReview(form, selectedPackage, $"共通カタログ：{SpaceDefinitions.Path}\n型 {SpaceDefinitions.Current.Types.Count} → {catalog.Types.Count} 件\n申込定義 {SpaceDefinitions.Current.Requests.Count} → {catalog.Requests.Count} 件\nイベントは変更しません。取消しは［ライブラリー］の［共通登録を戻す］です（起動中の直前登録）。", "共通カタログ登録")) return;
                        SpaceDefinitions.ImportPortable(catalog);
                        catalogApplied = true;
                        form.DialogResult = Forms.DialogResult.OK;
                        return;
                    }
                    var preview = EditorConnection.Current.PreviewPortable(new(owner.Project, package, selection));
                    var summary = string.Join("\n", selection.Select(item => "・" + item.Name));
                    var reviewPackage = package with
                    {
                        Items = package.Items.Where(item => selection.Any(selected => selected.ItemId == item.Id)).ToArray(),
                        Materials = package.Materials.Where(item => selection.Any(selected => selected.ItemId == item.Id)).ToArray(),
                        Knowledge = package.Knowledge.Where(item => selection.Any(selected => selected.ItemId == "knowledge:" + item.Id)).ToArray(),
                    };
                    if (!ShowPortableReview(form, reviewPackage, $"{summary}\n対象：配置 {preview.LayoutCount}、知見 {preview.KnowledgeCount}、素材 {preview.MaterialCount} 件\n追加する型 {preview.TypeCount} 件\n会場：{preview.VenueAction}（{preview.VenueName}）\n{string.Join("\n", preview.Changes)}\n衝突検査済み。１回のUndoで戻せます。", "適用内容の確認", preview.CandidatePreview, owner.Project)) return;
                    if (owner.Revision != revision) throw new InvalidOperationException("イベントが変更されました。再確認が必要です。");
                    owner.Execute(new ImportPortableSelection(package, selection), selectedPlanEdit: false);
                    var layoutSelection = selection.FirstOrDefault(item => package.Items.Any(source => source.Id == item.ItemId));
                    if (layoutSelection is not null) owner.SelectDeskLayout(layoutSelection.Mode == "add" ? layoutSelection.NewId : layoutSelection.TargetLayoutId!);
                    form.DialogResult = Forms.DialogResult.OK;
                }
                catch (Exception ex) { status.Text = ex.Message; }
            };
            if (ShowEditorDialog(form) == Forms.DialogResult.OK)
            {
                CancelInProgressPointerInteraction();
                planScroll = 0;
                CreateToolbar();
                ShowInAppMessage("部分読込みが完了しました", catalogApplied ? "共通カタログに保存しました。直前登録はライブラリー画面から戻せます。" : "選択した項目を適用しました。イベントを保存するとファイルに残ります。");
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
        grid.Columns.Add(new Forms.DataGridViewTextBoxColumn
        {
            HeaderText = "種類", ReadOnly = true, DisplayIndex = 1,
            AutoSizeMode = Forms.DataGridViewAutoSizeColumnMode.AllCells,
        });
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
        var label = new Forms.Label { Left = 16, Top = form.ClientSize.Height - 122, Width = 700, Height = 65, ForeColor = System.Drawing.Color.DarkRed };
        form.Controls.Add(label);
        return label;
    }

    private static Forms.Button PortableButtons(Forms.Form form, Forms.DataGridView grid, string caption)
    {
        var all = new Forms.Button { Text = "すべて選択", Left = 16, Top = 10, Width = 100 };
        var none = new Forms.Button { Text = "選択解除", Left = 125, Top = 10, Width = 100 };
        var count = new Forms.Label { Left = 245, Top = 15, Width = 115 };
        var filter = new Forms.ComboBox { Left = 365, Top = 10, Width = 150, DropDownStyle = Forms.ComboBoxStyle.DropDownList };
        filter.Items.Add("すべての種類");
        filter.Items.AddRange(grid.Rows.Cast<Forms.DataGridViewRow>().Select(row => row.Cells[4].Value).OfType<string>().Distinct().Cast<object>().ToArray());
        filter.SelectedIndex = 0;
        filter.SelectedIndexChanged += (_, _) =>
        {
            grid.CurrentCell = null;
            foreach (Forms.DataGridViewRow row in grid.Rows) row.Visible = filter.SelectedIndex == 0 || Equals(row.Cells[4].Value, filter.SelectedItem);
        };
        var accept = new Forms.Button { Text = caption, Left = 440, Top = form.ClientSize.Height - 42, Width = 130 };
        var cancel = new Forms.Button { Text = "キャンセル", Left = 580, Top = form.ClientSize.Height - 42, Width = 135, DialogResult = Forms.DialogResult.Cancel };
        void Update()
        {
            var selected = grid.Rows.Cast<Forms.DataGridViewRow>().Count(row => Equals(row.Cells[0].Value, true));
            count.Text = $"{selected} / {grid.Rows.Count} 項目を選択";
            accept.Enabled = selected > 0;
        }
        all.Text = "表示分を選択";
        all.Click += (_, _) => { foreach (Forms.DataGridViewRow row in grid.Rows) if (row.Visible) row.Cells[0].Value = true; Update(); };
        none.Click += (_, _) => { foreach (Forms.DataGridViewRow row in grid.Rows) row.Cells[0].Value = false; Update(); };
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(Forms.DataGridViewDataErrorContexts.Commit); };
        grid.CellValueChanged += (_, _) => Update();
        form.Controls.AddRange([all, none, count, filter, accept, cancel]);
        form.CancelButton = cancel;
        Update();
        return accept;
    }

    private static string MaterialKindName(string kind) => kind switch
    {
        "venue" => "会場", "frame-definition" => "フレーム定義", "request-definition" => "申込定義",
        "genre-styles" => "ジャンルコード表", "block-styles" => "ブロック色対応表",
        _ => kind,
    };
}
