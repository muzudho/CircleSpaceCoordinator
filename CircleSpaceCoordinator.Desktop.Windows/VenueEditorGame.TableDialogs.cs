namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Infrastructure.Tabular;
using CircleSpaceCoordinator.Desktop.Windows.Persistence;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private void OpenParticipantImport()
    {
        if (workspace is null) return;
        var path = WindowsTableFileDialog.Select(false, CurrentParticipantImportDirectory);
        if (path is null) { OpenProjectProfile(ProjectProfileTab.CircleData); return; }
        settings?.RememberParticipantImportPath(path);
        var csv = Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase);
        IReadOnlyList<ParticipantTableSheet> sheets = [];
        var sheetIndex = 0;
        var encoding = ParticipantCsvEncoding.Utf8;
        var decodingErrors = false;
        var status = "";

        void StartMapping()
        {
            if (sheets.Count == 0 || decodingErrors) { ShowDraft(); return; }
            // The reader's progress modal is still open when its callback runs.
            ApplyModalAction(ModalDialogAction.Accept);
            var result = OpenChannelColumnMapping("列名と既定のチャンネルを対応付け、［この対応で取り込む］を押してください。",
                sheets[sheetIndex], path, csv ? encoding : null);
            if (result == System.Windows.Forms.DialogResult.Retry)
            {
                encoding = encoding == ParticipantCsvEncoding.Utf8 ? ParticipantCsvEncoding.ShiftJis : ParticipantCsvEncoding.Utf8;
                Load();
            }
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
                status = decodingErrors ? "読めない文字があります。文字コードを切り替えてください。" :
                    sheets.Count == 0 ? "列見出しのあるシートがありません。" : "";
                if (sheets.Count == 1 && !decodingErrors) StartMapping();
                else ShowDraft();
            }, exception => { sheets = []; status = "読込み失敗：" + exception.GetBaseException().Message; ShowDraft(); });
        }

        void ShowDraft()
        {
            var labels = new List<string>();
            if (csv) labels.Add($"文字コード：{encoding}（選択して変更）");
            var offset = csv ? 1 : 0;
            labels.Add("シート：" + (sheets.Count == 0 ? "（未読込み）" : sheets[sheetIndex].Name));
            labels.Add("プレビュー表を開く");
            labels.Add("列とチャンネルを対応付ける");
            if (status.Length > 0) labels.Add(status + "（選択して全文）");
            if (csv) labels.Add("文字コードを変えても欠損文字は復元できません。元のExcelも確認してください。");
            OpenSelection("参加サークル一覧：" + Path.GetFileName(path), labels, 0, selected =>
            {
                if (csv && selected == 0)
                {
                    OpenSelection("CSVの文字コード", ["UTF-8（BOMあり／なし）", "Shift-JIS（CP932）"],
                        encoding == ParticipantCsvEncoding.Utf8 ? 0 : 1,
                        index => { encoding = index == 0 ? ParticipantCsvEncoding.Utf8 : ParticipantCsvEncoding.ShiftJis; Load(); }, ShowDraft);
                    return;
                }
                var index = selected - offset;
                if (sheets.Count == 0) { ShowNotice("読込み", status, ShowDraft); return; }
                if (index == 0)
                {
                    OpenSelection("シート", sheets.Select(item => item.Name).ToArray(), sheetIndex,
                        chosen => { sheetIndex = chosen; StartMapping(); }, ShowDraft);
                    return;
                }
                if (index == 1) { OpenTablePreview(sheets[sheetIndex], ShowDraft); return; }
                if (index == 2)
                {
                    if (decodingErrors) ShowNotice("文字コード", "読み取れない文字があるため取り込めません。文字コードを切り替えてください。", ShowDraft);
                    else StartMapping();
                    return;
                }
                OpenTextViewer("読込みについて", status + "\nExcelからCSVへの変換時に失われた文字は、元のExcelから読み直してください。", ShowDraft);
            });
        }
        Load();
    }
}
