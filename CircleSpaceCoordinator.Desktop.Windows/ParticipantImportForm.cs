namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Application.Participants;
using CircleSpaceCoordinator.Application.Workspace;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.Infrastructure.Tabular;

internal sealed class ParticipantImportForm : System.Windows.Forms.Form
{
    private readonly ProjectWorkspace workspace;
    private readonly IReadOnlyList<ParticipantTableSheet> sheets;
    private readonly System.Windows.Forms.ComboBox sheetBox = new() { DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
    private readonly System.Windows.Forms.ComboBox circleIdBox = new() { DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
    private readonly System.Windows.Forms.ComboBox nameBox = new() { DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
    private readonly System.Windows.Forms.ComboBox requiredCellCountBox = new() { DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
    private readonly System.Windows.Forms.ComboBox combinedWithCircleIdBox = new() { DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
    private readonly System.Windows.Forms.ComboBox genreIdBox = new() { DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
    private readonly System.Windows.Forms.DataGridView preview = new()
    {
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.DisplayedCells,
        RowHeadersVisible = false,
    };

    private ParticipantImportForm(ProjectWorkspace workspace, string path, IReadOnlyList<ParticipantTableSheet> sheets)
    {
        this.workspace = workspace;
        this.sheets = sheets;
        Text = "参加サークル一覧の読込み";
        Width = 1380;
        Height = 650;
        MinimumSize = new System.Drawing.Size(980, 500);
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);

        var sourceLabel = new System.Windows.Forms.Label
        {
            Text = path,
            Left = 16,
            Top = 14,
            Width = 1330,
            AutoEllipsis = true,
        };
        AddLabel("シート", 16, 48);
        AddLabel("サークルID列", 235, 48);
        AddLabel("サークル名列", 470, 48);
        AddLabel("必要セル数列（任意）", 705, 48);
        AddLabel("ジャンルID列（任意）", 900, 48);
        AddLabel("合体先サークルID列（任意）", 1125, 48);
        SetupCombo(sheetBox, 16);
        SetupCombo(circleIdBox, 235);
        SetupCombo(nameBox, 470);
        SetupCombo(requiredCellCountBox, 705);
        SetupCombo(genreIdBox, 900);
        SetupCombo(combinedWithCircleIdBox, 1125);
        preview.Left = 16;
        preview.Top = 108;
        preview.Width = 1330;
        preview.Height = 450;
        preview.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom |
                         System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;

        var importButton = new System.Windows.Forms.Button
        {
            Text = "この対応で取り込む",
            Left = 1176,
            Top = 570,
            Width = 170,
            Height = 34,
            Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right,
        };
        var cancelButton = new System.Windows.Forms.Button
        {
            Text = "キャンセル",
            Left = 1066,
            Top = 570,
            Width = 100,
            Height = 34,
            Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right,
            DialogResult = System.Windows.Forms.DialogResult.Cancel,
        };
        importButton.Click += (_, _) => Import();
        sheetBox.SelectedIndexChanged += (_, _) => SelectSheet();
        Controls.Add(sourceLabel);
        Controls.AddRange([sheetBox, circleIdBox, nameBox, requiredCellCountBox, genreIdBox, combinedWithCircleIdBox, preview, importButton, cancelButton]);
        AcceptButton = importButton;
        CancelButton = cancelButton;

        foreach (var sheet in sheets)
            sheetBox.Items.Add(sheet.Name);
        sheetBox.SelectedIndex = 0;
    }

    public int ImportedCount { get; private set; }

    public static int? ShowImport(ProjectWorkspace workspace, ApplicationSettingsService? settings = null, System.Windows.Forms.IWin32Window? owner = null)
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Filter = "参加サークル一覧 (*.xlsx;*.xlsm;*.csv)|*.xlsx;*.xlsm;*.csv|Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|CSV (*.csv)|*.csv",
            Title = "参加サークル一覧を開く",
            CheckFileExists = true,
            InitialDirectory = settings?.Current.ParticipantImportDirectory,
        };
        if (dialog.ShowDialog(owner) != System.Windows.Forms.DialogResult.OK)
            return null;
        try
        {
            settings?.RememberParticipantImportPath(dialog.FileName);
            var sheets = LoadingSpinnerDialog.Run(owner, "ファイルを読み込んでいます…", () => ParticipantTableReader.Read(dialog.FileName));
            if (sheets.Count == 0)
                throw new InvalidDataException("列見出しのあるシートがありません。");
            using var form = new ParticipantImportForm(workspace, dialog.FileName, sheets);
            return form.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK ? form.ImportedCount : null;
        }
        catch (Exception exception)
        {
            System.Windows.Forms.MessageBox.Show(owner, exception.Message, "参加サークル一覧の読込み",
                System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            return null;
        }
    }

    private void SelectSheet()
    {
        if (sheetBox.SelectedIndex < 0)
            return;
        var sheet = sheets[sheetBox.SelectedIndex];
        var guess = ParticipantTableMapper.Guess(sheet.Headers);
        FillColumns(circleIdBox, sheet.Headers, optional: false, guess.CircleIdColumn);
        FillColumns(nameBox, sheet.Headers, optional: false, guess.DisplayNameColumn);
        FillColumns(requiredCellCountBox, sheet.Headers, optional: true, guess.RequiredCellCountColumn);
        FillColumns(combinedWithCircleIdBox, sheet.Headers, optional: true, guess.CombinedWithCircleIdColumn);
        FillColumns(genreIdBox, sheet.Headers, optional: true, guess.GenreIdColumn);
        preview.Columns.Clear();
        foreach (var header in sheet.Headers.Select((text, index) => string.IsNullOrWhiteSpace(text) ? $"列 {index + 1}" : text))
            preview.Columns.Add($"column{preview.Columns.Count}", header);
        preview.Rows.Clear();
        foreach (var row in sheet.Rows.Take(100))
            preview.Rows.Add(row.Cast<object>().ToArray());
    }

    private void Import()
    {
        try
        {
            var sheet = sheets[sheetBox.SelectedIndex];
            var mapping = new ParticipantColumnMapping(
                SelectedColumn(circleIdBox),
                SelectedColumn(nameBox),
                SelectedOptionalColumn(requiredCellCountBox),
                SelectedOptionalColumn(combinedWithCircleIdBox),
                SelectedOptionalColumn(genreIdBox));
            var rows = ParticipantTableMapper.Map(sheet, mapping);
            var answer = System.Windows.Forms.MessageBox.Show(this,
                $"参加サークル {rows.Count} 件で一覧を更新します。\n一覧から消えたサークルの配置は解除されます。",
                "参加サークル一覧の確認",
                System.Windows.Forms.MessageBoxButtons.OKCancel,
                System.Windows.Forms.MessageBoxIcon.Question);
            if (answer != System.Windows.Forms.DialogResult.OK)
                return;
            workspace.ApplyProjectEdit(project => ParticipantCatalogService.ReplaceParticipants(project, rows));
            ImportedCount = rows.Count;
            DialogResult = System.Windows.Forms.DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            System.Windows.Forms.MessageBox.Show(this, exception.Message, "取込みエラー",
                System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
        }
    }

    private void AddLabel(string text, int left, int top) => Controls.Add(new System.Windows.Forms.Label
    {
        Text = text,
        Left = left,
        Top = top,
        Width = 210,
    });

    private static void SetupCombo(System.Windows.Forms.ComboBox combo, int left)
    {
        combo.Left = left;
        combo.Top = 69;
        combo.Width = 195;
    }

    private static void FillColumns(
        System.Windows.Forms.ComboBox combo,
        IReadOnlyList<string> headers,
        bool optional,
        int? selected)
    {
        combo.Items.Clear();
        if (optional)
            combo.Items.Add(new ColumnChoice(-1, "（使用しない）"));
        foreach (var item in headers.Select((header, index) => new ColumnChoice(index,
                     $"{index + 1}: {(string.IsNullOrWhiteSpace(header) ? "（見出しなし）" : header)}")))
            combo.Items.Add(item);
        var target = selected ?? (optional ? -1 : 0);
        combo.SelectedIndex = combo.Items.Cast<ColumnChoice>().ToList().FindIndex(item => item.Index == target);
    }

    private static int SelectedColumn(System.Windows.Forms.ComboBox combo)
    {
        if (combo.SelectedItem is not ColumnChoice choice || choice.Index < 0)
            throw new InvalidOperationException("列を選択してください。");
        return choice.Index;
    }

    private static int? SelectedOptionalColumn(System.Windows.Forms.ComboBox combo) =>
        combo.SelectedItem is ColumnChoice { Index: >= 0 } choice ? choice.Index : null;

    private sealed record ColumnChoice(int Index, string Label)
    {
        public override string ToString() => Label;
    }
}
