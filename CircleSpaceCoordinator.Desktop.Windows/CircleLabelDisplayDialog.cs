namespace CircleSpaceCoordinator.Desktop.Windows;

using System.Text.RegularExpressions;
using CircleSpaceCoordinator.Desktop.Core.Persistence;

internal static class CircleLabelDisplayDialog
{
    public static CircleLabelDisplaySettings? Show(CircleLabelDisplaySettings? initial)
    {
        initial ??= new CircleLabelDisplaySettings();
        using var form = new System.Windows.Forms.Form
        {
            Text = "サークルIDの正規表現",
            Width = 570,
            Height = 279,
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen,
            ShowInTaskbar = false,
        };
        using var description = new System.Windows.Forms.Label
        {
            Text = "［サークルID］を選んだときの表示に適用します。\r\n正規表現を空欄にすると、元のIDをそのまま表示します。",
            Left = 14,
            Top = 14,
            Width = 520,
            Height = 42,
        };
        using var patternLabel = new System.Windows.Forms.Label { Text = "正規表現", Left = 14, Top = 76, AutoSize = true };
        using var pattern = new System.Windows.Forms.TextBox { Left = 120, Top = 72, Width = 400, Text = initial.CircleIdPattern ?? "" };
        using var replacementLabel = new System.Windows.Forms.Label { Text = "置換文字列", Left = 14, Top = 112, AutoSize = true };
        using var replacement = new System.Windows.Forms.TextBox { Left = 120, Top = 108, Width = 400, Text = initial.CircleIdReplacement ?? "" };
        using var example = new System.Windows.Forms.Label
        {
            Text = "例：  \\w+\\d+-(\\d+)    ／    $1",
            Left = 120,
            Top = 141,
            Width = 400,
            Height = 24,
            ForeColor = System.Drawing.Color.DimGray,
        };
        using var save = new System.Windows.Forms.Button { Text = "保存", Left = 364, Top = 190, Width = 75 };
        using var cancel = new System.Windows.Forms.Button
        {
            Text = "キャンセル",
            DialogResult = System.Windows.Forms.DialogResult.Cancel,
            Left = 445,
            Top = 190,
            Width = 75,
        };

        save.Click += (_, _) =>
        {
            var enteredPattern = string.IsNullOrWhiteSpace(pattern.Text) ? null : pattern.Text.Trim();
            if (enteredPattern is not null)
            {
                try
                {
                    _ = new Regex(enteredPattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
                }
                catch (ArgumentException exception)
                {
                    System.Windows.Forms.MessageBox.Show($"正規表現が正しくありません。\r\n{exception.Message}", "サークル表示の設定",
                        System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }
            }
            form.Tag = initial with
            {
                CircleIdPattern = enteredPattern,
                CircleIdReplacement = string.IsNullOrWhiteSpace(replacement.Text) ? null : replacement.Text,
            };
            form.DialogResult = System.Windows.Forms.DialogResult.OK;
            form.Close();
        };

        form.Controls.AddRange([description, patternLabel, pattern, replacementLabel, replacement, example, save, cancel]);
        form.AcceptButton = save;
        form.CancelButton = cancel;
        return form.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? (CircleLabelDisplaySettings?)form.Tag
            : null;
    }
}
