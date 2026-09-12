namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;

internal static class LayoutBindingDialog
{
    public static string? Show(IReadOnlyList<DeskLayout> deskLayouts, string currentDeskLayoutId,
        string prompt = "紐付け先の机配置")
    {
        using var form = new System.Windows.Forms.Form
        {
            Text = "机配置を選択",
            Width = 430,
            Height = 160,
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen,
            ShowInTaskbar = false,
        };
        using var label = new System.Windows.Forms.Label { Text = prompt, Left = 14, Top = 14, AutoSize = true };
        using var choices = new System.Windows.Forms.ComboBox
        {
            Left = 14, Top = 38, Width = 386,
            DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList,
        };
        var items = deskLayouts.Select(item => new Item(item.Id, item.Name)).ToArray();
        choices.Items.AddRange(items);
        choices.SelectedIndex = Math.Max(0, Array.FindIndex(items, item => item.Id == currentDeskLayoutId));
        using var ok = new System.Windows.Forms.Button { Text = "OK", Left = 244, Top = 75, Width = 75, DialogResult = System.Windows.Forms.DialogResult.OK };
        using var cancel = new System.Windows.Forms.Button { Text = "キャンセル", Left = 325, Top = 75, Width = 75, DialogResult = System.Windows.Forms.DialogResult.Cancel };
        form.Controls.AddRange([label, choices, ok, cancel]);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return form.ShowDialog() == System.Windows.Forms.DialogResult.OK ? (choices.SelectedItem as Item)?.Id : null;
    }

    private sealed record Item(string Id, string Name)
    {
        public override string ToString() => Name;
    }
}
