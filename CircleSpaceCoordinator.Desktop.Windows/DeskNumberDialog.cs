namespace CircleSpaceCoordinator.Desktop.Windows;

internal sealed record DeskNumberEdit(string? DeskNumber);

internal static class DeskNumberDialog
{
    public static DeskNumberEdit? Show(string? initialDeskNumber, string fieldName = "フレーム番号")
    {
        using var form = new System.Windows.Forms.Form
        {
            Text = $"{fieldName}を変更",
            Width = 430,
            Height = 165,
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen,
            ShowInTaskbar = false,
        };
        using var label = new System.Windows.Forms.Label { Text = $"{fieldName}（空欄で削除）", Left = 14, Top = 15, AutoSize = true };
        using var number = new System.Windows.Forms.TextBox { Text = initialDeskNumber ?? "", Left = 14, Top = 38, Width = 386, MaxLength = 80 };
        using var ok = new System.Windows.Forms.Button { Text = "OK", Left = 244, Top = 78, Width = 75 };
        using var cancel = new System.Windows.Forms.Button { Text = "キャンセル", DialogResult = System.Windows.Forms.DialogResult.Cancel, Left = 325, Top = 78, Width = 75 };
        DeskNumberEdit? result = null;
        ok.Click += (_, _) =>
        {
            result = new DeskNumberEdit(string.IsNullOrWhiteSpace(number.Text) ? null : number.Text.Trim());
            form.DialogResult = System.Windows.Forms.DialogResult.OK;
            form.Close();
        };
        form.Controls.AddRange([label, number, ok, cancel]);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        form.Shown += (_, _) => { number.Focus(); number.SelectAll(); };
        return form.ShowDialog() == System.Windows.Forms.DialogResult.OK ? result : null;
    }
}
