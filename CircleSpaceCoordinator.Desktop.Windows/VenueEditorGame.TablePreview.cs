namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Infrastructure.Tabular;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private ParticipantTableSheet? previewSheet;
    private Action? previewBack;
    private int previewRow;
    private int previewColumn;
    private int PreviewRows => Math.Max(1, (int)(SelectionArea().Height / 28) - 1);
    private int PreviewColumns => Math.Max(1, (int)(SelectionArea().Width / 160));

    private void OpenTablePreview(ParticipantTableSheet sheet, Action back, int row = 0, int column = 0)
    {
        OpenModal(new ModalDialogModel(ModalDialogKind.Message, "プレビュー：" + sheet.Name, ""), _ => back());
        previewSheet = sheet;
        previewBack = back;
        previewRow = row;
        previewColumn = column;
    }

    private bool UpdateTablePreview(KeyboardState keyboard, MouseState mouse)
    {
        if (previewSheet is not { } sheet) return false;
        var wheel = mouse.ScrollWheelValue - previousMouse.ScrollWheelValue;
        if (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift))
        { previewColumn = Math.Clamp(previewColumn + (wheel > 0 ? -1 : wheel < 0 ? 1 : 0), 0, Math.Max(0, sheet.Headers.Count - PreviewColumns)); wheel = 0; }
        previewRow = Math.Clamp(previewRow + (wheel > 0 ? -3 : wheel < 0 ? 3 : 0)
            + (IsPressed(keyboard, Keys.Down) ? 1 : IsPressed(keyboard, Keys.Up) ? -1 : 0)
            + (IsPressed(keyboard, Keys.PageDown) ? PreviewRows : IsPressed(keyboard, Keys.PageUp) ? -PreviewRows : 0),
            0, Math.Max(0, sheet.Rows.Count - PreviewRows));
        previewColumn = Math.Clamp(previewColumn + (IsPressed(keyboard, Keys.Right) ? 1 : IsPressed(keyboard, Keys.Left) ? -1 : 0),
            0, Math.Max(0, sheet.Headers.Count - PreviewColumns));
        if (IsPressed(keyboard, Keys.Home)) previewRow = previewColumn = 0;
        if (IsPressed(keyboard, Keys.End)) { previewRow = Math.Max(0, sheet.Rows.Count - PreviewRows); previewColumn = Math.Max(0, sheet.Headers.Count - PreviewColumns); }
        var point = new ScreenPoint(mouse.X, mouse.Y);
        var area = SelectionArea();
        if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed && Contains(area, point))
        {
            var row = previewRow + (int)((point.Y - area.Y) / 28) - 1;
            var column = previewColumn + (int)((point.X - area.X) / (area.Width / PreviewColumns));
            if (row >= previewRow && row < previewRow + PreviewRows && row < sheet.Rows.Count && column < sheet.Headers.Count)
            {
                var back = previewBack!;
                var savedRow = previewRow;
                var savedColumn = previewColumn;
                OpenTextViewer($"{row + 1} 行 / {column + 1} 列：{sheet.Headers[column]}",
                    sheet.Rows[row].ElementAtOrDefault(column) ?? "", () => OpenTablePreview(sheet, back, savedRow, savedColumn));
                return true;
            }
        }
        return false;
    }

    private void DrawTablePreview()
    {
        if (previewSheet is not { } sheet) return;
        var area = SelectionArea();
        var width = area.Width / PreviewColumns;
        for (var row = -1; row < PreviewRows && previewRow + row < sheet.Rows.Count; row++)
            for (var column = 0; column < PreviewColumns && previewColumn + column < sheet.Headers.Count; column++)
            {
                var index = previewColumn + column;
                var bounds = new ScreenRectangle(area.X + column * width, area.Y + (row + 1) * 28, width, 28);
                DrawOutline(bounds, 1, Color.Gray);
                var value = row < 0 ? $"{index + 1}: {sheet.Headers[index]}" : sheet.Rows[previewRow + row].ElementAtOrDefault(index) ?? "";
                // Full text remains available by opening the cell; keep preview rendering bounded.
                var elements = System.Globalization.StringInfo.ParseCombiningCharacters(value);
                if (elements.Length > 24) value = value[..elements[24]] + "…";
                textRenderer?.Draw(value.Replace('\n', ' ').Replace('\r', ' '), ToRectangle(bounds, 3), row < 0 ? Color.LightGreen : Color.White, 16);
            }
        var panel = ModalBounds();
        textRenderer?.Draw($"行 {previewRow + 1} / {sheet.Rows.Count}　列 {previewColumn + 1} / {sheet.Headers.Count}　矢印・PgUp/PgDn、セルで全文",
            ToRectangle(new ScreenRectangle(area.X, panel.Y + panel.Height - 80, area.Width, 20)), Color.LightGray, 12);
    }
}
