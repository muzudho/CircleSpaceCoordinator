namespace CircleSpaceCoordinator.Desktop.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private string[]? selectionLabels;
    private int selectionIndex;
    private int selectionScroll;
    private int selectionPressed = -1;
    private ScreenRectangle SelectionArea()
    {
        var bounds = ModalBounds();
        if (exportPlanChoices is not null)
            return new ScreenRectangle(bounds.X + bounds.Width / 2 + 10, bounds.Y + 124,
                (bounds.Width - 60) / 2, Math.Max(1, bounds.Height - 212));
        return new ScreenRectangle(bounds.X + 20, bounds.Y + 60, bounds.Width - 40, Math.Max(32, bounds.Height - 148));
    }
    private int SelectionRowHeight => exportPlanChoices is null ? 32 : 70;
    private int SelectionPageSize => Math.Max(1, (int)(SelectionArea().Height / SelectionRowHeight));

    private void ShowNotice(string title, string message, Action back) =>
        OpenModal(new ModalDialogModel(ModalDialogKind.Message, title, message), _ => back());

    private void OpenLayoutSelection(string title, string selectedId, Action<string> accepted)
    {
        var layouts = workspace!.Project.DeskLayouts.ToArray();
        OpenSelection(title, layouts.Select(item => $"{item.Name}（{item.Id}）").ToArray(),
            Array.FindIndex(layouts, item => item.Id == selectedId), index => accepted(layouts[index].Id));
    }

    private void OpenSelection(string title, IReadOnlyList<string> labels, int selected, Action<int> accepted, Action? cancelled = null)
    {
        var items = labels.ToArray();
        var owner = workspace;
        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, title, ""), action =>
        {
            if (action != ModalDialogAction.Accept) { cancelled?.Invoke(); return; }
            if (items.Length == 0) return;
            var chosen = selectionIndex;
            try
            {
                if (workspace != owner) throw new InvalidOperationException("対象のイベントが変わりました。画面を開き直してください。");
                accepted(chosen);
            }
            catch (Exception exception) { ShowNotice(title, exception.Message, () => OpenSelection(title, items, chosen, accepted, cancelled)); }
        }, [("キャンセル", ModalDialogAction.Cancel), ("選択", ModalDialogAction.Accept)]);
        selectionLabels = items;
        selectionIndex = Math.Clamp(selected, 0, Math.Max(0, items.Length - 1));
        selectionScroll = selectionIndex;
        selectionPressed = -1;
    }

    private bool UpdateSelection(KeyboardState keyboard, MouseState mouse)
    {
        if (selectionLabels is null) return false;
        var area = SelectionArea();
        var page = SelectionPageSize;
        var delta = IsPressed(keyboard, Keys.Down) ? 1 : IsPressed(keyboard, Keys.Up) ? -1
            : IsPressed(keyboard, Keys.PageDown) ? page : IsPressed(keyboard, Keys.PageUp) ? -page : 0;
        if (IsPressed(keyboard, Keys.Home)) delta = -selectionLabels.Length;
        if (IsPressed(keyboard, Keys.End)) delta = selectionLabels.Length;
        selectionIndex = Math.Clamp(selectionIndex + delta, 0, Math.Max(0, selectionLabels.Length - 1));
        if (delta != 0)
        {
            if (selectionIndex < selectionScroll) selectionScroll = selectionIndex;
            if (selectionIndex >= selectionScroll + page) selectionScroll = selectionIndex - page + 1;
        }
        var wheel = mouse.ScrollWheelValue - previousMouse.ScrollWheelValue;
        if (wheel != 0) selectionScroll += wheel > 0 ? -3 : 3;
        selectionScroll = Math.Clamp(selectionScroll, 0, Math.Max(0, selectionLabels.Length - page));
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        var row = Contains(area, pointer) && pointer.Y < area.Y + page * SelectionRowHeight ? selectionScroll + (int)((pointer.Y - area.Y) / SelectionRowHeight) : -1;
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            selectionPressed = row;
            if (row >= 0 && row < selectionLabels.Length) selectionIndex = row;
        }
        if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
        {
            if (row >= 0 && row < selectionLabels.Length && selectionPressed == row) selectionIndex = row;
            selectionPressed = -1;
        }
        return false;
    }

    private void DrawSelection()
    {
        if (selectionLabels is null) return;
        if (exportPlanChoices is not null) { DrawExportPlanSelection(); return; }
        var area = SelectionArea();
        if (selectionLabels.Length == 0)
            textRenderer?.Draw("選択できる項目がありません。", ToRectangle(area), Color.White, 18);
        for (var row = 0; row < SelectionPageSize && selectionScroll + row < selectionLabels.Length; row++)
        {
            var index = selectionScroll + row;
            var bounds = new ScreenRectangle(area.X, area.Y + row * 32, area.Width, 30);
            if (index == selectionIndex) DrawRectangle(bounds, new Color(45, 95, 100));
            textRenderer?.Draw(selectionLabels[index], ToRectangle(bounds, 4), Color.White, 17);
        }
        var panel = ModalBounds();
        textRenderer?.Draw($"{(selectionLabels.Length == 0 ? 0 : selectionIndex + 1)} / {selectionLabels.Length}　↑↓・PgUp/PgDn・ホイール",
            ToRectangle(new ScreenRectangle(area.X, panel.Y + panel.Height - 80, area.Width, 20)), Color.LightGray, 12);
    }
}
