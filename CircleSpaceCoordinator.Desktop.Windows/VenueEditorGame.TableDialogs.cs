namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Infrastructure.Tabular;
using CircleSpaceCoordinator.Desktop.Windows.Persistence;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private void ChooseColumn(string title, IReadOnlyList<string> headers, int? selected, bool optional, Action<int?> accepted, Action back)
    {
        var labels = headers.Select((value, index) => $"{index + 1}: {(value.Length == 0 ? "（見出しなし）" : value)}");
        if (optional) labels = new[] { "（使用しない）" }.Concat(labels);
        OpenSelection(title, labels.ToArray(), optional ? (selected ?? -1) + 1 : selected ?? 0,
            index => accepted(optional ? index == 0 ? null : index - 1 : index), back);
    }

    private void OpenParticipantImport()
    {
        if (workspace is null) return;
        var owner = workspace;
        var path = WindowsTableFileDialog.Select(false, settings?.Current.ParticipantImportDirectory);
        if (path is null) return;
        settings?.RememberParticipantImportPath(path);
        var csv = Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase);
        IReadOnlyList<ParticipantTableSheet> sheets = [];
        var sheetIndex = 0;
        var encoding = ParticipantCsvEncoding.Utf8;
        var decodingErrors = false;
        var status = "";
        int?[] columns = [0, 0, null, null, null];
        string[] names = ["サークルID列", "サークル名列", "必要セル数列（任意）", "合体先ID列（任意）", "ジャンルID列（任意）"];
        void SelectSheet(int index)
        {
            sheetIndex = index;
            var guess = ParticipantTableMapper.Guess(sheets[index].Headers);
            columns = [guess.CircleIdColumn, guess.DisplayNameColumn, guess.RequiredCellCountColumn, guess.CombinedWithCircleIdColumn, guess.GenreIdColumn];
        }
        void Load()
        {
            var requestedEncoding = encoding;
            RunBackground("参加サークル一覧の読込み", () =>
            {
                if (!csv) return (Sheets: ParticipantTableReader.Read(path), Errors: false);
                var result = ParticipantTableReader.ReadCsvPreview(path, requestedEncoding);
                return (Sheets: (IReadOnlyList<ParticipantTableSheet>)new[] { result.Sheet }, Errors: result.HasDecodingErrors);
            }, result =>
            {
                sheets = result.Sheets.Where(sheet => sheet.Headers.Count > 0).ToArray();
                decodingErrors = result.Errors;
                status = decodingErrors ? "読めない文字があります。文字コードを切り替えてください。" : "";
                if (sheets.Count > 0) SelectSheet(0); else status = "列見出しのあるシートがありません。";
                ShowDraft();
            }, exception => { sheets = []; status = "読込み失敗：" + exception.GetBaseException().Message; ShowDraft(); });
        }
        void ShowDraft()
        {
            var labels = new List<string>();
            if (csv) labels.Add($"文字コード：{encoding}（選択して変更）");
            var offset = csv ? 1 : 0;
            labels.Add("シート：" + (sheets.Count == 0 ? "（未読込み）" : sheets[sheetIndex].Name));
            for (var i = 0; i < columns.Length; i++) labels.Add(names[i] + "：" +
                (sheets.Count > 0 && columns[i] is { } column ? $"{column + 1}: {sheets[sheetIndex].Headers.ElementAtOrDefault(column)}" : "（使用しない）"));
            labels.Add("プレビュー表を開く");
            labels.Add("この対応で取り込む");
            if (status.Length > 0) labels.Add(status + "（選択して全文）");
            if (csv) labels.Add("文字コードを変えても欠損文字は復元できません。元のExcelも確認してください。");
            OpenSelection("参加サークル一覧：" + Path.GetFileName(path), labels, 0, selected =>
            {
                if (csv && selected == 0)
                {
                    OpenSelection("CSVの文字コード", ["UTF-8（BOMあり／なし）", "Shift-JIS（CP932）"], encoding == ParticipantCsvEncoding.Utf8 ? 0 : 1,
                        index => { encoding = index == 0 ? ParticipantCsvEncoding.Utf8 : ParticipantCsvEncoding.ShiftJis; Load(); }, ShowDraft);
                    return;
                }
                var index = selected - offset;
                if (index >= 8) { OpenTextViewer("読込みについて", status + "\nExcelからCSVへの変換時に失われた文字は、元のExcelから読み直してください。", ShowDraft); return; }
                if (sheets.Count == 0) { ShowNotice("読込み", status, ShowDraft); return; }
                var sheet = sheets[sheetIndex];
                if (index == 0) { OpenSelection("シート", sheets.Select(item => item.Name).ToArray(), sheetIndex, chosen => { SelectSheet(chosen); ShowDraft(); }, ShowDraft); return; }
                if (index <= 5)
                {
                    var field = index - 1; ChooseColumn(names[field], sheet.Headers, columns[field], field >= 2,
                    chosen => { columns[field] = chosen; ShowDraft(); }, ShowDraft); return;
                }
                if (index == 6) { OpenTablePreview(sheet, ShowDraft); return; }
                if (decodingErrors) { ShowNotice("文字コード", "読み取れない文字があるため取り込めません。文字コードを切り替えてください。", ShowDraft); return; }
                try
                {
                    var mapping = new ParticipantColumnMapping(columns[0] ?? -1, columns[1] ?? -1, columns[2], columns[3], columns[4]);
                    RunBackground("取込み内容の確認", () => ParticipantTableMapper.Map(sheet, mapping), rows =>
                    {
                        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "参加サークル一覧の確認",
                            $"参加サークル {rows.Count} 件で一覧を更新します。\n一覧から消えたサークルの配置は解除されます。"), action =>
                        {
                            if (action != ModalDialogAction.Accept) { ShowDraft(); return; }
                            try
                            {
                                if (workspace != owner) throw new InvalidOperationException("対象のイベントが変わりました。");
                                var source = new ParticipantTableSource(Path.GetFileName(path), sheet.Name, sheet.Headers.ToArray(), ParticipantTableMapper.GetColumnKeys(sheet.Headers));
                                workspace.Execute(new ParticipantCatalogServiceReplaceParticipants(rows, source), selectedPlanEdit: false);
                                Log("participant_import", true, $"participants={rows.Count}");
                                ShowInAppMessage("取込み完了", $"参加サークル {rows.Count} 件を取り込みました。");
                            }
                            catch (Exception exception) { ShowNotice("取込みエラー", exception.Message, ShowDraft); }
                        }, [("キャンセル", ModalDialogAction.Cancel), ("取り込む", ModalDialogAction.Accept)]);
                    }, exception => ShowNotice("取込み内容", exception.Message, ShowDraft));
                }
                catch (Exception exception) { ShowNotice("取込み内容", exception.Message, ShowDraft); }
            });
        }
        Load();
    }

    private object? exportTargetOwner;
    private string? exportTargetPath;
    private ParticipantTableSheet? exportTargetSheet;
    private ParticipantCsvEncoding exportTargetEncoding;
    private int[] exportTargetColumns = [0, 0, 0];
    private CircleSpaceCoordinator.Desktop.Core.Interaction.ParticipantTableView outputTable = new([], [], "出力先：未選択");

    private void EnsureExportTargetOwner()
    {
        if (ReferenceEquals(exportTargetOwner, workspace)) return;
        exportTargetOwner = workspace;
        exportTargetPath = null;
        exportTargetSheet = null;
        outputTable = new([], [], "出力先：未選択");
        tableTextPage = null;
    }

    private void SetOutputTable(string path, ParticipantTableSheet sheet)
    {
        exportTargetPath = path;
        exportTargetSheet = sheet;
        outputTable = new(sheet.Headers, sheet.Rows, $"出力先：{path}　シート：{sheet.Name}");
        tableTextPage = null;
        MoveParticipantTable(0, 0);
    }

    private void OpenExportTarget()
    {
        EnsureExportTargetOwner();
        var owner = workspace;
        var path = WindowsTableFileDialog.Select(true, settings?.Current.CircleSeatExportDirectory);
        if (path is null) return;
        var csv = Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase);
        void Load(ParticipantCsvEncoding encoding)
        {
            RunBackground("出力先の読込み", () => ParticipantTableReader.Read(path, encoding), sheets =>
            {
                if (workspace != owner) return;
                if (sheets.Count == 0 || sheets.All(sheet => sheet.Headers.Count == 0))
                {
                    ShowInAppMessage("出力先", "列見出しのあるシートがありません。");
                    return;
                }
                void SelectSheet(int index)
                {
                    var sheet = sheets[index];
                    exportTargetEncoding = encoding;
                    string[][] hints = [["ブロック"], ["セル番", "席"], ["サークルID", "circle", "id"]];
                    exportTargetColumns = hints.Select(hint => Math.Max(0, sheet.Headers.ToList().FindIndex(header => hint.Any(word => header.Contains(word, StringComparison.OrdinalIgnoreCase))))).ToArray();
                    SetOutputTable(path, sheet);
                    settings?.RememberCircleSeatExportPath(path);
                    ApplyModalAction(ModalDialogAction.Accept);
                }
                if (sheets.Count == 1) SelectSheet(0);
                else OpenSelection("出力先のシート", sheets.Select(sheet => sheet.Name).ToArray(), 0, SelectSheet);
            });
        }
        if (csv)
            OpenSelection("出力先 CSV の文字コード", ["UTF-8（BOMあり／なし）", "Shift-JIS（CP932）"], 0,
                index => Load(index == 0 ? ParticipantCsvEncoding.Utf8 : ParticipantCsvEncoding.ShiftJis));
        else Load(ParticipantCsvEncoding.Utf8);
    }

    private void OpenSeatExport()
    {
        EnsureExportTargetOwner();
        if (exportTargetPath is null || exportTargetSheet is null)
        {
            OpenExportTarget();
            return;
        }
        var rows = BuildCircleSeatExportRows();
        var path = exportTargetPath;
        var sheet = exportTargetSheet;
        var encoding = exportTargetEncoding;
        var columns = exportTargetColumns.ToArray();
        string[] names = ["ブロック番号列", "セル番列", "サークルID列"];
        void ShowDraft() => OpenSelection("出力先へ書き出し：" + Path.GetFileName(path),
            names.Select((name, index) => $"{name}：{columns[index] + 1}: {sheet.Headers[columns[index]]}")
                .Concat(["この対応で書き出す"]).ToArray(), 0, index =>
        {
            if (index < 3)
            {
                ChooseColumn(names[index], sheet.Headers, columns[index], false,
                    chosen => { columns[index] = chosen!.Value; ShowDraft(); }, ShowDraft);
                return;
            }
            OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "出力先への書出し",
                $"{path}\nシート：{sheet.Name}\n選択したファイルを更新します。\n配置済みでセル番のあるサークルを書き出します。該当しない行は変更しません。"), action =>
            {
                if (action != ModalDialogAction.Accept) { ShowDraft(); return; }
                RunBackground("出力先へ書き出しています", () =>
                {
                    var result = CircleSeatExcelExporter.Export(path, sheet.Name, columns[0], columns[1], columns[2], rows, encoding);
                    return result;
                }, result =>
                {
                    exportTargetColumns = columns;
                    Log("seat_export", true);
                    RunBackground("出力先の表を更新", () => ParticipantTableReader.Read(path, encoding).Single(item => item.Name == sheet.Name),
                        updated =>
                        {
                            SetOutputTable(path, updated);
                            ShowInAppMessage("書出し完了", $"{result.UpdatedRowCount} 行へ書き出しました。\n出力先で見つからないサークルID: {result.MissingCircleCount} 件");
                        }, exception => ShowInAppMessage("書出し完了・再読込み失敗", "ファイルへの書出しは完了しました。\n" + exception.Message));
                }, exception => ShowNotice("書出しエラー", exception.Message, ShowDraft));
            }, [("キャンセル", ModalDialogAction.Cancel), ("書き出す", ModalDialogAction.Accept)]);
        });
        ShowDraft();
    }
}
