namespace CircleSpaceCoordinator.Desktop.Windows;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

public sealed partial class VenueEditorGame
{
    private string? portableLibraryDirectory;

    private void ShowPortableLibrary()
    {
        try
        {
            using var form = new Forms.Form { Text = "パッケージ・ライブラリー", ClientSize = new(960, 700),
                FormBorderStyle = Forms.FormBorderStyle.FixedDialog, MaximizeBox = false,
                StartPosition = Forms.FormStartPosition.CenterScreen, AutoScaleMode = Forms.AutoScaleMode.Dpi };
            var folder = new Forms.TextBox { Left = 16, Top = 16, Width = 780, ReadOnly = true,
                Text = portableLibraryDirectory ?? Path.Combine(AppContext.BaseDirectory, "portable-library") };
            var browse = new Forms.Button { Left = 812, Top = 14, Width = 130, Text = "フォルダー選択" };
            var search = new Forms.TextBox { Left = 16, Top = 52, Width = 660, PlaceholderText = "タイトル・メモ・タグ・ファイル名を検索" };
            var refresh = new Forms.Button { Left = 688, Top = 50, Width = 110, Text = "更新" };
            var undo = new Forms.Button { Left = 810, Top = 50, Width = 132, Text = "共通登録を戻す", Enabled = SpaceDefinitions.CanUndoPortableImport };
            var grid = new Forms.DataGridView { Left = 16, Top = 90, Width = 926, Height = 255,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true, RowHeadersVisible = false,
                SelectionMode = Forms.DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
                AutoSizeColumnsMode = Forms.DataGridViewAutoSizeColumnsMode.Fill };
            foreach (var header in new[] { "タイトル", "メモ", "タグ", "マル秘／状態", "ファイル" }) grid.Columns.Add(header, header);
            var preview = new Forms.Panel { Left = 16, Top = 358, Width = 450, Height = 270, BackColor = Drawing.Color.White };
            var detail = new Forms.TextBox { Left = 480, Top = 358, Width = 462, Height = 270, ReadOnly = true, Multiline = true, ScrollBars = Forms.ScrollBars.Vertical };
            var status = new Forms.Label { Left = 16, Top = 642, Width = 550, Height = 45, Text = "選択したフォルダー直下の先頭200ファイルまで表示します。" };
            var edit = new Forms.Button { Left = 578, Top = 652, Width = 110, Text = "メモ・タグ編集" };
            var import = new Forms.Button { Left = 700, Top = 652, Width = 110, Text = "項目を選んで読込" };
            var close = new Forms.Button { Left = 822, Top = 652, Width = 120, Text = "閉じる", DialogResult = Forms.DialogResult.Cancel };
            IReadOnlyList<PortableLibraryEntry> entries = [];
            PortableLibraryEntry? Selected() => grid.CurrentRow?.Tag as PortableLibraryEntry;
            void Filter()
            {
                grid.Rows.Clear();
                foreach (var entry in entries.Where(entry => entry.Matches(search.Text)))
                {
                    var package = entry.Package;
                    var row = grid.Rows[grid.Rows.Add(package?.Name ?? "読込みエラー", package?.Description ?? "",
                        package is null ? "" : string.Join(", ", package.Tags), package is null ? entry.Error ?? "読込みエラー" : package.IsConfidential ? "マル秘" : "通常",
                        Path.GetFileName(entry.Path))];
                    row.Tag = entry;
                }
                UpdateSelection();
            }
            void UpdateSelection()
            {
                var entry = Selected();
                edit.Enabled = import.Enabled = entry?.Package is not null;
                detail.Text = entry is null ? "" : entry.Package is { } package
                    ? $"{(package.IsConfidential ? "【マル秘】\r\n" : "")}{package.Name}\r\n{package.Description}\r\n{string.Join(", ", package.Tags)}\r\n配置 {package.Items.Count}、知見 {package.Knowledge.Count}、素材 {package.Materials.Count} 件\r\n{entry.Path}"
                    : entry.Error;
                preview.Invalidate();
            }
            void Reload()
            {
                try { entries = PortableLibraryService.Read(folder.Text, EditorConnection.Current.ParsePortable); Filter(); }
                catch (Exception ex) { status.Text = ex.Message; }
            }
            browse.Click += (_, _) =>
            {
                using var dialog = new Forms.FolderBrowserDialog { Description = "パッケージの保存フォルダー", SelectedPath = folder.Text };
                if (dialog.ShowDialog(form) != Forms.DialogResult.OK) return;
                portableLibraryDirectory = folder.Text = dialog.SelectedPath;
                Reload();
            };
            refresh.Click += (_, _) => Reload();
            search.TextChanged += (_, _) => Filter();
            grid.SelectionChanged += (_, _) => UpdateSelection();
            preview.Paint += (_, args) => DrawPortableThumbnail(args.Graphics, preview.ClientRectangle, Selected()?.Package);
            undo.Click += (_, _) =>
            {
                try
                {
                    if (Forms.MessageBox.Show(form, "起動中の直前の共通カタログ登録を戻します。イベントのUndoとは別の操作です。", "共通登録の取消し", Forms.MessageBoxButtons.OKCancel) != Forms.DialogResult.OK) return;
                    SpaceDefinitions.UndoPortableImport();
                    undo.Enabled = SpaceDefinitions.CanUndoPortableImport;
                    status.Text = "共通カタログ登録を戻しました。";
                }
                catch (Exception ex) { status.Text = ex.Message; }
            };
            edit.Click += (_, _) =>
            {
                if (Selected() is not { Package: { } package } entry) return;
                using var editor = PortableForm("ファイル全体のメモ・タグを編集");
                var name = PortableText(editor, "パッケージのタイトル", 24, package.Name);
                var memo = PortableText(editor, "ファイル全体のメモ", 65, package.Description);
                memo.Multiline = true; memo.Height = 220; memo.ScrollBars = Forms.ScrollBars.Vertical;
                var tags = PortableText(editor, "タグ（カンマ区切り）", 310, string.Join(", ", package.Tags));
                var save = new Forms.Button { Left = 440, Top = 550, Width = 130, Text = "保存" };
                var cancel = new Forms.Button { Left = 580, Top = 550, Width = 135, Text = "キャンセル", DialogResult = Forms.DialogResult.Cancel };
                var error = PortableStatus(editor);
                save.Click += (_, _) =>
                {
                    try
                    {
                        var json = EditorConnection.Current.UpdatePortableMetadata(new(entry.Json, name.Text, memo.Text,
                            tags.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)));
                        PortableLibraryService.SaveMetadata(entry, json);
                        editor.DialogResult = Forms.DialogResult.OK;
                    }
                    catch (Exception ex) { error.Text = ex.Message; }
                };
                editor.Controls.AddRange([save, cancel]); editor.CancelButton = cancel;
                if (ShowEditorDialog(editor, form) == Forms.DialogResult.OK) Reload();
            };
            import.Click += (_, _) => { if (Selected() is { Package: not null } entry) { form.Close(); ImportPortable(entry.Json, sourcePath: entry.Path); } };
            form.Controls.AddRange([folder, browse, search, refresh, undo, grid, preview, detail, status, edit, import, close]);
            form.CancelButton = close;
            Reload(); ShowEditorDialog(form);
        }
        catch (Exception ex) { ShowInAppMessage("ライブラリーを開けません", ex.Message); }
        finally { modalInputDrain = true; }
    }

    private bool ShowPortableReview(Forms.Form owner, PortablePackage package, string summary, string title,
        CircleSpaceProject? candidate = null, CircleSpaceProject? previous = null)
    {
        using var form = new Forms.Form { Text = title, ClientSize = new(900, 650), StartPosition = Forms.FormStartPosition.CenterParent,
            AutoScaleMode = Forms.AutoScaleMode.Dpi, FormBorderStyle = Forms.FormBorderStyle.FixedDialog, MaximizeBox = false };
        var text = new Forms.TextBox { Left = 16, Top = 16, Width = 868, Height = 220, ReadOnly = true,
            Multiline = true, ScrollBars = Forms.ScrollBars.Vertical, Text = ((package.IsConfidential ? "【マル秘】取扱い・受渡し先を確認してください。\n" : "") + summary).Replace("\n", "\r\n") };
        var list = new Forms.ComboBox { Left = 16, Top = 250, Width = 868, DropDownStyle = Forms.ComboBoxStyle.DropDownList };
        var painters = new List<Action<Drawing.Graphics, Drawing.Rectangle>>();
        var layouts = candidate?.DeskLayouts.Select(item => item.Name).ToArray() ?? package.Items.Select(item => item.Name).ToArray();
        for (var i = 0; i < layouts.Length; i++)
        {
            var index = i;
            list.Items.Add(layouts[i]);
            painters.Add((graphics, bounds) =>
            {
                if (candidate is null) DrawPortableThumbnail(graphics, bounds, package, index);
                else DrawLayoutPreview(graphics, bounds, candidate, index, previous);
            });
        }
        foreach (var material in package.Materials)
        {
            list.Items.Add(material.Name);
            var one = package with { Items = [], Materials = [material], Knowledge = [] };
            painters.Add((graphics, bounds) => DrawPortableThumbnail(graphics, bounds, one));
        }
        foreach (var knowledge in package.Knowledge)
        {
            list.Items.Add(knowledge.Name);
            var one = package with { Items = [], Materials = [], Knowledge = [knowledge] };
            painters.Add((graphics, bounds) => DrawPortableThumbnail(graphics, bounds, one));
        }
        if (painters.Count == 0)
        {
            list.Items.Add("会場");
            painters.Add((graphics, bounds) => { if (candidate is not null) DrawLayoutPreview(graphics, bounds, candidate, 0); });
        }
        list.SelectedIndex = 0;
        var panel = new Forms.Panel { Left = 16, Top = 290, Width = 868, Height = 295, BackColor = Drawing.Color.White };
        panel.Paint += (_, args) =>
        {
            painters[Math.Max(0, list.SelectedIndex)](args.Graphics, panel.ClientRectangle);
        };
        list.SelectedIndexChanged += (_, _) => panel.Invalidate();
        var legend = new Forms.Label { Text = candidate is null ? "配置は会場の絶対座標で表示します。" : "青：適用後のフレーム／赤の輪郭：変更前のフレーム", Left = 16, Top = 600, Width = 590 };
        var accept = new Forms.Button { Text = "確定", Left = 650, Top = 603, Width = 110, DialogResult = Forms.DialogResult.OK };
        var cancel = new Forms.Button { Text = "キャンセル", Left = 774, Top = 603, Width = 110, DialogResult = Forms.DialogResult.Cancel };
        form.Controls.AddRange([text, list, panel, legend, accept, cancel]); form.CancelButton = cancel;
        return ShowEditorDialog(form, owner) == Forms.DialogResult.OK;
    }

    private static void DrawLayoutPreview(Drawing.Graphics graphics, Drawing.Rectangle bounds, CircleSpaceProject project, int index, CircleSpaceProject? previous = null)
    {
        var scale = Math.Min((bounds.Width - 24f) / Math.Max(1, project.Venue.Width), (bounds.Height - 24f) / Math.Max(1, project.Venue.Height));
        Drawing.RectangleF Cell(int x, int y) => new(12 + x * scale, 12 + y * scale, Math.Max(1, scale - 1), Math.Max(1, scale - 1));
        using var outline = new Drawing.Pen(Drawing.Color.Gray);
        graphics.DrawRectangle(outline, 12, 12, project.Venue.Width * scale, project.Venue.Height * scale);
        foreach (var cell in project.Venue.BlockedCells) graphics.FillRectangle(Drawing.Brushes.DimGray, Cell(cell.X, cell.Y));
        if (project.DeskLayouts.Count == 0) return;
        var layout = project.DeskLayouts[Math.Min(index, project.DeskLayouts.Count - 1)];
        var types = project.DeskTypes.ToDictionary(type => type.Id);
        foreach (var desk in layout.DeskPlacements)
            foreach (var cell in desk.GetOccupiedCells(types[desk.DeskTypeId])) graphics.FillRectangle(Drawing.Brushes.SteelBlue, Cell(cell.X, cell.Y));
        if (previous?.DeskLayouts.FirstOrDefault(item => item.Id == layout.Id) is { } old)
        {
            var oldTypes = previous.DeskTypes.ToDictionary(type => type.Id);
            using var pen = new Drawing.Pen(Drawing.Color.IndianRed, 2);
            foreach (var desk in old.DeskPlacements)
                foreach (var cell in desk.GetOccupiedCells(oldTypes[desk.DeskTypeId]))
                { var rectangle = Cell(cell.X, cell.Y); graphics.DrawRectangle(pen, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height); }
        }
    }

    private static void DrawPortableThumbnail(Drawing.Graphics graphics, Drawing.Rectangle bounds, PortablePackage? package, int index = 0)
    {
        if (package is null) return;
        if (package.Items.Count > 0) { DrawLayoutPreview(graphics, bounds, package.Items[Math.Min(index, package.Items.Count - 1)].Project, 0); return; }
        var material = package.Materials.FirstOrDefault();
        if (material?.Kind is "genre-styles" or "block-styles")
        {
            var rows = material.GenreStyles?.Select(s => $"{s.GenreId}: {s.PrimaryColor} / {s.SecondaryColor} / {s.Pattern}")
                ?? material.BlockStyles!.Select(s => $"{s.BlockNumber}: {s.PrimaryColor} / {s.SecondaryColor} / {s.Pattern}");
            using var font = new Drawing.Font("Meiryo", 10);
            graphics.DrawString($"{material.Name}\n{material.Credits?.AttributionText ?? "変更者・日付不明"}\n{material.Credits?.ChangeLog ?? "チェンジログ未記録"}\n\n{string.Join("\n", rows.Take(8))}",
                font, Drawing.Brushes.Black, new Drawing.RectangleF(12, 12, Math.Max(1, bounds.Width - 24), Math.Max(1, bounds.Height - 24)));
            return;
        }
        var definition = material?.Definitions?.Types.FirstOrDefault();
        if (definition is not null)
        {
            var scale = Math.Min((bounds.Width - 24f) / definition.Width, (bounds.Height - 24f) / definition.Height);
            foreach (var cell in definition.Cells) graphics.FillRectangle(cell.Area > 0 ? Drawing.Brushes.SteelBlue : Drawing.Brushes.LightGray,
                12 + cell.X * scale, 12 + cell.Y * scale, Math.Max(1, scale - 1), Math.Max(1, scale - 1));
            return;
        }
        var venue = material?.Venue ?? package.Knowledge.FirstOrDefault()?.Venue;
        if (venue is null) return;
        var preview = new CircleSpaceProject("1.0", "preview", "preview", new("venue", venue.Name, venue.Width, venue.Height, venue.BlockedCells.ToHashSet()), [], [], new([], []), []);
        DrawLayoutPreview(graphics, bounds, preview, 0);
        var cellScale = Math.Min((bounds.Width - 24f) / venue.Width, (bounds.Height - 24f) / venue.Height);
        foreach (var cell in package.Knowledge.FirstOrDefault()?.Cells ?? [])
            graphics.FillRectangle(cell.Weight < 0 ? Drawing.Brushes.Orange : Drawing.Brushes.LightSkyBlue,
                12 + cell.X * cellScale, 12 + cell.Y * cellScale, Math.Max(1, cellScale - 1), Math.Max(1, cellScale - 1));
    }
}
