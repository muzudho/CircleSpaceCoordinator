namespace CircleSpaceCoordinator.Desktop.Windows;

using System.Windows.Forms;

internal static class ChannelDialog
{
    public static (string Name, string? Column)? Show(string name, string? column, IReadOnlyList<string> columns)
    {
        using var form = new Form
        {
            Text = "チャンネルの名前・Excel列", Width = 480, Height = 250,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
            StartPosition = FormStartPosition.CenterScreen, ShowInTaskbar = false,
        };
        using var nameLabel = new Label { Text = "チャンネル名", Left = 16, Top = 14, Width = 430 };
        using var input = new TextBox { Text = name, Left = 16, Top = 36, Width = 430, MaxLength = 100 };
        using var columnLabel = new Label { Text = "取り込んだ Excel / CSV の列", Left = 16, Top = 70, Width = 430 };
        using var combo = new ComboBox { Left = 16, Top = 92, Width = 430, DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.Add("（対応なし：0点）");
        foreach (var item in columns) combo.Items.Add(item);
        combo.SelectedIndex = column is null ? 0 : Math.Max(0, columns.ToList().IndexOf(column) + 1);
        using var hint = new Label
        {
            Text = columns.Count == 0 ? "先に参加サークル一覧を取り込むと、列を選べます。" : "列の数値 × セルの重みを加点します。空欄は 0 です。",
            Left = 16, Top = 125, Width = 440, Height = 24,
        };
        using var ok = new Button { Text = "保存", Left = 280, Top = 165, Width = 80 };
        using var cancel = new Button { Text = "キャンセル", Left = 366, Top = 165, Width = 80, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(input.Text) || input.Text.Trim() == "番地")
            {
                MessageBox.Show(form, "番地以外のチャンネル名を入力してください。");
                return;
            }
            form.DialogResult = DialogResult.OK;
        };
        form.Controls.AddRange([nameLabel, input, columnLabel, combo, hint, ok, cancel]);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return form.ShowDialog() == DialogResult.OK
            ? (input.Text.Trim(), combo.SelectedIndex == 0 ? null : columns[combo.SelectedIndex - 1]) : null;
    }

    public static double? ShowWeight(double initial)
    {
        using var form = new Form
        {
            Text = "セルの重み", Width = 400, Height = 180,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
            StartPosition = FormStartPosition.CenterScreen, ShowInTaskbar = false,
        };
        using var label = new Label { Text = "重み（0～1）　選択範囲がある場合は一括入力", Left = 14, Top = 12, Width = 360 };
        using var input = new NumericUpDown
        {
            Left = 14, Top = 42, Width = 350, Minimum = 0, Maximum = 1, DecimalPlaces = 6,
            Increment = 0.1m, Value = (decimal)Math.Clamp(initial, 0, 1),
        };
        using var ok = new Button { Text = "適用", Left = 188, Top = 88, Width = 80, DialogResult = DialogResult.OK };
        using var cancel = new Button { Text = "キャンセル", Left = 274, Top = 88, Width = 90, DialogResult = DialogResult.Cancel };
        form.Controls.AddRange([label, input, ok, cancel]);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return form.ShowDialog() == DialogResult.OK ? (double)input.Value : null;
    }
}
