namespace CircleSpaceCoordinator.Desktop.Windows;

using System.Text.RegularExpressions;
using CircleSpaceCoordinator.Desktop.Windows.Persistence;

internal static class CircleLabelDisplayDialog
{
    public static CircleLabelDisplaySettings? Show(CircleLabelDisplaySettings? initial)
    {
        initial ??= new CircleLabelDisplaySettings();
        using var form = new System.Windows.Forms.Form
        {
            Text = "サークル表示の設定",
            Width = 570,
            Height = 315,
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen,
            ShowInTaskbar = false,
        };
        using var description = new System.Windows.Forms.Label
        {
            Text = "サークル配置モードで、各サークルに表示する内容を選びます。\r\nサークルIDでは正規表現による置換で、必要な部分だけを表示できます。",
            Left = 14,
            Top = 14,
            Width = 520,
            Height = 42,
        };
        using var fieldLabel = new System.Windows.Forms.Label { Text = "表示内容", Left = 14, Top = 70, AutoSize = true };
        using var field = new System.Windows.Forms.ComboBox
        {
            Left = 120,
            Top = 66,
            Width = 400,
            DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList,
        };
        field.Items.AddRange(["内部ID", "サークルID", "サークル名"]);
        field.SelectedIndex = initial.DisplayField switch
        {
            "circleId" => 1,
            "displayName" => 2,
            _ => 0,
        };

        using var patternLabel = new System.Windows.Forms.Label { Text = "正規表現", Left = 14, Top = 112, AutoSize = true };
        using var pattern = new System.Windows.Forms.TextBox { Left = 120, Top = 108, Width = 400, Text = initial.CircleIdPattern ?? "" };
        using var replacementLabel = new System.Windows.Forms.Label { Text = "置換文字列", Left = 14, Top = 148, AutoSize = true };
        using var replacement = new System.Windows.Forms.TextBox { Left = 120, Top = 144, Width = 400, Text = initial.CircleIdReplacement ?? "" };
        using var example = new System.Windows.Forms.Label
        {
            Text = "例：  \\w+\\d+-(\\d+)    ／    $1",
            Left = 120,
            Top = 177,
            Width = 400,
            Height = 24,
            ForeColor = System.Drawing.Color.DimGray,
        };
        using var save = new System.Windows.Forms.Button { Text = "保存", Left = 364, Top = 226, Width = 75 };
        using var cancel = new System.Windows.Forms.Button
        {
            Text = "キャンセル",
            DialogResult = System.Windows.Forms.DialogResult.Cancel,
            Left = 445,
            Top = 226,
            Width = 75,
        };

        void UpdateRegexControls()
        {
            var enabled = field.SelectedIndex == 1;
            patternLabel.Enabled = enabled;
            pattern.Enabled = enabled;
            replacementLabel.Enabled = enabled;
            replacement.Enabled = enabled;
            example.Enabled = enabled;
        }

        field.SelectedIndexChanged += (_, _) => UpdateRegexControls();
        save.Click += (_, _) =>
        {
            var displayField = field.SelectedIndex switch
            {
                1 => "circleId",
                2 => "displayName",
                _ => "internalId",
            };
            var enteredPattern = string.IsNullOrWhiteSpace(pattern.Text) ? null : pattern.Text.Trim();
            if (displayField == "circleId" && enteredPattern is not null)
            {
                try
                {
                    _ = new Regex(enteredPattern);
                }
                catch (ArgumentException exception)
                {
                    System.Windows.Forms.MessageBox.Show($"正規表現が正しくありません。\r\n{exception.Message}", "サークル表示の設定",
                        System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }
            }
            form.Tag = new CircleLabelDisplaySettings(displayField, enteredPattern,
                string.IsNullOrWhiteSpace(replacement.Text) ? null : replacement.Text);
            form.DialogResult = System.Windows.Forms.DialogResult.OK;
            form.Close();
        };

        form.Controls.AddRange([description, fieldLabel, field, patternLabel, pattern, replacementLabel, replacement, example, save, cancel]);
        form.AcceptButton = save;
        form.CancelButton = cancel;
        UpdateRegexControls();
        return form.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? (CircleLabelDisplaySettings?)form.Tag
            : null;
    }
}
