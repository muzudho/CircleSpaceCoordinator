namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Desktop.Windows.Persistence;
using CircleSpaceCoordinator.Infrastructure.Tabular;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private object? exportTargetOwner;
    private string? exportTargetPath;
    private ParticipantTableSheet? exportTargetSheet;
    private ParticipantCsvEncoding exportTargetEncoding;
    private int[] exportTargetColumns = [-1, -1, -1];
    private bool exportNewFile;
    private bool exportColumnsConfirmed;
    private CircleSpaceProject? exportPreviewSourceProject;
    private CircleSpaceProject? exportNewSourceProject;
    private PreparedCircleSeatTable? preparedExport;
    private string exportPreviewStatus = "出力先を選択してください。";
    private ParticipantTableView outputTable = new([], [], "出力先：未選択");

    private void EnsureExportTargetOwner()
    {
        if (ReferenceEquals(exportTargetOwner, workspace)) return;
        exportTargetOwner = workspace;
        exportTargetPath = null;
        exportTargetSheet = null;
        exportColumnsConfirmed = false;
        exportPreviewSourceProject = null;
        exportNewSourceProject = null;
        preparedExport = null;
        outputTable = new([], [], "出力先：未選択");
        exportPreviewStatus = "出力先を選択してください。";
        tableTextPage = null;
    }

    private void RefreshExportPreview()
    {
        EnsureExportTargetOwner();
        if (workspace is null || exportTargetSheet is null || ReferenceEquals(exportPreviewSourceProject, workspace.Project)) return;
        exportPreviewSourceProject = workspace.Project;
        preparedExport = null;
        tableTextPage = null;
        exportPreviewStatus = "［出力列］で書き込む列を確認してください。";
        if (exportNewFile && exportNewSourceProject is { } source && !CircleSeatSourceTable.HasSameValues(source, workspace.Project))
        {
            exportColumnsConfirmed = false;
            exportPreviewStatus = "入力データが更新されました。［出力先］から新規書出しを選び直してください。";
        }
        else if (exportColumnsConfirmed)
        {
            try
            {
                var rows = CircleSeatExportBuilder.BuildDecided(workspace.Project);
                preparedExport = CircleSeatTableWriter.Prepare(exportTargetSheet, exportTargetColumns[0], exportTargetColumns[1], exportTargetColumns[2], rows);
                var errors = rows.Count(row => row.SeatName == CircleSeatExportBuilder.UndefinedSpaceNumber || row.BlockName == "#MISSING_BLOCK_NUMBER");
                exportPreviewStatus = $"書出し予定：{preparedExport.Result.UpdatedRowCount} 行 ／ 出力先にないID：{preparedExport.Result.MissingCircleCount} 件 ／ エラー番号：{errors} 件";
            }
            catch (InvalidOperationException exception) { exportPreviewStatus = exception.Message.Replace('\n', ' '); }
        }
        var sheet = preparedExport?.Sheet ?? exportTargetSheet;
        outputTable = new(sheet.Headers, sheet.Rows, $"{(exportNewFile ? "新規" : "既存")}：{exportTargetPath}　シート：{sheet.Name}　{(preparedExport is null ? "元の表" : "書出しプレビュー（未保存）")}");
    }

    private void SetExportTarget(string path, ParticipantTableSheet sheet, ParticipantCsvEncoding encoding, bool newFile, int[] columns)
    {
        exportTargetPath = path;
        exportTargetSheet = sheet;
        exportTargetEncoding = encoding;
        exportNewFile = newFile;
        exportTargetColumns = columns;
        exportColumnsConfirmed = false;
        exportPreviewSourceProject = null;
        exportNewSourceProject = newFile ? workspace!.Project : null;
        settings?.RememberCircleSeatExportPath(path);
        RefreshExportPreview();
        MoveParticipantTable(0, 0);
        OpenExportColumns();
    }

    private void OpenExportTarget()
    {
        EnsureExportTargetOwner();
        OpenSelection("書出し方法", ["新しいファイルに書き出す（読み込み済みのサークル表を使用）", "既存のファイルに書き出す（サークルIDで照合）"], 0, index =>
        {
            if (index == 0)
            {
                var path = WindowsTableFileDialog.SelectNew(settings?.Current.CircleSeatExportDirectory);
                if (path is null) return;
                if (File.Exists(path)) { ShowInAppMessage("新規書出し", "同名のファイルが存在します。別名を選ぶか、既存のファイルへの書出しを選んでください。"); return; }
                var source = CircleSeatSourceTable.Build(workspace!.Project);
                SetExportTarget(path, source.Sheet, ParticipantCsvEncoding.Utf8, true, [source.Block, source.Seat, source.Circle]);
                Log("seat_export_new_selected", true);
                return;
            }
            var existingPath = WindowsTableFileDialog.Select(true, settings?.Current.CircleSeatExportDirectory);
            if (existingPath is null) return;
            void Load(ParticipantCsvEncoding encoding) => RunBackground("出力先の読込み", () => ParticipantTableReader.Read(existingPath, encoding), sheets =>
            {
                var valid = sheets.Where(sheet => sheet.Headers.Count > 0).ToArray();
                if (valid.Length == 0) { ShowInAppMessage("出力先", "列見出しのあるシートがありません。"); return; }
                void SelectSheet(int selected)
                {
                    var sheet = valid[selected];
                    string[][] hints = [["ブロック"], ["セル番", "席"], ["サークルID", "circle", "id"]];
                    var columns = hints.Select(hint => sheet.Headers.ToList().FindIndex(header => hint.Any(word => header.Contains(word, StringComparison.OrdinalIgnoreCase)))).ToArray();
                    SetExportTarget(existingPath, sheet, encoding, false, columns);
                    Log("seat_export_existing_selected", true);
                }
                if (valid.Length == 1) SelectSheet(0);
                else OpenSelection("出力先のシート", valid.Select(sheet => sheet.Name).ToArray(), 0, SelectSheet);
            });
            if (Path.GetExtension(existingPath).Equals(".csv", StringComparison.OrdinalIgnoreCase))
                OpenSelection("出力先 CSV の文字コード", ["UTF-8（BOMあり／なし）", "Shift-JIS（CP932）"], 0,
                    chosen => Load(chosen == 0 ? ParticipantCsvEncoding.Utf8 : ParticipantCsvEncoding.ShiftJis));
            else Load(ParticipantCsvEncoding.Utf8);
        });
    }

    private string ExportColumnDescription(int column) => exportTargetSheet is { } sheet && column >= 0 && column < sheet.Headers.Count
        ? $"{column + 1}: {sheet.Headers[column]}" : "未選択";

    private string ExportColumnsSummary => $"書込先　ブロック番号 → {ExportColumnDescription(exportTargetColumns[0])}　／　セル番 → {ExportColumnDescription(exportTargetColumns[1])}　／　照合ID → {ExportColumnDescription(exportTargetColumns[2])}";

    private void OpenSeatExport()
    {
        RefreshExportPreview();
        if (exportTargetSheet is null || exportTargetPath is null || preparedExport is null)
        {
            ShowInAppMessage("書出し準備", exportPreviewStatus);
            return;
        }
        var path = exportTargetPath;
        var sheet = exportTargetSheet;
        var prepared = preparedExport;
        var encoding = exportTargetEncoding;
        var columns = exportTargetColumns.ToArray();
        var newFile = exportNewFile;
        var rows = CircleSeatExportBuilder.BuildDecided(workspace!.Project);
        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "書出し内容の確認",
            $"{(newFile ? "新しいファイルを作成" : "既存のファイルを更新")}：{path}\n{ExportColumnsSummary}\n{exportPreviewStatus}\n{(newFile ? "読み込み済みの表の値を保存します。元ブックの書式・他シートは含みません。" : "照合した行の指定列を更新します。その他の行は変更しません。")}"), action =>
        {
            if (action != ModalDialogAction.Accept) return;
            RunBackground("書き出しています", () =>
            {
                if (newFile) { CircleSeatTableWriter.Create(path, prepared.Sheet); return prepared.Result; }
                return CircleSeatExcelExporter.Export(path, sheet.Name, columns[0], columns[1], columns[2], rows, encoding, sheet.Headers);
            }, result =>
            {
                Log(newFile ? "seat_export_new_complete" : "seat_export_existing_complete", true);
                RunBackground("保存した表の再読込み", () => ParticipantTableReader.Read(path, encoding), loaded =>
                {
                    exportTargetSheet = newFile ? loaded[0] : loaded.Single(item => item.Name == sheet.Name);
                    exportNewFile = false;
                    exportNewSourceProject = null;
                    exportPreviewSourceProject = workspace!.Project;
                    outputTable = new(exportTargetSheet.Headers, exportTargetSheet.Rows, $"保存済み：{path}　シート：{exportTargetSheet.Name}");
                    exportPreviewStatus = $"書出し完了：{result.UpdatedRowCount} 行 ／ 見つからないID：{result.MissingCircleCount} 件";
                    tableTextPage = null;
                    ShowInAppMessage("書出し完了", exportPreviewStatus);
                }, exception => ShowInAppMessage("書出し完了・再読込み失敗", "ファイルへの保存は完了しています。［出力先］から選び直してください。\n" + exception.Message));
            }, exception =>
            {
                Log("seat_export_failed", false);
                ShowInAppMessage("書出しエラー", exception.Message);
            });
        }, [("キャンセル", ModalDialogAction.Cancel), ("書き出す", ModalDialogAction.Accept)]);
    }
}
