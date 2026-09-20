namespace CircleSpaceCoordinator.Desktop.Windows;

using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private bool genreExitConfirmationOpen;

    private bool TryExitGenreScope()
    {
        // Repeated window-close requests must not replace an active input dialog.
        if (modalDialog is not null || mappingComposition.Length > 0) return false;
        if (!SaveGenreScope()) { ShowGenreExitSaveError(); return false; }
        SyncMappingChangeTag();
        if (MappingCommentsCanClose) return FinishGenreScope();
        var missing = new List<string>();
        if (mappingChangeTag?.CanClose == false) missing.Add("プロジェクト");
        if (packageChangeTag?.CanClose == false) missing.Add("パッケージ");
        genreExitConfirmationOpen = true;
        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "変更コメントを確認して終了",
            string.Join("と", missing) + "の変更コメントが未入力のままです。\n巻き戻す場合は、プロジェクト・パッケージの両方を編集開始時点へ戻して保存します。"), action =>
        {
            genreExitConfirmationOpen = false;
            if (action == ModalDialogAction.Accept)
            {
                DiscardGenreSide(project: true);
                DiscardGenreSide(project: false);
                genreCloseAfterDiscard = false;
                if (!SaveGenreScope() || !FlushAutoSave()) { ShowGenreExitSaveError(); return; }
                CloseStyleMappingEditor();
                Exit();
            }
            else if (action == ModalDialogAction.Increase) ContinueGenreCommentExit();
        }, [("変更を巻き戻してアプリケーションを終了する", ModalDialogAction.Accept),
            ("変更コメントを入力してアプリケーションを終了する", ModalDialogAction.Increase),
            ("キャンセル", ModalDialogAction.Cancel)]);
        return false;
    }

    private void ContinueGenreCommentExit()
    {
        if (!SaveGenreScope()) { ShowGenreExitSaveError(); return; }
        SyncMappingChangeTag();
        if (mappingChangeTag?.CanClose == false)
            OpenGenreChangeComment(project: true, accepted: ContinueGenreCommentExit);
        else if (packageChangeTag?.CanClose == false)
            OpenGenreChangeComment(project: false, accepted: ContinueGenreCommentExit);
        else if (FinishGenreScope()) Exit();
    }

    private void ShowGenreExitSaveError() => ShowInAppMessage("保存できないため終了しません",
        genreSaveError ?? autoSaveError ?? "保存に失敗しました。内容を保持しています。保存先を確認してから、終了をやり直してください。");
}
