namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Persistence;
using System.Drawing;
using System.Windows.Forms;

internal static class SpaceDefinitionDialog
{
    private static Form CreateForm(string title) => new()
    {
        Text = title, ClientSize = new Size(850, 620), StartPosition = FormStartPosition.CenterParent,
        FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
        ShowInTaskbar = false, Font = new Font("Yu Gothic UI", 10), AutoScaleMode = AutoScaleMode.Dpi,
    };

    private static void Label(Form form, string text, int x, int y, int width = 130) =>
        form.Controls.Add(new Label { Text = text, Left = x, Top = y, Width = width, Height = text.Contains('\n') ? 100 : 28 });

    private static ComboBox Combo(Form form, string[] choices, string value, int x, int y, int width = 130)
    {
        var combo = new ComboBox { Left = x, Top = y, Width = width, DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(choices);
        combo.SelectedItem = value;
        form.Controls.Add(combo);
        return combo;
    }

    private static void Buttons(Form form, Action save)
    {
        var ok = new Button { Text = "保存", Left = 620, Top = 570, Width = 100, Height = 34 };
        var cancel = new Button { Text = "キャンセル", Left = 730, Top = 570, Width = 105, Height = 34, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) =>
        {
            try { save(); form.DialogResult = DialogResult.OK; }
            catch (Exception ex) { MessageBox.Show(form, ex.Message, "定義を確認してください", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        form.Controls.AddRange([ok, cancel]);
        form.CancelButton = cancel;
    }

    public static void EditType(IWin32Window owner, SpaceTypeDefinition source, Action<SpaceTypeDefinition> save)
    {
        using var form = CreateForm("配置物の型を編集（アプリ共通）");
        Label(form, "名前", 16, 17);
        var name = new TextBox { Left = 80, Top = 14, Width = 500, Text = source.Name, MaxLength = 100 };
        form.Controls.Add(name);
        var kind = Combo(form, ["机", "場所", "ブース"], source.Kind, 600, 14, 230);
        Label(form, "幅", 16, 60, 40);
        Label(form, "高さ", 160, 60, 50);
        var width = new NumericUpDown { Left = 60, Top = 56, Width = 80, Minimum = 1, Maximum = 12, Value = source.Width };
        var height = new NumericUpDown { Left = 215, Top = 56, Width = 80, Minimum = 1, Maximum = 12, Value = source.Height };
        form.Controls.AddRange([width, height]);
        Label(form, "クリックで塗る内容", 320, 60, 160);
        var brush = Combo(form, ["範囲外（消す）", "席ではない部分", "区画1", "区画2", "区画3", "区画4", "区画5", "区画6", "区画7", "区画8", "区画9"], "区画1", 490, 56, 340);
        Label(form, "同じ番号のセルをまとめて1サークル用の区画にします。右クリックで範囲外に戻します。", 16, 98, 815);
        Label(form, "寸法を小さくすると、保存時に範囲外のセルを取り除きます。", 16, 126, 815);
        var cells = source.Cells.ToDictionary(c => (c.X, c.Y), c => c.Area);
        using var grid = new PreviewPanel { Left = 16, Top = 165, Width = 525, Height = 380, BackColor = Color.FromArgb(26, 31, 40) };
        form.Controls.Add(grid);
        int CellSize() => Math.Min(48, Math.Min((grid.Width - 12) / (int)width.Value, (grid.Height - 12) / (int)height.Value));
        grid.Paint += (_, e) =>
        {
            var size = CellSize();
            for (var y = 0; y < height.Value; y++)
            for (var x = 0; x < width.Value; x++)
            {
                var rect = new Rectangle(6 + x * size, 6 + y * size, size - 2, size - 2);
                var occupied = cells.TryGetValue((x, y), out var area);
                using var fill = new SolidBrush(!occupied ? Color.FromArgb(40, 45, 53) : AreaColor(area));
                e.Graphics.FillRectangle(fill, rect);
                e.Graphics.DrawRectangle(Pens.Gray, rect);
                TextRenderer.DrawText(e.Graphics, !occupied ? "" : area == 0 ? "—" : area.ToString(), form.Font, rect, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        };
        grid.MouseDown += (_, e) =>
        {
            var x = (e.X - 6) / CellSize();
            var y = (e.Y - 6) / CellSize();
            if (e.X < 6 || e.Y < 6 || x >= width.Value || y >= height.Value) return;
            if (e.Button == MouseButtons.Right || brush.SelectedIndex == 0) cells.Remove((x, y));
            else cells[(x, y)] = brush.SelectedIndex - 1;
            grid.Invalidate();
        };
        width.ValueChanged += (_, _) => grid.Invalidate();
        height.ValueChanged += (_, _) => grid.Invalidate();
        string[] edgeNames = ["上辺", "右辺", "下辺", "左辺"];
        var edges = new ComboBox[4];
        for (var i = 0; i < 4; i++)
        {
            Label(form, edgeNames[i], 565, 170 + i * 65);
            edges[i] = Combo(form, ["開放", "壁", "入口", "正面"], source.Edges[i], 635, 165 + i * 65, 195);
        }
        Label(form, "灰色（—）：占有するが席ではない\n色＋番号：割当区画\n暗い空欄：占有しない", 560, 445, 270);
        Buttons(form, () => save(source with
        {
            Name = name.Text.Trim(), Kind = kind.Text, Width = (int)width.Value, Height = (int)height.Value,
            Cells = cells.Where(p => p.Key.X < width.Value && p.Key.Y < height.Value).Select(p => new SpaceCell(p.Key.X, p.Key.Y, p.Value)).ToArray(),
            Edges = edges.Select(e => e.Text).ToArray(),
        }));
        form.ShowDialog(owner);
    }

    public static void EditRequest(IWin32Window owner, SpaceRequestDefinition source, SpaceDefinitionCatalog catalog, Action<SpaceRequestDefinition> save)
    {
        using var form = CreateForm("申込スペースを編集（アプリ共通）");
        Label(form, "読込み値", 16, 20);
        var value = new TextBox { Left = 150, Top = 16, Width = 680, Text = source.Value, MaxLength = 100 };
        Label(form, "説明", 16, 65);
        var description = new TextBox { Left = 150, Top = 61, Width = 680, Text = source.Description, MaxLength = 200 };
        Label(form, "割当可能な型・区画にチェックを付けます。複数の型への振替も指定できます。", 16, 110, 810);
        using var targets = new CheckedListBox { Left = 16, Top = 150, Width = 814, Height = 395, CheckOnClick = true };
        var choices = catalog.Types.SelectMany(t => t.Cells.Where(c => c.Area > 0).Select(c => c.Area).Distinct().Order()
            .Select(a => (Target: new SpaceTarget(t.Id, a), Label: $"{t.Name} ／ 区画{a}（{t.Cells.Count(c => c.Area == a)}セル）"))).ToArray();
        foreach (var choice in choices) targets.Items.Add(choice.Label, source.Targets.Contains(choice.Target));
        form.Controls.AddRange([value, description, targets]);
        Buttons(form, () => save(source with
        {
            Value = value.Text.Trim(), Description = description.Text.Trim(),
            Targets = targets.CheckedIndices.Cast<int>().Select(i => choices[i].Target).ToArray(),
        }));
        form.ShowDialog(owner);
    }

    internal static Color AreaColor(int area) => area == 0 ? Color.FromArgb(100, 107, 114)
        : new[] { Color.Teal, Color.SteelBlue, Color.DarkGoldenrod, Color.IndianRed, Color.MediumPurple, Color.OliveDrab, Color.DarkCyan, Color.DarkOrange, Color.DeepPink }[(area - 1) % 9];

    private sealed class PreviewPanel : Panel
    {
        public PreviewPanel() { DoubleBuffered = true; }
    }
}
