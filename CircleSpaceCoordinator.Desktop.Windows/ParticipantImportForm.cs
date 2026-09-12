namespace CircleSpaceCoordinator.Desktop.Windows;


using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.Infrastructure.Tabular;

internal sealed class ParticipantImportForm : System.Windows.Forms.Form
{
    private readonly IEditorWorkspace workspace;
    private readonly string fileName;
    private readonly string sourcePath;
    private IReadOnlyList<ParticipantTableSheet> sheets;
    private readonly System.Windows.Forms.ComboBox encodingBox = new() { DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
    private readonly System.Windows.Forms.Label encodingStatus = new() { Left = 540, Top = 45, Width = 805, Height = 38, AutoEllipsis = true };
    private readonly System.Windows.Forms.Button importButton = new();
    private readonly System.Windows.Forms.ComboBox sheetBox = new() { DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
    private readonly System.Windows.Forms.ComboBox circleIdBox = new() { DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
    private readonly System.Windows.Forms.ComboBox nameBox = new() { DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
    private readonly System.Windows.Forms.ComboBox requiredCellCountBox = new() { DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
    private readonly System.Windows.Forms.ComboBox combinedWithCircleIdBox = new() { DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
    private readonly System.Windows.Forms.ComboBox genreIdBox = new() { DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
    private readonly System.Windows.Forms.DataGridView preview = new()
    {
        ReadOnly = true,
        VirtualMode = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.DisplayedCells,
        RowHeadersVisible = false,
    };

    private ParticipantImportForm(IEditorWorkspace workspace, string path, IReadOnlyList<ParticipantTableSheet> sheets)
    {
        this.workspace = workspace;
        fileName = Path.GetFileName(path);
        sourcePath = path;
        this.sheets = sheets;
        var isCsv = Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase);
        var encodingRowHeight = isCsv ? 40 : 0;
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
        if (isCsv)
        {
            AddLabel("CSV の文字コード", 16, 48);
            encodingBox.Left = 235;
            encodingBox.Top = 45;
            encodingBox.Width = 290;
            encodingBox.Items.AddRange(["UTF-8（BOM あり／なし）", "Shift-JIS（Windows / CP932）"]);
            encodingBox.SelectedIndex = 0;
            encodingBox.SelectedIndexChanged += (_, _) => ReloadCsv();
            Controls.AddRange([encodingBox, encodingStatus]);
            Shown += (_, _) => ReloadCsv();
        }
        AddLabel("シート", 16, 48 + encodingRowHeight);
        AddLabel("サークルID列", 235, 48 + encodingRowHeight);
        AddLabel("サークル名列", 470, 48 + encodingRowHeight);
        AddLabel("必要セル数列（任意）", 705, 48 + encodingRowHeight);
        AddLabel("ジャンルID列（任意）", 900, 48 + encodingRowHeight);
        AddLabel("合体先サークルID列（任意）", 1125, 48 + encodingRowHeight);
        SetupCombo(sheetBox, 16, encodingRowHeight);
        SetupCombo(circleIdBox, 235, encodingRowHeight);
        SetupCombo(nameBox, 470, encodingRowHeight);
        SetupCombo(requiredCellCountBox, 705, encodingRowHeight);
        SetupCombo(genreIdBox, 900, encodingRowHeight);
        SetupCombo(combinedWithCircleIdBox, 1125, encodingRowHeight);
        preview.Left = 16;
        preview.Top = 108 + encodingRowHeight;
        preview.Width = 1330;
        preview.Height = 450 - encodingRowHeight;
        preview.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom |
                         System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;

        importButton.Text = "この対応で取り込む";
        importButton.Left = 1176;
        importButton.Top = 570;
        importButton.Width = 170;
        importButton.Height = 34;
        importButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        importButton.Enabled = sheets.Count > 0;
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
        preview.CellValueNeeded += (_, args) =>
        {
            if (sheetBox.SelectedIndex >= 0)
            {
                var rows = this.sheets[sheetBox.SelectedIndex].Rows;
                if (args.RowIndex < rows.Count && args.ColumnIndex < rows[args.RowIndex].Count)
                    args.Value = rows[args.RowIndex][args.ColumnIndex];
            }
        };
        Controls.Add(sourceLabel);
        Controls.AddRange([sheetBox, circleIdBox, nameBox, requiredCellCountBox, genreIdBox, combinedWithCircleIdBox, preview, importButton, cancelButton]);
        AcceptButton = importButton;
        CancelButton = cancelButton;

        foreach (var sheet in sheets)
            sheetBox.Items.Add(sheet.Name);
        if (sheets.Count > 0)
            sheetBox.SelectedIndex = 0;
    }

    public int ImportedCount { get; private set; }

    public static int? ShowImport(IEditorWorkspace workspace, ApplicationSettingsService? settings = null, System.Windows.Forms.IWin32Window? owner = null)
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
            var isCsv = Path.GetExtension(dialog.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase);
            // CSV must open the encoding selector even when the initial UTF-8 read fails.
            var sheets = isCsv ? Array.Empty<ParticipantTableSheet>()
                : LoadingSpinnerDialog.Run(owner, "ファイルを読み込んでいます…", () => ParticipantTableReader.Read(dialog.FileName));
            if (!isCsv && sheets.Count == 0)
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

    private void ReloadCsv()
    {
        importButton.Enabled = false;
        sheetBox.Items.Clear();
        preview.RowCount = 0;
        preview.Columns.Clear();
        foreach (var combo in new[] { circleIdBox, nameBox, requiredCellCountBox, combinedWithCircleIdBox, genreIdBox })
            combo.Items.Clear();
        sheets = [];
        encodingBox.Enabled = false;
        try
        {
            var encoding = encodingBox.SelectedIndex == 1 ? ParticipantCsvEncoding.ShiftJis : ParticipantCsvEncoding.Utf8;
            var result = LoadingSpinnerDialog.Run(this, "選択した文字コードで読み直しています…",
                () => ParticipantTableReader.ReadCsvPreview(sourcePath, encoding));
            sheets = [result.Sheet];
            if (sheets.Count == 0 || sheets[0].Headers.Count == 0)
                throw new InvalidDataException("列見出しのある CSV ではありません。");
            foreach (var sheet in sheets)
                sheetBox.Items.Add(sheet.Name);
            sheetBox.SelectedIndex = 0;
            if (result.HasDecodingErrors)
            {
                encodingStatus.ForeColor = System.Drawing.Color.Firebrick;
                encodingStatus.Text = "読み取れない文字を � で表示しています。文字コードを切り替え、表を確認してください。";
                return;
            }
            encodingStatus.ForeColor = System.Drawing.SystemColors.ControlText;
            encodingStatus.Text = "文字化けする場合は文字コードを切り替えてください。列の対応とプレビューを確認して取り込みます。";
            importButton.Enabled = true;
        }
        catch (Exception exception)
        {
            encodingStatus.ForeColor = System.Drawing.Color.Firebrick;
            var failure = exception.GetBaseException();
            encodingStatus.Text = failure is System.Text.DecoderFallbackException
                ? "この文字コードでは読み込めません。UTF-8／Shift-JIS を切り替えてください。"
                : $"読込みに失敗しました: {failure.Message}";
        }
        finally
        {
            encodingBox.Enabled = true;
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
        preview.RowCount = 0;
        preview.Columns.Clear();
        foreach (var header in sheet.Headers.Select((text, index) => string.IsNullOrWhiteSpace(text) ? $"列 {index + 1}" : text))
            preview.Columns.Add($"column{preview.Columns.Count}", header);
        preview.RowCount = sheet.Rows.Count;
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
            var source = new CircleSpaceCoordinator.Core.Model.ParticipantTableSource(
                fileName, sheet.Name, sheet.Headers.ToArray(), ParticipantTableMapper.GetColumnKeys(sheet.Headers));
            workspace.Execute(new ParticipantCatalogServiceReplaceParticipants(rows, source), selectedPlanEdit: false);
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

    private static void SetupCombo(System.Windows.Forms.ComboBox combo, int left, int encodingRowHeight)
    {
        combo.Left = left;
        combo.Top = 69 + encodingRowHeight;
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
