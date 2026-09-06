namespace CircleSpaceCoordinator.Desktop;

internal static class TextPromptDialog
{
    public static string? Show(string title, string labelText, string initialValue)
    {
        using var form = new System.Windows.Forms.Form
        {
            Text = title,
            Width = 460,
            Height = 165,
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen,
            ShowInTaskbar = false,
        };
        using var label = new System.Windows.Forms.Label { Text = labelText, Left = 14, Top = 14, AutoSize = true };
        using var input = new System.Windows.Forms.TextBox
        {
            Text = initialValue,
            Left = 14,
            Top = 40,
            Width = 416,
            MaxLength = 100,
        };
        using var ok = new System.Windows.Forms.Button
        {
            Text = "OK",
            DialogResult = System.Windows.Forms.DialogResult.OK,
            Left = 274,
            Top = 79,
            Width = 75,
        };
        using var cancel = new System.Windows.Forms.Button
        {
            Text = "キャンセル",
            DialogResult = System.Windows.Forms.DialogResult.Cancel,
            Left = 355,
            Top = 79,
            Width = 75,
        };
        form.Controls.AddRange([label, input, ok, cancel]);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        form.Shown += (_, _) => { input.Focus(); input.SelectAll(); };
        return form.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(input.Text)
            ? input.Text.Trim()
            : null;
    }
}
