namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Core.Model;

internal sealed class GenreStyleDialog : System.Windows.Forms.Form
{
    private static readonly Choice[] Colors =
    [
        new("red", "赤"), new("yellow", "黄"), new("yellow-green", "黄緑"), new("green", "緑"),
        new("cyan", "水色"), new("blue-green", "青緑"), new("blue", "青"), new("indigo", "藍色"),
        new("blue-violet", "青紫"), new("red-violet", "赤紫"), new("pink", "桃色"), new("brown", "茶色"),
        new("white", "白"), new("black", "黒"), new("gray", "灰色"),
    ];
    private static readonly Choice[] Patterns =
    [
        new("solid", "■■■■ 単色"),
        new("horizontal", "━━━━ （太細）横縞"),
        new("vertical", "┃┃┃┃ （太細）縦縞"),
        new("thick-grid", "▦▦▦▦ （太細）格子"),
        new("checkerboard", "■□■□ 市松模様"),
        new("grid", "╬╬╬╬ 格子"),
        new("uniform-horizontal", "▰▱▰▱ （均等）横縞"),
        new("dots", "●○●○ 水玉"),
    ];
    private readonly IEditorWorkspace workspace;
    private readonly System.Windows.Forms.DataGridView grid = new()
    {
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false,
        SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect,
    };

    private GenreStyleDialog(IEditorWorkspace workspace)
    {
        this.workspace = workspace;
        Text = "ジャンルと色・網掛けパターンの対応";
        Width = 900;
        Height = 650;
        MinimumSize = new System.Drawing.Size(720, 480);
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);

        var explanation = new System.Windows.Forms.Label
        {
            Text = "ジャンルごとに主色・副色・網掛けを選びます。見本の黒が主色、白が副色です。括弧内は（主色の太さ・副色の太さ）の順です。",
            Left = 16,
            Top = 14,
            Width = 850,
            Height = 40,
        };
        grid.Left = 16;
        grid.Top = 58;
        grid.Width = 850;
        grid.Height = 488;
        grid.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom |
                      System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        grid.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn
        {
            Name = "genre",
            HeaderText = "ジャンル",
            ReadOnly = true,
            FillWeight = 130,
        });
        grid.Columns.Add(ColorColumn("primary", "主色"));
        grid.Columns.Add(ColorColumn("secondary", "副色"));
        grid.Columns.Add(ComboColumn("pattern", "網掛け", Patterns, 170));
        grid.EditingControlShowing += GridEditingControlShowing;
        grid.CellPainting += GridCellPainting;
        grid.CellDoubleClick += PickCustomColor;
        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty)
                grid.CommitEdit(System.Windows.Forms.DataGridViewDataErrorContexts.Commit);
        };
        grid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == grid.Columns["pattern"]?.Index)
                UpdateSecondaryCell(e.RowIndex);
        };

        var save = new System.Windows.Forms.Button
        {
            Text = "保存",
            Width = 100,
            Height = 34,
            Left = 766,
            Top = 560,
            Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right,
        };
        var cancel = new System.Windows.Forms.Button
        {
            Text = "キャンセル",
            Width = 100,
            Height = 34,
            Left = 656,
            Top = 560,
            Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right,
            DialogResult = System.Windows.Forms.DialogResult.Cancel,
        };
        save.Click += (_, _) => SaveMappings();
        Controls.AddRange([explanation, grid, save, cancel]);
        AcceptButton = save;
        CancelButton = cancel;
        PopulateRows();
    }

    public static bool ShowEditor(IEditorWorkspace workspace, System.Windows.Forms.IWin32Window? owner = null)
    {
        using var dialog = new GenreStyleDialog(workspace);
        return dialog.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK;
    }

    private void PopulateRows()
    {
        var genres = workspace.Project.Participants.Select(item => item.GenreId)
            .Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var configured = workspace.Project.GenreStyles.ToDictionary(item => item.GenreId, StringComparer.Ordinal);
        for (var index = 0; index < genres.Length; index++)
        {
            var genre = genres[index];
            var style = configured.GetValueOrDefault(genre) ?? DefaultStyle(genre, index);
            if (style.Pattern == "checker")
                style = style with { Pattern = "thick-grid" };
            if (style.Pattern == "thick-horizontal")
                style = style with { Pattern = "uniform-horizontal" };
            grid.Rows.Add(genre, style.PrimaryColor, style.SecondaryColor, style.Pattern);
            UpdateSecondaryCell(grid.Rows.Count - 1);
        }
    }

    private void SaveMappings()
    {
        var mappings = grid.Rows.Cast<System.Windows.Forms.DataGridViewRow>()
            .Select(row => new GenreStyleDefinition(
                CellValue(row, "genre"),
                NormalizeColor(CellValue(row, "primary")),
                NormalizeColor(CellValue(row, "secondary")),
                CellValue(row, "pattern")))
            .ToArray();
        var invalid = mappings.SelectMany(mapping => new[]
            {
                (mapping.GenreId, Label: "主色", Value: mapping.PrimaryColor),
                (mapping.GenreId, Label: "副色", Value: mapping.SecondaryColor),
            })
            .FirstOrDefault(item => !Colors.Any(choice => choice.Id == item.Value) &&
                !RgbHexColor.TryParse(item.Value, out _, out _, out _));
        if (invalid != default)
        {
            System.Windows.Forms.MessageBox.Show(this,
                $"{invalid.GenreId} の{invalid.Label}「{invalid.Value}」を色名または #RRGGBB 形式で入力してください。",
                "色コードが正しくありません",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Warning);
            return;
        }
        workspace.Execute(new SetGenreStyles(mappings), selectedPlanEdit: false);
        DialogResult = System.Windows.Forms.DialogResult.OK;
        Close();
    }

    private void GridEditingControlShowing(
        object? sender,
        System.Windows.Forms.DataGridViewEditingControlShowingEventArgs e)
    {
        if (e.Control is not System.Windows.Forms.ComboBox combo)
            return;
        combo.DrawItem -= DrawColorChoice;
        var colorColumn = grid.CurrentCell?.OwningColumn?.Name is "primary" or "secondary";
        combo.DrawMode = colorColumn
            ? System.Windows.Forms.DrawMode.OwnerDrawFixed
            : System.Windows.Forms.DrawMode.Normal;
        if (colorColumn)
            combo.DrawItem += DrawColorChoice;
    }

    private static void DrawColorChoice(object? sender, System.Windows.Forms.DrawItemEventArgs e)
    {
        if (sender is not System.Windows.Forms.ComboBox combo || e.Index < 0 || e.Index >= combo.Items.Count)
            return;
        var choice = combo.Items[e.Index] as Choice;
        var fill = ColorValue(choice?.Id);
        using var brush = new System.Drawing.SolidBrush(fill);
        e.Graphics.FillRectangle(brush, e.Bounds);
        var foreground = IsDark(fill) ? System.Drawing.Color.White : System.Drawing.Color.Black;
        System.Windows.Forms.TextRenderer.DrawText(
            e.Graphics,
            choice?.Label ?? Convert.ToString(combo.Items[e.Index]) ?? "",
            e.Font,
            new System.Drawing.Rectangle(e.Bounds.X + 5, e.Bounds.Y, e.Bounds.Width - 5, e.Bounds.Height),
            foreground,
            System.Windows.Forms.TextFormatFlags.Left | System.Windows.Forms.TextFormatFlags.VerticalCenter);
        e.DrawFocusRectangle();
    }

    private void GridCellPainting(object? sender, System.Windows.Forms.DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || grid.Columns[e.ColumnIndex].Name is not ("primary" or "secondary"))
            return;
        if (grid.Columns[e.ColumnIndex].Name == "secondary" && IsSolidPattern(e.RowIndex))
        {
            e.PaintBackground(e.CellBounds, true);
            var disabledBounds = System.Drawing.Rectangle.Inflate(e.CellBounds, -1, -1);
            using var controlBrush = new System.Drawing.SolidBrush(System.Drawing.SystemColors.Control);
            e.Graphics!.FillRectangle(controlBrush, disabledBounds);
            e.Paint(e.ClipBounds, System.Windows.Forms.DataGridViewPaintParts.Border);
            e.Handled = true;
            return;
        }
        e.PaintBackground(e.CellBounds, true);
        var id = Convert.ToString(e.FormattedValue, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        var choice = Colors.FirstOrDefault(item => item.Label == id || item.Id == id);
        var fill = ColorValue(choice?.Id ?? id);
        var inner = System.Drawing.Rectangle.Inflate(e.CellBounds, -2, -2);
        using var brush = new System.Drawing.SolidBrush(fill);
        e.Graphics!.FillRectangle(brush, inner);
        System.Windows.Forms.TextRenderer.DrawText(
            e.Graphics,
            choice?.Label ?? id,
            e.CellStyle?.Font ?? grid.Font,
            inner,
            IsDark(fill) ? System.Drawing.Color.White : System.Drawing.Color.Black,
            System.Windows.Forms.TextFormatFlags.HorizontalCenter | System.Windows.Forms.TextFormatFlags.VerticalCenter);
        e.Paint(e.ClipBounds, System.Windows.Forms.DataGridViewPaintParts.Border);
        e.Handled = true;
    }

    private bool IsSolidPattern(int rowIndex) =>
        string.Equals(Convert.ToString(grid.Rows[rowIndex].Cells["pattern"].Value), "solid", StringComparison.Ordinal);

    private void UpdateSecondaryCell(int rowIndex)
    {
        var cell = grid.Rows[rowIndex].Cells["secondary"];
        var solid = IsSolidPattern(rowIndex);
        cell.ReadOnly = solid;
        cell.ToolTipText = solid ? "単色では副色を使用しません。" : "網掛けの副色を選択します。";
        if (cell is System.Windows.Forms.DataGridViewComboBoxCell comboCell)
            comboCell.DisplayStyle = solid
                ? System.Windows.Forms.DataGridViewComboBoxDisplayStyle.Nothing
                : System.Windows.Forms.DataGridViewComboBoxDisplayStyle.DropDownButton;
        grid.InvalidateCell(cell);
    }

    private static System.Drawing.Color ColorValue(string? id) => id switch
    {
        "red" => System.Drawing.Color.FromArgb(210, 72, 65),
        "yellow" => System.Drawing.Color.FromArgb(229, 194, 55),
        "yellow-green" => System.Drawing.Color.FromArgb(166, 201, 65),
        "green" => System.Drawing.Color.FromArgb(69, 184, 85),
        "cyan" => System.Drawing.Color.FromArgb(70, 190, 207),
        "blue-green" => System.Drawing.Color.FromArgb(45, 162, 147),
        "blue" => System.Drawing.Color.FromArgb(64, 132, 207),
        "indigo" => System.Drawing.Color.FromArgb(65, 80, 160),
        "blue-violet" => System.Drawing.Color.FromArgb(116, 82, 190),
        "red-violet" => System.Drawing.Color.FromArgb(181, 71, 151),
        "pink" => System.Drawing.Color.FromArgb(224, 103, 153),
        "brown" => System.Drawing.Color.FromArgb(151, 99, 62),
        "white" => System.Drawing.Color.White,
        "black" => System.Drawing.Color.FromArgb(25, 28, 32),
        _ when RgbHexColor.TryParse(id, out var red, out var green, out var blue) =>
            System.Drawing.Color.FromArgb(red, green, blue),
        _ => System.Drawing.Color.FromArgb(128, 136, 144),
    };

    private void PickCustomColor(object? sender, System.Windows.Forms.DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || grid.Columns[e.ColumnIndex].Name is not ("primary" or "secondary") ||
            grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ReadOnly)
            return;
        var cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
        using var picker = new System.Windows.Forms.ColorDialog
        {
            Color = ColorValue(Convert.ToString(cell.Value, System.Globalization.CultureInfo.InvariantCulture)),
            FullOpen = true,
        };
        if (picker.ShowDialog(this) != System.Windows.Forms.DialogResult.OK)
            return;
        cell.Value = $"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}";
        grid.InvalidateCell(cell);
    }

    private static bool IsDark(System.Drawing.Color color) =>
        color.R * 0.299d + color.G * 0.587d + color.B * 0.114d < 145d;

    private static string CellValue(System.Windows.Forms.DataGridViewRow row, string columnName) =>
        Convert.ToString(row.Cells[columnName].Value, System.Globalization.CultureInfo.InvariantCulture)
        ?? throw new InvalidOperationException($"{columnName} が選択されていません。");

    private static string NormalizeColor(string value) =>
        value.StartsWith('#') ? value.ToUpperInvariant() : value;

    private static GenreStyleDefinition DefaultStyle(string genre, int index)
    {
        var baseColors = Colors.Take(12).ToArray();
        var patternIndex = index / baseColors.Length;
        return new GenreStyleDefinition(
            genre,
            baseColors[index % baseColors.Length].Id,
            "white",
            patternIndex == 0 ? "solid" : Patterns[1 + (patternIndex - 1) % (Patterns.Length - 1)].Id);
    }

    private static System.Windows.Forms.DataGridViewComboBoxColumn ComboColumn(
        string name,
        string header,
        Choice[] choices,
        float fillWeight = 90) => new()
    {
        Name = name,
        HeaderText = header,
        DataSource = choices,
        DisplayMember = nameof(Choice.Label),
        ValueMember = nameof(Choice.Id),
        FillWeight = fillWeight,
        DisplayStyle = System.Windows.Forms.DataGridViewComboBoxDisplayStyle.DropDownButton,
    };

    private static System.Windows.Forms.DataGridViewTextBoxColumn ColorColumn(string name, string header) => new()
    {
        Name = name,
        HeaderText = header,
        FillWeight = 90,
        ToolTipText = "色名または #RRGGBB。ダブルクリックで色を選択できます。",
    };

    private sealed record Choice(string Id, string Label);
}
