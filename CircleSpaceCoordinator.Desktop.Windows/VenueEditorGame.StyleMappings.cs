namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Interaction;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private sealed record MappingEditorButton(IconButtonModel Button, Action Execute, string? ColorId = null, string? PatternId = null);
    private StyleMappingDraft? mappingDraft;
    private string mappingKeyLabel = "";
    private string mappingEmptyMessage = "";
    private Action<StyleMappingEntry[]>? applyStyleMapping;
    private readonly List<MappingEditorButton> mappingEditorButtons = [];
    private IconButtonModel? pressedMappingButton;
    private int mappingRow;
    private int mappingColumn = 1;
    private int mappingScroll;
    private int mappingFocus = -1;
    private int mappingPickerColumn;
    private int mappingWidth = -1;
    private int mappingHeight = -1;
    private const int MappingVisibleRows = 8;
    private static readonly double[] MappingColumnEdges = [20, 290, 450, 610, 810, 980];
    private double MappingEditorScale => Math.Max(0.1, Math.Min(GraphicsDevice.Viewport.Width / 1000d, GraphicsDevice.Viewport.Height / 660d));
    private ScreenRectangle MappingBounds(double x, double y, double width, double height)
    {
        var scale = MappingEditorScale;
        return new((GraphicsDevice.Viewport.Width - 1000 * scale) / 2 + x * scale,
            (GraphicsDevice.Viewport.Height - 660 * scale) / 2 + y * scale, width * scale, height * scale);
    }
    private ScreenRectangle MappingCell(int visibleRow, int column) =>
        MappingBounds(MappingColumnEdges[column], 142 + visibleRow * 52, MappingColumnEdges[column + 1] - MappingColumnEdges[column] - 6, 46);

    /// <summary>Opens the shared editor; the caller owns persistence and undo.</summary>
    private void OpenStyleMappingEditor(StyleMappingDraft draft, string keyLabel, string emptyMessage,
        Action<StyleMappingEntry[]> apply)
    {
        CancelInProgressPointerInteraction();
        mappingDraft = draft;
        mappingKeyLabel = keyLabel;
        mappingEmptyMessage = emptyMessage;
        applyStyleMapping = apply;
        mappingRow = mappingScroll = mappingPickerColumn = 0;
        mappingColumn = 1;
        mappingFocus = -1;
        mappingWidth = -1;
        modalInputDrain = true;
    }

    private void CloseStyleMappingEditor()
    {
        CancelInProgressPointerInteraction();
        mappingDraft = null;
        applyStyleMapping = null;
        mappingEditorButtons.Clear();
        mappingPickerColumn = 0;
        modalInputDrain = true;
    }

    private void SaveStyleMapping()
    {
        if (mappingDraft is null || applyStyleMapping is null) return;
        try
        {
            applyStyleMapping(mappingDraft.Build());
            CloseStyleMappingEditor();
        }
        catch (Exception ex) { ShowInAppMessage($"{mappingKeyLabel}設定を反映できません", ex.Message); }
    }

    private void OpenMappingPicker(int row, int column)
    {
        if (mappingDraft is null || row < 0 || row >= mappingDraft.Rows.Count || column is < 1 or > 3) return;
        if (column == 2 && mappingDraft.Rows[row].Pattern == "solid") return;
        mappingRow = row;
        mappingColumn = column;
        mappingPickerColumn = column;
        var style = mappingDraft.Rows[row];
        var current = column == 3 ? style.Pattern : column == 1 ? style.PrimaryColor : style.SecondaryColor;
        var choices = column == 3 ? StyleMappingDraft.Patterns : StyleMappingDraft.Colors;
        mappingFocus = choices.Select((choice, index) => (choice, index)).Where(item => item.choice.Id == current)
            .Select(item => item.index).DefaultIfEmpty(column == 3 ? 0 : choices.Count).First();
        mappingWidth = -1;
        pressedMappingButton = null;
        modalInputDrain = true;
    }

    private void CloseMappingPicker()
    {
        mappingPickerColumn = 0;
        mappingFocus = -1;
        mappingWidth = -1;
        pressedMappingButton = null;
        modalInputDrain = true;
    }

    private void BuildMappingEditorButtons()
    {
        if (mappingDraft is not { } draft) return;
        if (mappingWidth == GraphicsDevice.Viewport.Width && mappingHeight == GraphicsDevice.Viewport.Height) return;
        mappingWidth = GraphicsDevice.Viewport.Width;
        mappingHeight = GraphicsDevice.Viewport.Height;
        mappingEditorButtons.Clear();
        pressedMappingButton = null;
        void Add(string label, ScreenRectangle bounds, Action action, bool enabled = true, string? color = null, string? pattern = null) =>
            mappingEditorButtons.Add(new(new IconButtonModel(bounds, label) { IsEnabled = enabled }, action, color, pattern));
        if (mappingPickerColumn > 0)
        {
            var column = mappingPickerColumn;
            var row = mappingRow;
            var choices = column == 3 ? StyleMappingDraft.Patterns : StyleMappingDraft.Colors;
            var columns = column == 3 ? 4 : 5;
            var width = (840d - (columns - 1) * 12) / columns;
            var height = column == 3 ? 130 : 76;
            for (var index = 0; index < choices.Count; index++)
            {
                var choice = choices[index];
                Add(choice.Label, MappingBounds(80 + index % columns * (width + 12), 180 + index / columns * (height + 12), width, height), () =>
                {
                    if (column == 3) draft.SetPattern(row, choice.Id);
                    else draft.SetColor(row, column == 1, choice.Id);
                    CloseMappingPicker();
                }, color: column == 3 ? null : choice.Id, pattern: column == 3 ? choice.Id : null);
            }
            if (column != 3)
                Add("任意の色（#RRGGBB）", MappingBounds(80, 500, 340, 40), () =>
                {
                    var current = column == 1 ? draft.Rows[row].PrimaryColor : draft.Rows[row].SecondaryColor;
                    var color = GenreColorFromId(current, Color.Gray);
                    OpenUnderlineInput("色コードを指定", $"#{color.R:X2}{color.G:X2}{color.B:X2}", value =>
                    {
                        draft.SetColor(row, column == 1, value);
                        CloseMappingPicker();
                    }, "#RRGGBB 形式で指定してください。例：#3366CC\nキャンセルすると色見本に戻ります。", 7);
                });
            Add("キャンセル", MappingBounds(730, 500, 190, 40), CloseMappingPicker);
        }
        else
        {
            Add("前のページ", MappingBounds(20, 576, 160, 36), () => ScrollMappingRows(-MappingVisibleRows), mappingScroll > 0);
            Add("次のページ", MappingBounds(192, 576, 160, 36), () => ScrollMappingRows(MappingVisibleRows), mappingScroll + MappingVisibleRows < draft.Rows.Count);
            Add("保存", MappingBounds(640, 606, 160, 38), SaveStyleMapping);
            Add("キャンセル", MappingBounds(820, 606, 160, 38), CloseStyleMappingEditor);
        }
        if (mappingFocus >= mappingEditorButtons.Count) mappingFocus = -1;
    }

    private void ScrollMappingRows(int offset)
    {
        mappingScroll = Math.Clamp(mappingScroll + offset, 0, Math.Max(0, mappingDraft!.Rows.Count - MappingVisibleRows));
        mappingRow = Math.Clamp(mappingRow, mappingScroll, Math.Max(mappingScroll, Math.Min(mappingDraft.Rows.Count - 1, mappingScroll + MappingVisibleRows - 1)));
        mappingWidth = -1;
    }

    private void UpdateStyleMappingEditor(KeyboardState keyboard, MouseState mouse)
    {
        if (mappingDraft is not { } draft) return;
        BuildMappingEditorButtons();
        if (IsPressed(keyboard, Keys.Escape))
        {
            if (mappingPickerColumn > 0) CloseMappingPicker(); else CloseStyleMappingEditor();
            return;
        }
        if (mappingPickerColumn == 0 && IsControlDown(keyboard) && IsPressed(keyboard, Keys.S)) { SaveStyleMapping(); return; }
        if (IsPressed(keyboard, Keys.Tab))
        {
            var backwards = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
            if (mappingPickerColumn > 0)
                mappingFocus = (mappingFocus + (backwards ? mappingEditorButtons.Count - 1 : 1)) % mappingEditorButtons.Count;
            else mappingFocus = backwards ? mappingFocus < 0 ? mappingEditorButtons.Count - 1 : mappingFocus - 1
                : (mappingFocus + 2) % (mappingEditorButtons.Count + 1) - 1;
        }
        if (mappingPickerColumn > 0)
        {
            var columns = mappingPickerColumn == 3 ? 4 : 5;
            var offset = IsPressed(keyboard, Keys.Right) ? 1 : IsPressed(keyboard, Keys.Left) ? -1
                : IsPressed(keyboard, Keys.Down) ? columns : IsPressed(keyboard, Keys.Up) ? -columns : 0;
            mappingFocus = Math.Clamp(mappingFocus + offset, 0, mappingEditorButtons.Count - 1);
        }
        else if (mappingFocus < 0)
        {
            if (IsPressed(keyboard, Keys.Left)) mappingColumn = Math.Max(1, mappingColumn - 1);
            if (IsPressed(keyboard, Keys.Right)) mappingColumn = Math.Min(3, mappingColumn + 1);
            if (IsPressed(keyboard, Keys.Up)) mappingRow = Math.Max(0, mappingRow - 1);
            if (IsPressed(keyboard, Keys.Down)) mappingRow = Math.Min(Math.Max(0, draft.Rows.Count - 1), mappingRow + 1);
            if (IsPressed(keyboard, Keys.Up) || IsPressed(keyboard, Keys.Down))
            {
                mappingScroll = Math.Clamp(mappingScroll, Math.Max(0, mappingRow - MappingVisibleRows + 1), mappingRow);
                mappingWidth = -1;
            }
            if (IsPressed(keyboard, Keys.PageUp)) ScrollMappingRows(-MappingVisibleRows);
            if (IsPressed(keyboard, Keys.PageDown)) ScrollMappingRows(MappingVisibleRows);
        }
        if (IsPressed(keyboard, Keys.Enter) || IsPressed(keyboard, Keys.Space))
        {
            if (mappingFocus < 0) OpenMappingPicker(mappingRow, mappingColumn);
            else if (mappingEditorButtons[mappingFocus].Button.IsEnabled) mappingEditorButtons[mappingFocus].Execute();
            return;
        }
        if (mappingPickerColumn == 0 && mouse.ScrollWheelValue != previousMouse.ScrollWheelValue)
        {
            ScrollMappingRows(-Math.Sign(mouse.ScrollWheelValue - previousMouse.ScrollWheelValue) * 3);
            BuildMappingEditorButtons();
        }
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        foreach (var item in mappingEditorButtons) item.Button.UpdatePointer(pointer);
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            pressedMappingButton = mappingEditorButtons.Select(item => item.Button).FirstOrDefault(button => button.Press(pointer));
            if (pressedMappingButton is not null) mappingFocus = mappingEditorButtons.FindIndex(item => item.Button == pressedMappingButton);
            else if (mappingPickerColumn == 0)
                for (var row = 0; row < MappingVisibleRows && mappingScroll + row < draft.Rows.Count; row++)
                for (var column = 1; column <= 3; column++)
                    if (Contains(MappingCell(row, column), pointer)) { OpenMappingPicker(mappingScroll + row, column); return; }
        }
        if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
        {
            var pressed = pressedMappingButton;
            pressedMappingButton = null;
            if (pressed?.Release(pointer) == true) mappingEditorButtons.Single(item => item.Button == pressed).Execute();
        }
    }

    private void DrawStyleMappingEditor()
    {
        if (mappingDraft is not { } draft) return;
        BuildMappingEditorButtons();
        void Text(string text, ScreenRectangle bounds, int size = 17, Color? color = null) =>
            textRenderer?.Draw(text, ToRectangle(bounds, 3), color ?? Color.White, Math.Max(10, (int)(size * MappingEditorScale)), true);
        Text($"{mappingKeyLabel}と色・網掛けパターンの紐づけ", MappingBounds(20, 18, 960, 38), 26);
        Text("主色・副色・網掛けのセルをクリックして選択。黒＝主色、白＝副色。", MappingBounds(20, 68, 960, 30));
        var headers = new[] { mappingKeyLabel, "主色", "副色", "網掛け（白黒見本）", "配色の見本" };
        for (var column = 0; column < headers.Length; column++)
            Text(headers[column], MappingBounds(MappingColumnEdges[column], 104, MappingColumnEdges[column + 1] - MappingColumnEdges[column] - 6, 30));
        for (var row = 0; row < MappingVisibleRows && mappingScroll + row < draft.Rows.Count; row++)
        {
            var style = draft.Rows[mappingScroll + row];
            for (var column = 0; column < 5; column++)
            {
                var bounds = MappingCell(row, column);
                DrawRectangle(bounds, new Color(35, 43, 54));
                if (column == 0) Text(style.Key, bounds);
                else if (column is 1 or 2)
                {
                    if (column == 2 && style.Pattern == "solid") Text("単色では未使用", bounds, 14, Color.Gray);
                    else
                    {
                        var id = column == 1 ? style.PrimaryColor : style.SecondaryColor;
                        var color = GenreColorFromId(id, Color.Gray);
                        DrawRectangle(bounds, color);
                        Text(StyleMappingDraft.Colors.FirstOrDefault(choice => choice.Id == id).Label ?? id, bounds, 17, MappingColorText(color));
                    }
                }
                else
                {
                    var swatch = new ScreenRectangle(bounds.X + 4, bounds.Y + 3, bounds.Width - 8, column == 3 ? bounds.Height * 0.54 : bounds.Height - 6);
                    DrawRectangle(swatch, column == 3 ? Color.Black : GenreColorFromId(style.PrimaryColor, Color.Gray));
                    DrawGenrePattern(swatch, GenrePatternFromId(style.Pattern), column == 3 ? Color.White : GenreColorFromId(style.SecondaryColor, Color.White), column == 3 ? (byte)255 : (byte)180);
                    if (column == 3) Text(StyleMappingDraft.Patterns.FirstOrDefault(choice => choice.Id == style.Pattern).Label ?? style.Pattern,
                        new(bounds.X, bounds.Y + bounds.Height * 0.58, bounds.Width, bounds.Height * 0.4), 12);
                }
                DrawOutline(bounds, 1, new Color(100, 119, 130));
                if (mappingPickerColumn == 0 && mappingFocus < 0 && mappingRow == mappingScroll + row && mappingColumn == column)
                    DrawOutline(bounds, 2, OperationTargetColor);
            }
        }
        if (draft.Rows.Count == 0) Text(mappingEmptyMessage, MappingBounds(20, 142, 960, 46));
        Text($"{draft.Rows.Count} 件　矢印：セル移動　Enter：選択　Tab：操作ボタンへ", MappingBounds(370, 576, 610, 30), 14);
        Text("Ctrl+S：設定を反映　Esc：キャンセル　イベントのファイル保存は編集画面のCtrl+S", MappingBounds(20, 620, 610, 25), 12);
        if (mappingPickerColumn > 0)
        {
            DrawRectangle(new(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 190));
            var panel = MappingBounds(60, 90, 880, 470);
            DrawRectangle(panel, new Color(24, 29, 36));
            DrawOutline(panel, 2, Color.LightSlateGray);
            Text(mappingPickerColumn == 3 ? "網掛けを選択 — 黒が主色、白が副色" : mappingPickerColumn == 1 ? "主色を選択" : "副色を選択", MappingBounds(80, 108, 840, 38), 23);
            Text("見本をクリックして選択　Tab・矢印で移動　Enterで決定　Escでキャンセル", MappingBounds(80, 146, 840, 26), 14);
        }
        for (var index = 0; index < mappingEditorButtons.Count; index++)
        {
            var item = mappingEditorButtons[index];
            var button = item.Button;
            OperationButtonRenderer.Draw(button,
                (area, color) => DrawRectangle(area, ToButtonColor(color)),
                (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)),
                (area, color) => { if (item.ColorId is null && item.PatternId is null) Text(button.AccessibleName, area, 17, ToButtonColor(color)); });
            var bounds = button.Bounds;
            if (item.ColorId is { } colorId)
            {
                var color = GenreColorFromId(colorId, Color.Gray);
                var inside = new ScreenRectangle(bounds.X + 4, bounds.Y + 4, bounds.Width - 8, bounds.Height - 8);
                DrawRectangle(inside, color);
                Text(button.AccessibleName, inside, 17, MappingColorText(color));
            }
            if (item.PatternId is { } pattern)
            {
                var inside = new ScreenRectangle(bounds.X + 8, bounds.Y + 8, bounds.Width - 16, bounds.Height * 0.65);
                DrawRectangle(inside, Color.Black);
                DrawGenrePattern(inside, GenrePatternFromId(pattern), Color.White, 255);
                Text(button.AccessibleName, new(bounds.X + 4, bounds.Y + bounds.Height * 0.73, bounds.Width - 8, bounds.Height * 0.23), 15);
            }
            if (mappingFocus == index && button.IsEnabled) DrawOutline(bounds, 2, OperationTargetColor);
        }
    }

    private static Color MappingColorText(Color color) => color.R * 0.299 + color.G * 0.587 + color.B * 0.114 < 145 ? Color.White : Color.Black;
}
