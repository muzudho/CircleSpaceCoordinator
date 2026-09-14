namespace CircleSpaceCoordinator.Desktop.Windows;

using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private string[]? viewerLines;
    private CircleSpaceCoordinator.Desktop.Core.Interaction.ParticipantCellText? viewerDocument;
    private int viewerTop;
    private int viewerLeft;
    private (int Row, int Column) viewerAnchor;
    private (int Row, int Column) viewerEnd;
    private bool viewerDragging;
    private string? viewerStatus;

    private void OpenTextViewer(string title, string text, Action? back = null)
    {
        OpenModal(new ModalDialogModel(ModalDialogKind.Message, title, ""), _ => back?.Invoke());
        viewerDocument = new(text);
        viewerLines = viewerDocument.Lines.ToArray();
        viewerTop = viewerLeft = 0;
        viewerAnchor = viewerEnd = (0, 0);
        viewerDragging = false;
        viewerStatus = null;
    }

    private int ViewerRows => Math.Max(1, (int)(SelectionArea().Height / 24));
    private string ViewerSlice(int lineIndex, out int start)
    {
        var line = viewerLines![lineIndex];
        var boundaries = viewerDocument!.Boundaries(lineIndex);
        start = boundaries[Math.Min(viewerLeft, boundaries.Length - 1)];
        var from = start;
        var width = SelectionArea().Width - 8;
        var low = Math.Min(viewerLeft, boundaries.Length - 1);
        var high = Math.Min(boundaries.Length - 1, low + 1024);
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if ((textRenderer?.Measure(line[from..boundaries[middle]], 18).X ?? 0) <= width) low = middle;
            else high = middle - 1;
        }
        return line[from..boundaries[low]];
    }

    private (int Row, int Column) ViewerPosition(ScreenPoint point)
    {
        var area = SelectionArea();
        var row = Math.Clamp(viewerTop + (int)((point.Y - area.Y) / 24), 0, viewerLines!.Length - 1);
        var slice = ViewerSlice(row, out var start);
        var column = StringInfo.ParseCombiningCharacters(slice).Append(slice.Length)
            .MinBy(index => Math.Abs(area.X + 4 + (textRenderer?.Measure(slice[..index], 18).X ?? 0) - point.X));
        return (row, start + column);
    }

    private void UpdateTextViewer(KeyboardState keyboard, MouseState mouse)
    {
        if (viewerLines is null) return;
        var wheel = mouse.ScrollWheelValue - previousMouse.ScrollWheelValue;
        var horizontalWheel = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        if (horizontalWheel) { viewerLeft = Math.Clamp(viewerLeft + (wheel > 0 ? -8 : wheel < 0 ? 8 : 0), 0, viewerLines.Max(line => line.Length)); wheel = 0; }
        viewerTop = Math.Clamp(viewerTop + (wheel == 0 ? 0 : wheel > 0 ? -3 : 3)
            + (IsPressed(keyboard, Keys.Down) ? 1 : IsPressed(keyboard, Keys.Up) ? -1 : 0)
            + (IsPressed(keyboard, Keys.PageDown) ? ViewerRows : IsPressed(keyboard, Keys.PageUp) ? -ViewerRows : 0),
            0, Math.Max(0, viewerLines.Length - ViewerRows));
        if (IsPressed(keyboard, Keys.Home)) viewerLeft = 0;
        if (IsPressed(keyboard, Keys.End)) viewerTop = Math.Max(0, viewerLines.Length - ViewerRows);
        viewerLeft = Math.Clamp(viewerLeft + (IsPressed(keyboard, Keys.Right) ? 8 : IsPressed(keyboard, Keys.Left) ? -8 : 0),
            0, viewerLines.Max(line => line.Length));
        var point = new ScreenPoint(mouse.X, mouse.Y);
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released && Contains(SelectionArea(), point))
        {
            viewerEnd = ViewerPosition(point);
            if (!keyboard.IsKeyDown(Keys.LeftShift) && !keyboard.IsKeyDown(Keys.RightShift)) viewerAnchor = viewerEnd;
            viewerDragging = true;
        }
        if (viewerDragging) viewerEnd = ViewerPosition(point);
        if (mouse.LeftButton == ButtonState.Released) viewerDragging = false;
        if (IsControlDown(keyboard) && IsPressed(keyboard, Keys.A))
        { viewerAnchor = (0, 0); viewerEnd = (viewerLines.Length - 1, viewerLines[^1].Length); }
        if (IsControlDown(keyboard) && IsPressed(keyboard, Keys.C) && viewerAnchor != viewerEnd)
        {
            try
            {
                textInputService ??= new global::StationeryUI.Windows.WindowsTextInputService(Window.Handle);
                textInputService.WriteClipboard(viewerDocument!.Select(viewerAnchor, viewerEnd));
                viewerStatus = "選択した文字をコピーしました。";
            }
            catch (System.Runtime.InteropServices.ExternalException) { viewerStatus = "コピーできませんでした。Ctrl+C でもう一度試してください。"; }
        }
    }

    private ((int Row, int Column) First, (int Row, int Column) Last) ViewerSelection() =>
        viewerAnchor.CompareTo(viewerEnd) <= 0 ? (viewerAnchor, viewerEnd) : (viewerEnd, viewerAnchor);

    private void DrawTextViewer()
    {
        if (viewerLines is null) return;
        var area = SelectionArea();
        var (first, last) = ViewerSelection();
        for (var row = 0; row < ViewerRows && viewerTop + row < viewerLines.Length; row++)
        {
            var lineIndex = viewerTop + row;
            var slice = ViewerSlice(lineIndex, out var start);
            var y = area.Y + row * 24;
            if (lineIndex >= first.Row && lineIndex <= last.Row)
            {
                var a = Math.Clamp(lineIndex == first.Row ? first.Column - start : 0, 0, slice.Length);
                var b = Math.Clamp(lineIndex == last.Row ? last.Column - start : slice.Length, 0, slice.Length);
                var x = textRenderer?.Measure(slice[..a], 18).X ?? 0;
                var right = textRenderer?.Measure(slice[..b], 18).X ?? 0;
                DrawRectangle(new ScreenRectangle(area.X + 4 + x, y, Math.Max(0, right - x), 24), new Color(45, 95, 100));
            }
            textRenderer?.Draw(slice, ToRectangle(new ScreenRectangle(area.X + 4, y, area.Width - 8, 24)), Color.White, 18);
        }
        var panel = ModalBounds();
        textRenderer?.Draw(viewerStatus ?? "ドラッグで選択 / Ctrl+A・C / 矢印・PgUp/PgDn / Shift＋ホイールで横移動",
            ToRectangle(new ScreenRectangle(area.X, panel.Y + panel.Height - 80, area.Width, 20)), Color.LightGray, 12);
    }
}
