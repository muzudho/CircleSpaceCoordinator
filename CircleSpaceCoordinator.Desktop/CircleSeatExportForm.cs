namespace CircleSpaceCoordinator.Desktop;

using CircleSpaceCoordinator.Infrastructure.Tabular;
using CircleSpaceCoordinator.Desktop.Persistence;

internal sealed class CircleSeatExportForm : System.Windows.Forms.Form
{
    private readonly string path;
    private readonly IReadOnlyList<ParticipantTableSheet> sheets;
    private readonly IReadOnlyList<CircleSeatExportRow> rows;
    private readonly System.Windows.Forms.ComboBox sheetBox = new() { DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
    private readonly System.Windows.Forms.ComboBox blockBox = new() { DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
    private readonly System.Windows.Forms.ComboBox seatBox = new() { DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
    private readonly System.Windows.Forms.ComboBox circleIdBox = new() { DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };

    private CircleSeatExportForm(string path, IReadOnlyList<ParticipantTableSheet> sheets, IReadOnlyList<CircleSeatExportRow> rows)
    {
        this.path = path;
        this.sheets = sheets;
        this.rows = rows;
        Text = "Excelへ書き出し";
        Width = 760;
        Height = 270;
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        Controls.Add(new System.Windows.Forms.Label { Text = path, Left = 14, Top = 14, Width = 710, AutoEllipsis = true });
        AddLabel("シート", 14); AddLabel("ブロック番号列", 195); AddLabel("席番号列", 376); AddLabel("サークルID列", 557);
        Setup(sheetBox, 14); Setup(blockBox, 195); Setup(seatBox, 376); Setup(circleIdBox, 557);
        var note = new System.Windows.Forms.Label { Text = "配置済みで席名のあるサークルだけを書き出します。該当しない行は変更しません。", Left = 14, Top = 111, Width = 710 };
        var export = new System.Windows.Forms.Button { Text = "この対応で書き出す", Left = 540, Top = 155, Width = 184, Height = 34 };
        var cancel = new System.Windows.Forms.Button { Text = "キャンセル", Left = 428, Top = 155, Width = 100, Height = 34, DialogResult = System.Windows.Forms.DialogResult.Cancel };
        export.Click += (_, _) => Export();
        sheetBox.SelectedIndexChanged += (_, _) => SelectSheet();
        Controls.AddRange([sheetBox, blockBox, seatBox, circleIdBox, note, export, cancel]);
        AcceptButton = export; CancelButton = cancel;
        foreach (var sheet in sheets) sheetBox.Items.Add(sheet.Name);
        sheetBox.SelectedIndex = 0;
    }

    public CircleSeatExportResult? Result { get; private set; }

    public static CircleSeatExportResult? ShowExport(IReadOnlyList<CircleSeatExportRow> rows, ApplicationSettingsService? settings = null, System.Windows.Forms.IWin32Window? owner = null)
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Filter = "Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm",
            Title = "書き出し先のExcelを選択",
            CheckFileExists = true,
            InitialDirectory = settings?.Current.CircleSeatExportDirectory,
        };
        if (dialog.ShowDialog(owner) != System.Windows.Forms.DialogResult.OK) return null;
        try
        {
            settings?.RememberCircleSeatExportPath(dialog.FileName);
            var sheets = LoadingSpinnerDialog.Run(owner, "Excelファイルを読み込んでいます…", () => ParticipantTableReader.Read(dialog.FileName));
            if (sheets.Count == 0) throw new InvalidDataException("列見出しのあるシートがありません。");
            using var form = new CircleSeatExportForm(dialog.FileName, sheets, rows);
            return form.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK ? form.Result : null;
        }
        catch (Exception exception)
        {
            System.Windows.Forms.MessageBox.Show(owner, exception.Message, "Excelへ書き出し", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            return null;
        }
    }

    private void SelectSheet()
    {
        if (sheetBox.SelectedIndex < 0) return;
        var headers = sheets[sheetBox.SelectedIndex].Headers;
        Fill(blockBox, headers, "ブロック");
        Fill(seatBox, headers, "席");
        Fill(circleIdBox, headers, "サークル", "circle", "id");
    }

    private void Export()
    {
        try
        {
            // Read every UI value before entering the background file operation.
            var sheetName = sheets[sheetBox.SelectedIndex].Name;
            var blockColumn = Column(blockBox);
            var seatColumn = Column(seatBox);
            var circleIdColumn = Column(circleIdBox);
            Result = LoadingSpinnerDialog.Run(this, "Excelファイルへ書き出しています…", () =>
                CircleSeatExcelExporter.Export(path, sheetName,
                    blockColumn, seatColumn, circleIdColumn, rows));
            System.Windows.Forms.MessageBox.Show(this, $"{Result.UpdatedRowCount} 行へ書き出しました。\nExcel内で見つからないサークルID: {Result.MissingCircleCount} 件", "Excelへ書き出し", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            DialogResult = System.Windows.Forms.DialogResult.OK; Close();
        }
        catch (Exception exception)
        {
            System.Windows.Forms.MessageBox.Show(this, exception.Message, "書き出しエラー", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
        }
    }

    private void AddLabel(string text, int left) => Controls.Add(new System.Windows.Forms.Label { Text = text, Left = left, Top = 46, Width = 165 });
    private static void Setup(System.Windows.Forms.ComboBox combo, int left) { combo.Left = left; combo.Top = 67; combo.Width = 165; }
    private static int Column(System.Windows.Forms.ComboBox box) => box.SelectedItem is ColumnChoice choice ? choice.Index : throw new InvalidOperationException("列を選択してください。");
    private static void Fill(System.Windows.Forms.ComboBox box, IReadOnlyList<string> headers, params string[] hints)
    {
        box.Items.Clear();
        foreach (var item in headers.Select((header, index) => new ColumnChoice(index, $"{index + 1}: {(string.IsNullOrWhiteSpace(header) ? "（見出しなし）" : header)}"))) box.Items.Add(item);
        var selected = headers.ToList().FindIndex(header => hints.All(hint => header.Contains(hint, StringComparison.OrdinalIgnoreCase)));
        box.SelectedIndex = selected >= 0 ? selected : 0;
    }
    private sealed record ColumnChoice(int Index, string Label) { public override string ToString() => Label; }
}
