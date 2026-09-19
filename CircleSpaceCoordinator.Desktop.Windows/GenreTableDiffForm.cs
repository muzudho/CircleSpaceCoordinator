namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Core.Model;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

internal sealed class GenreTableDiffForm : Forms.Form
{
    private readonly GenreTableDiffDraft draft;
    private readonly Forms.DataGridView left = Grid();
    private readonly Forms.DataGridView right = Grid();
    private readonly Forms.TextBox leftRename = new() { Dock = Forms.DockStyle.Fill };
    private readonly Forms.TextBox rightRename = new() { Dock = Forms.DockStyle.Fill };
    private readonly Forms.TextBox log = new() { Dock = Forms.DockStyle.Fill, PlaceholderText = "変更内容（イベントへの反映／パッケージ保存時に必須）" };
    private readonly Forms.Label status = new() { Dock = Forms.DockStyle.Fill, AutoEllipsis = true, ForeColor = Drawing.Color.DarkRed };
    private readonly Forms.Label leftTitle = new() { Dock = Forms.DockStyle.Fill, AutoEllipsis = true };
    private readonly Forms.Label rightTitle = new() { Dock = Forms.DockStyle.Fill, AutoEllipsis = true };
    private readonly Forms.TextBox leftDetail = Detail();
    private readonly Forms.TextBox rightDetail = Detail();
    private readonly List<(Forms.Button Button, Func<bool> Enabled)> actions = [];
    private string? selectedCode;
    private bool refreshing;

    public GenreTableDiffForm(GenreTableDiffDraft draft, string projectName, string handle,
        Action<PortableMaterial, string> applyLeft, Func<PortableMaterial, string, bool, bool> saveRight, bool hasSourcePath)
    {
        this.draft = draft;
        Text = "ジャンルコード表の比較・交換";
        ClientSize = new(1240, 760); MinimumSize = new(1000, 680);
        StartPosition = Forms.FormStartPosition.CenterParent;
        AutoScaleMode = Forms.AutoScaleMode.Dpi;
        var panel = new Forms.TableLayoutPanel { Dock = Forms.DockStyle.Fill, ColumnCount = 2, RowCount = 9, Padding = new(12) };
        panel.ColumnStyles.Add(new(Forms.SizeType.Percent, 50)); panel.ColumnStyles.Add(new(Forms.SizeType.Percent, 50));
        foreach (var height in new[] { 46, 42, -1, 40, 34, 58, 34, 44, 40 })
            panel.RowStyles.Add(height < 0 ? new(Forms.SizeType.Percent, 100) : new(Forms.SizeType.Absolute, height));
        var help = new Forms.Label { Dock = Forms.DockStyle.Fill,
            Text = $"左：現在のイベント ／ 右：パッケージ　　作業者：{handle}\n文字コード順。同じコードを同じ行に表示。改名・削除は表だけに適用し、サークルデータは変更しません。" };
        panel.Controls.Add(help, 0, 0); panel.SetColumnSpan(help, 2);
        panel.Controls.Add(leftTitle, 0, 1); panel.Controls.Add(rightTitle, 1, 1);
        panel.Controls.Add(left, 0, 2); panel.Controls.Add(right, 1, 2);
        var leftActions = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Fill, WrapContents = false };
        var rightActions = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Fill, WrapContents = false };
        panel.Controls.Add(leftActions, 0, 3); panel.Controls.Add(rightActions, 1, 3);
        bool Exists(GenreTableSide side) => selectedCode is not null && draft.Contains(side, selectedCode);
        void Button(Forms.FlowLayoutPanel parent, string label, Action action, Func<bool> enabled, int width = 135)
        {
            var button = new Forms.Button { Text = label, Width = width, Height = 32, AutoEllipsis = true };
            button.Click += (_, _) => Run(action);
            parent.Controls.Add(button); actions.Add((button, enabled));
        }
        Button(leftActions, "← 左にコピーを挿入", () => draft.CopyTo(GenreTableSide.Left, selectedCode!, false), () => Exists(GenreTableSide.Right) && !Exists(GenreTableSide.Left), 145);
        Button(leftActions, "← 左を上書き", () => draft.CopyTo(GenreTableSide.Left, selectedCode!, true), () => Exists(GenreTableSide.Right) && Exists(GenreTableSide.Left), 120);
        Button(leftActions, "左を削除", () => draft.Delete(GenreTableSide.Left, selectedCode!), () => Exists(GenreTableSide.Left), 95);
        Button(rightActions, "右にコピーを挿入 →", () => draft.CopyTo(GenreTableSide.Right, selectedCode!, false), () => Exists(GenreTableSide.Left) && !Exists(GenreTableSide.Right), 145);
        Button(rightActions, "右を上書き →", () => draft.CopyTo(GenreTableSide.Right, selectedCode!, true), () => Exists(GenreTableSide.Right) && Exists(GenreTableSide.Left), 120);
        Button(rightActions, "右を削除", () => draft.Delete(GenreTableSide.Right, selectedCode!), () => Exists(GenreTableSide.Right), 95);
        void RenameRow(GenreTableSide side, Forms.TextBox input)
        {
            draft.Rename(side, selectedCode!, input.Text);
            selectedCode = input.Text.Trim();
        }
        Forms.Control RenameArea(GenreTableSide side, Forms.TextBox input)
        {
            var area = new Forms.TableLayoutPanel { Dock = Forms.DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            area.ColumnStyles.Add(new(Forms.SizeType.Percent, 100)); area.ColumnStyles.Add(new(Forms.SizeType.Absolute, 160));
            input.PlaceholderText = "選択したジャンルコードの新しい名前";
            var button = new Forms.Button { Text = side == GenreTableSide.Left ? "左のコードをリネーム" : "右のコードをリネーム", Dock = Forms.DockStyle.Fill };
            button.Click += (_, _) => Run(() => RenameRow(side, input));
            actions.Add((button, () => Exists(side)));
            area.Controls.Add(input, 0, 0); area.Controls.Add(button, 1, 0);
            return area;
        }
        panel.Controls.Add(RenameArea(GenreTableSide.Left, leftRename), 0, 4);
        panel.Controls.Add(RenameArea(GenreTableSide.Right, rightRename), 1, 4);
        panel.Controls.Add(leftDetail, 0, 5); panel.Controls.Add(rightDetail, 1, 5);
        panel.Controls.Add(log, 0, 6); panel.SetColumnSpan(log, 2);
        var savesLeft = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Fill, WrapContents = false };
        var savesRight = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Fill, WrapContents = false };
        panel.Controls.Add(savesLeft, 0, 7); panel.Controls.Add(savesRight, 1, 7);
        Button(savesLeft, "左をイベントへ反映", () =>
        {
            applyLeft(draft.Left, PersonCredits.NormalizeChangeLog(log.Text));
            draft.MarkSaved(GenreTableSide.Left);
            status.Text = "左側をイベントへ反映しました。";
        }, () => draft.LeftChanged, 160);
        Button(savesLeft, "元に戻す", draft.Undo, () => draft.CanUndo, 90);
        Button(savesLeft, "やり直す", draft.Redo, () => draft.CanRedo, 90);
        Button(savesRight, "右をパッケージへ保存", () => SaveRight(false), () => draft.RightChanged && hasSourcePath, 160);
        Button(savesRight, "右を別名保存…", () => SaveRight(true), () => true, 135);
        Button(savesRight, "閉じる", Close, () => true, 80);
        void SaveRight(bool saveAs)
        {
            var description = PersonCredits.NormalizeChangeLog(log.Text);
            if (!saveRight(draft.Right, description, saveAs)) return;
            hasSourcePath = true;
            draft.MarkSaved(GenreTableSide.Right);
            status.Text = "右側を含むパッケージを保存しました。";
        }
        panel.Controls.Add(status, 0, 8); panel.SetColumnSpan(status, 2);
        Controls.Add(panel);
        left.CurrentCellChanged += (_, _) => SelectFrom(left, right);
        right.CurrentCellChanged += (_, _) => SelectFrom(right, left);
        left.Scroll += (_, e) => SyncScroll(left, right, e);
        right.Scroll += (_, e) => SyncScroll(right, left, e);
        FormClosing += (_, e) =>
        {
            if ((draft.LeftChanged || draft.RightChanged) && Forms.MessageBox.Show(this,
                $"{(draft.LeftChanged ? "左側に未反映の変更があります。\n" : "")}{(draft.RightChanged ? "右側に未保存の変更があります。\n" : "")}これらを破棄して閉じますか？",
                "比較編集の終了", Forms.MessageBoxButtons.OKCancel, Forms.MessageBoxIcon.Question) != Forms.DialogResult.OK) e.Cancel = true;
        };
        RefreshRows();

        void Run(Action action)
        {
            try { status.Text = ""; action(); RefreshRows(); }
            catch (Exception ex) { status.Text = ex.Message; }
        }
        void RefreshRows()
        {
            refreshing = true;
            var scroll = Math.Max(0, left.FirstDisplayedScrollingRowIndex);
            left.Rows.Clear(); right.Rows.Clear();
            var rows = draft.Rows;
            foreach (var row in rows) { AddRow(left, row, row.Left); AddRow(right, row, row.Right); }
            var index = rows.ToList().FindIndex(row => row.Code == selectedCode);
            if (index < 0) index = 0;
            if (rows.Count > 0)
            {
                left.CurrentCell = left.Rows[index].Cells[0]; right.CurrentCell = right.Rows[index].Cells[0];
                left.FirstDisplayedScrollingRowIndex = right.FirstDisplayedScrollingRowIndex = Math.Min(scroll, rows.Count - 1);
                selectedCode = rows[index].Code;
            }
            else selectedCode = null;
            refreshing = false;
            leftTitle.Text = $"現在の表：{draft.Left.Name}{(draft.LeftChanged ? " ＊未反映" : "")}\nイベント：{projectName}{(draft.Left.IsConfidential ? " 【マル秘】" : "")}";
            rightTitle.Text = $"パッケージの表：{draft.Right.Name}{(draft.RightChanged ? " ＊未保存" : "")}\n右側の変更はパッケージへ保存できます。{(draft.Right.IsConfidential ? " 【マル秘】" : "")}";
            RefreshSelection();
        }
        void RefreshSelection()
        {
            leftRename.Text = rightRename.Text = selectedCode ?? "";
            var row = draft.Rows.FirstOrDefault(row => row.Code == selectedCode);
            leftDetail.Text = Description(row?.Left); rightDetail.Text = Description(row?.Right);
            foreach (var (button, enabled) in actions) button.Enabled = enabled();
        }
        void SelectFrom(Forms.DataGridView from, Forms.DataGridView to)
        {
            if (refreshing || from.CurrentRow?.Tag is not string code) return;
            refreshing = true;
            selectedCode = code;
            to.CurrentCell = to.Rows[from.CurrentRow.Index].Cells[0];
            refreshing = false;
            RefreshSelection();
        }
        void SyncScroll(Forms.DataGridView from, Forms.DataGridView to, Forms.ScrollEventArgs e)
        {
            if (refreshing || e.ScrollOrientation != Forms.ScrollOrientation.VerticalScroll || from.FirstDisplayedScrollingRowIndex < 0) return;
            refreshing = true; to.FirstDisplayedScrollingRowIndex = from.FirstDisplayedScrollingRowIndex; refreshing = false;
        }
    }

    private static Forms.TextBox Detail() => new() { Dock = Forms.DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = Forms.ScrollBars.Vertical };
    private static string ColorName(string value) => StyleMappingDraft.Colors.FirstOrDefault(item => item.Id == value).Label ?? value;
    private static string PatternName(string value) => StyleMappingDraft.Patterns.FirstOrDefault(item => item.Id == StyleMappingDraft.NormalizePattern(value)).Label ?? value;
    private static string Description(GenreStyleDefinition? row) => row is null ? "この側にはありません。" :
        $"{row.GenreId}　太線色：{ColorName(row.PrimaryColor)}　細線色：{ColorName(row.SecondaryColor)}　網掛け：{PatternName(row.Pattern)}\r\n{row.KnowledgeComment ?? "（コメントなし）"}";
    private static Forms.DataGridView Grid()
    {
        var grid = new Forms.DataGridView { Dock = Forms.DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            AllowUserToDeleteRows = false, RowHeadersVisible = false, MultiSelect = false,
            SelectionMode = Forms.DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = Forms.DataGridViewAutoSizeColumnsMode.Fill };
        foreach (var (label, weight) in new[] { ("ジャンルコード", 130), ("差分", 48), ("太線色", 65), ("細線色", 65), ("網掛け", 85), ("コメント", 120) })
            grid.Columns.Add(new Forms.DataGridViewTextBoxColumn { HeaderText = label, FillWeight = weight, SortMode = Forms.DataGridViewColumnSortMode.NotSortable });
        return grid;
    }
    private static void AddRow(Forms.DataGridView grid, GenreTableDiffRow diff, GenreStyleDefinition? row)
    {
        var index = grid.Rows.Add(diff.Code, row is null ? "—" : diff.Difference, row is null ? "" : ColorName(row.PrimaryColor), row is null ? "" : ColorName(row.SecondaryColor), row is null ? "" : PatternName(row.Pattern), row?.KnowledgeComment ?? "");
        var item = grid.Rows[index]; item.Tag = diff.Code;
        item.DefaultCellStyle.BackColor = row is null ? Drawing.Color.Gainsboro : diff.Difference == "一致" ? Drawing.Color.White : Drawing.Color.LightYellow;
        if (row is null) item.DefaultCellStyle.ForeColor = Drawing.Color.Gray;
        if (diff.Left is not null && diff.Right is not null)
        {
            if (diff.Left.PrimaryColor != diff.Right.PrimaryColor) item.Cells[2].Style.BackColor = Drawing.Color.MistyRose;
            if (diff.Left.SecondaryColor != diff.Right.SecondaryColor) item.Cells[3].Style.BackColor = Drawing.Color.MistyRose;
            if (diff.Left.Pattern != diff.Right.Pattern) item.Cells[4].Style.BackColor = Drawing.Color.MistyRose;
            if (diff.Left.KnowledgeComment != diff.Right.KnowledgeComment) item.Cells[5].Style.BackColor = Drawing.Color.MistyRose;
        }
    }
}
