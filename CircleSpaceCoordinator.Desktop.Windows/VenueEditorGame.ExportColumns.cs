namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Interaction;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private ExportColumnDraft? exportColumnDraft;
    private int exportColumnScroll;
    private int exportColumnSelected;
    private int exportColumnField;
    private int exportDragColumn = -1;
    private int exportDragField = -1;
    private ScreenPoint exportDragStart;

    private void OpenExportColumns()
    {
        EnsureExportTargetOwner();
        if (exportTargetSheet is not { } sheet) { OpenExportTarget(); return; }
        var draft = new ExportColumnDraft(sheet, exportTargetColumns);
        var owner = workspace;
        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "出力列の割り当て（保存前の設定）", ""), action =>
        {
            if (action != ModalDialogAction.Accept || workspace != owner) return;
            var result = draft.Build();
            exportTargetSheet = result.Sheet;
            exportTargetColumns = result.Columns;
            exportColumnsConfirmed = true;
            exportPreviewSourceProject = null;
            RefreshExportPreview();
            Log("seat_export_columns_confirmed", true);
        }, [("キャンセル", ModalDialogAction.Cancel), ("プレビューに反映", ModalDialogAction.Accept)]);
        exportColumnDraft = draft;
        exportColumnScroll = 0;
        exportColumnSelected = 0;
        exportColumnField = 0;
        exportDragColumn = exportDragField = -1;
    }

    private ScreenRectangle ExportColumnListBounds()
    {
        var panel = ModalBounds();
        return new(panel.X + 20, panel.Y + 94, (panel.Width - 64) / 2, Math.Max(32, panel.Height - 216));
    }

    private ScreenRectangle ExportColumnSlot(int field)
    {
        var panel = ModalBounds();
        return new(panel.X + panel.Width / 2 + 12, panel.Y + 110 + field * 90, (panel.Width - 64) / 2, 56);
    }

    private ScreenRectangle ExportColumnAddButton(int field)
    {
        var slot = ExportColumnSlot(2);
        return new(slot.X, slot.Y + 76 + field * 36, slot.Width, 30);
    }

    private bool UpdateExportColumns(KeyboardState keyboard, MouseState mouse)
    {
        if (exportColumnDraft is not { } draft) return false;
        var list = ExportColumnListBounds();
        var page = Math.Max(1, (int)(list.Height / 32));
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        if (exportDragColumn >= 0 && IsPressed(keyboard, Keys.Escape))
        {
            exportDragColumn = exportDragField = -1;
            return true;
        }
        var delta = IsPressed(keyboard, Keys.Down) ? 1 : IsPressed(keyboard, Keys.Up) ? -1
            : IsPressed(keyboard, Keys.PageDown) ? page : IsPressed(keyboard, Keys.PageUp) ? -page : 0;
        if (IsPressed(keyboard, Keys.Home)) delta = -draft.Headers.Count;
        if (IsPressed(keyboard, Keys.End)) delta = draft.Headers.Count;
        exportColumnSelected = Math.Clamp(exportColumnSelected + delta, 0, Math.Max(0, draft.Headers.Count - 1));
        if (delta != 0) exportColumnScroll = Math.Clamp(exportColumnScroll, Math.Max(0, exportColumnSelected - page + 1), exportColumnSelected);
        if (Contains(list, pointer))
            exportColumnScroll -= Math.Sign(mouse.ScrollWheelValue - previousMouse.ScrollWheelValue) * 3;
        exportColumnScroll = Math.Clamp(exportColumnScroll, 0, Math.Max(0, draft.Headers.Count - page));
        for (var field = 0; field < 3; field++)
            if (IsPressed(keyboard, (Keys)((int)Keys.D1 + field)) && draft.Headers.Count > 0)
            {
                exportColumnField = field;
                draft.Assign(field, exportColumnSelected);
            }
        if (IsPressed(keyboard, Keys.Delete)) draft.Assign(exportColumnField, -1);
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            for (var field = 0; field < 2; field++)
                if (Contains(ExportColumnAddButton(field), pointer)) { draft.AddNumberColumn(field); return true; }
            for (var field = 0; field < 3; field++)
            {
                var slot = ExportColumnSlot(field);
                if (!Contains(slot, pointer)) continue;
                exportColumnField = field;
                if (pointer.X >= slot.X + slot.Width - 40) { draft.Assign(field, -1); return true; }
                exportDragColumn = draft.Columns[field];
                exportDragField = field;
                exportDragStart = pointer;
                return true;
            }
            var row = exportColumnScroll + (int)((pointer.Y - list.Y) / 32);
            if (Contains(list, pointer) && pointer.Y < list.Y + page * 32 && row < draft.Headers.Count)
            {
                exportColumnSelected = row;
                exportDragColumn = row;
                exportDragField = -1;
                exportDragStart = pointer;
                return true;
            }
        }
        if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed && exportDragColumn >= 0)
        {
            var destination = Enumerable.Range(0, 3).FirstOrDefault(field => Contains(ExportColumnSlot(field), pointer), -1);
            if (destination >= 0) { draft.Assign(destination, exportDragColumn); exportColumnField = destination; }
            else if (exportDragField >= 0 && (Math.Abs(pointer.X - exportDragStart.X) > 4 || Math.Abs(pointer.Y - exportDragStart.Y) > 4))
                draft.Assign(exportDragField, -1);
            exportDragColumn = exportDragField = -1;
            return true;
        }
        return false;
    }

    private void DrawExportColumns()
    {
        if (exportColumnDraft is not { } draft) return;
        var panel = ModalBounds();
        var list = ExportColumnListBounds();
        textRenderer?.Draw("列を右の用途へドラッグ。外すときは左へ戻すか［×］。", ToRectangle(new ScreenRectangle(panel.X + 20, panel.Y + 56, panel.Width - 40, 28)), Color.LightGray, 16);
        DrawRectangle(list, new Color(16, 22, 29));
        var page = Math.Max(1, (int)(list.Height / 32));
        for (var row = 0; row < page && exportColumnScroll + row < draft.Headers.Count; row++)
        {
            var column = exportColumnScroll + row;
            var rect = new ScreenRectangle(list.X, list.Y + row * 32, list.Width, 30);
            if (column == exportColumnSelected) DrawRectangle(rect, new Color(45, 95, 100));
            var mark = column >= draft.OriginalColumnCount ? "［追加］" : "";
            if (draft.Columns.Contains(column)) mark += "［割当済］";
            textRenderer?.Draw($"{column + 1}: {draft.Headers[column]} {mark}", ToRectangle(rect, 4), Color.White, 15);
        }
        string[] labels = ["1　ブロック番号の書込先", "2　セル番号／フレーム番号の書込先", "3　照合するサークルID列"];
        for (var field = 0; field < 3; field++)
        {
            var slot = ExportColumnSlot(field);
            var column = draft.Columns[field];
            DrawRectangle(slot, exportColumnField == field ? new Color(45, 95, 100) : new Color(36, 48, 58));
            textRenderer?.Draw(labels[field], ToRectangle(new ScreenRectangle(slot.X + 5, slot.Y + 2, slot.Width - 45, 23)), Color.LightGray, 14);
            textRenderer?.Draw(column >= 0 ? $"{column + 1}: {draft.Headers[column]}" : "ここへ列をドロップ", ToRectangle(new ScreenRectangle(slot.X + 5, slot.Y + 27, slot.Width - 45, 25)), Color.White, 16, true);
            textRenderer?.Draw("×", ToRectangle(new ScreenRectangle(slot.X + slot.Width - 38, slot.Y, 36, slot.Height)), Color.White, 22);
        }
        for (var field = 0; field < 2; field++)
        {
            var rect = ExportColumnAddButton(field);
            DrawRectangle(rect, new Color(68, 87, 58));
            textRenderer?.Draw(field == 0 ? "＋ 新規列［ブロック番号］を追加" : "＋ 新規列［セル番号］を追加", ToRectangle(rect, 4), Color.White, 15);
        }
        textRenderer?.Draw($"{exportColumnSelected + 1} / {draft.Headers.Count}　↑↓・ホイールで移動　1 / 2 / 3：割当　Delete：解除", ToRectangle(new ScreenRectangle(panel.X + 20, panel.Y + panel.Height - 110, panel.Width - 40, 23)), Color.LightGray, 13);
        textRenderer?.Draw(draft.IsComplete ? "［プレビューに反映］で設定を適用します。ファイルはまだ変更しません。" : "３つの用途に別々の列を割り当ててください。", ToRectangle(new ScreenRectangle(panel.X + 20, panel.Y + panel.Height - 85, panel.Width - 40, 23)), Color.LightGray, 14);
        if (exportDragColumn >= 0)
        {
            var pointer = Mouse.GetState();
            var ghost = new ScreenRectangle(pointer.X + 12, pointer.Y + 12, 240, 32);
            DrawRectangle(ghost, new Color(60, 125, 128));
            textRenderer?.Draw(draft.Headers[exportDragColumn], ToRectangle(ghost, 4), Color.White, 16);
        }
    }
}
