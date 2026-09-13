namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Engine.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private sealed record GenreEditorButton(IconButtonModel Button, Action Execute, string? ColorId = null, string? PatternId = null);
    private GenreStyleDraft? genreDraft;
    private readonly List<GenreEditorButton> genreEditorButtons = [];
    private IconButtonModel? pressedGenreButton;
    private int genreRow;
    private int genreColumn = 1;
    private int genreScroll;
    private int genreFocus = -1;
    private int genrePickerColumn;
    private int genreWidth = -1;
    private int genreHeight = -1;
    private const int GenreVisibleRows = 8;
    private static readonly double[] GenreColumnEdges = [20, 290, 450, 610, 810, 980];
    private double GenreEditorScale => Math.Max(0.1, Math.Min(GraphicsDevice.Viewport.Width / 1000d, GraphicsDevice.Viewport.Height / 660d));
    private ScreenRectangle GenreBounds(double x, double y, double width, double height)
    {
        var scale = GenreEditorScale;
        return new((GraphicsDevice.Viewport.Width - 1000 * scale) / 2 + x * scale,
            (GraphicsDevice.Viewport.Height - 660 * scale) / 2 + y * scale, width * scale, height * scale);
    }
    private ScreenRectangle GenreCell(int visibleRow, int column) =>
        GenreBounds(GenreColumnEdges[column], 142 + visibleRow * 52, GenreColumnEdges[column + 1] - GenreColumnEdges[column] - 6, 46);

    private void OpenGenreStyleEditor()
    {
        if (workspace is null) return;
        CancelInProgressPointerInteraction();
        genreDraft = new GenreStyleDraft(workspace.Project);
        genreRow = genreScroll = genrePickerColumn = 0;
        genreColumn = 1;
        genreFocus = -1;
        genreWidth = -1;
        modalInputDrain = true;
    }

    private void CloseGenreStyleEditor()
    {
        CancelInProgressPointerInteraction();
        genreDraft = null;
        genreEditorButtons.Clear();
        genrePickerColumn = 0;
        modalInputDrain = true;
    }

    private void SaveGenreStyles()
    {
        if (genreDraft is null || workspace is null) return;
        try
        {
            workspace.Execute(new SetGenreStyles(genreDraft.Build()), selectedPlanEdit: false);
            CloseGenreStyleEditor();
        }
        catch (Exception ex) { ShowInAppMessage("ジャンル設定を反映できません", ex.Message); }
    }

    private void OpenGenrePicker(int row, int column)
    {
        if (genreDraft is null || row < 0 || row >= genreDraft.Rows.Count || column is < 1 or > 3) return;
        if (column == 2 && genreDraft.Rows[row].Pattern == "solid") return;
        genreRow = row;
        genreColumn = column;
        genrePickerColumn = column;
        var style = genreDraft.Rows[row];
        var current = column == 3 ? style.Pattern : column == 1 ? style.PrimaryColor : style.SecondaryColor;
        var choices = column == 3 ? GenreStyleDraft.Patterns : GenreStyleDraft.Colors;
        genreFocus = choices.Select((choice, index) => (choice, index)).Where(item => item.choice.Id == current)
            .Select(item => item.index).DefaultIfEmpty(column == 3 ? 0 : choices.Count).First();
        genreWidth = -1;
        pressedGenreButton = null;
        modalInputDrain = true;
    }

    private void CloseGenrePicker()
    {
        genrePickerColumn = 0;
        genreFocus = -1;
        genreWidth = -1;
        pressedGenreButton = null;
        modalInputDrain = true;
    }

    private void BuildGenreEditorButtons()
    {
        if (genreDraft is not { } draft) return;
        if (genreWidth == GraphicsDevice.Viewport.Width && genreHeight == GraphicsDevice.Viewport.Height) return;
        genreWidth = GraphicsDevice.Viewport.Width;
        genreHeight = GraphicsDevice.Viewport.Height;
        genreEditorButtons.Clear();
        pressedGenreButton = null;
        void Add(string label, ScreenRectangle bounds, Action action, bool enabled = true, string? color = null, string? pattern = null) =>
            genreEditorButtons.Add(new(new IconButtonModel(bounds, label) { IsEnabled = enabled }, action, color, pattern));
        if (genrePickerColumn > 0)
        {
            var column = genrePickerColumn;
            var row = genreRow;
            var choices = column == 3 ? GenreStyleDraft.Patterns : GenreStyleDraft.Colors;
            var columns = column == 3 ? 4 : 5;
            var width = (840d - (columns - 1) * 12) / columns;
            var height = column == 3 ? 130 : 76;
            for (var index = 0; index < choices.Count; index++)
            {
                var choice = choices[index];
                Add(choice.Label, GenreBounds(80 + index % columns * (width + 12), 180 + index / columns * (height + 12), width, height), () =>
                {
                    if (column == 3) draft.SetPattern(row, choice.Id);
                    else draft.SetColor(row, column == 1, choice.Id);
                    CloseGenrePicker();
                }, color: column == 3 ? null : choice.Id, pattern: column == 3 ? choice.Id : null);
            }
            if (column != 3)
                Add("任意の色（#RRGGBB）", GenreBounds(80, 500, 340, 40), () =>
                {
                    var current = column == 1 ? draft.Rows[row].PrimaryColor : draft.Rows[row].SecondaryColor;
                    var color = GenreColorFromId(current, Color.Gray);
                    OpenUnderlineInput("色コードを指定", $"#{color.R:X2}{color.G:X2}{color.B:X2}", value =>
                    {
                        draft.SetColor(row, column == 1, value);
                        CloseGenrePicker();
                    }, "#RRGGBB 形式で指定してください。例：#3366CC\nキャンセルすると色見本に戻ります。", 7);
                });
            Add("キャンセル", GenreBounds(730, 500, 190, 40), CloseGenrePicker);
        }
        else
        {
            Add("前のページ", GenreBounds(20, 576, 160, 36), () => ScrollGenreRows(-GenreVisibleRows), genreScroll > 0);
            Add("次のページ", GenreBounds(192, 576, 160, 36), () => ScrollGenreRows(GenreVisibleRows), genreScroll + GenreVisibleRows < draft.Rows.Count);
            Add("保存", GenreBounds(640, 606, 160, 38), SaveGenreStyles);
            Add("キャンセル", GenreBounds(820, 606, 160, 38), CloseGenreStyleEditor);
        }
        if (genreFocus >= genreEditorButtons.Count) genreFocus = -1;
    }

    private void ScrollGenreRows(int offset)
    {
        genreScroll = Math.Clamp(genreScroll + offset, 0, Math.Max(0, genreDraft!.Rows.Count - GenreVisibleRows));
        genreRow = Math.Clamp(genreRow, genreScroll, Math.Max(genreScroll, Math.Min(genreDraft.Rows.Count - 1, genreScroll + GenreVisibleRows - 1)));
        genreWidth = -1;
    }

    private void UpdateGenreStyleEditor(KeyboardState keyboard, MouseState mouse)
    {
        if (genreDraft is not { } draft) return;
        BuildGenreEditorButtons();
        if (IsPressed(keyboard, Keys.Escape))
        {
            if (genrePickerColumn > 0) CloseGenrePicker(); else CloseGenreStyleEditor();
            return;
        }
        if (genrePickerColumn == 0 && IsControlDown(keyboard) && IsPressed(keyboard, Keys.S)) { SaveGenreStyles(); return; }
        if (IsPressed(keyboard, Keys.Tab))
        {
            var backwards = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
            if (genrePickerColumn > 0)
                genreFocus = (genreFocus + (backwards ? genreEditorButtons.Count - 1 : 1)) % genreEditorButtons.Count;
            else genreFocus = backwards ? genreFocus < 0 ? genreEditorButtons.Count - 1 : genreFocus - 1
                : (genreFocus + 2) % (genreEditorButtons.Count + 1) - 1;
        }
        if (genrePickerColumn > 0)
        {
            var columns = genrePickerColumn == 3 ? 4 : 5;
            var offset = IsPressed(keyboard, Keys.Right) ? 1 : IsPressed(keyboard, Keys.Left) ? -1
                : IsPressed(keyboard, Keys.Down) ? columns : IsPressed(keyboard, Keys.Up) ? -columns : 0;
            genreFocus = Math.Clamp(genreFocus + offset, 0, genreEditorButtons.Count - 1);
        }
        else if (genreFocus < 0)
        {
            if (IsPressed(keyboard, Keys.Left)) genreColumn = Math.Max(1, genreColumn - 1);
            if (IsPressed(keyboard, Keys.Right)) genreColumn = Math.Min(3, genreColumn + 1);
            if (IsPressed(keyboard, Keys.Up)) genreRow = Math.Max(0, genreRow - 1);
            if (IsPressed(keyboard, Keys.Down)) genreRow = Math.Min(Math.Max(0, draft.Rows.Count - 1), genreRow + 1);
            if (IsPressed(keyboard, Keys.Up) || IsPressed(keyboard, Keys.Down))
            {
                genreScroll = Math.Clamp(genreScroll, Math.Max(0, genreRow - GenreVisibleRows + 1), genreRow);
                genreWidth = -1;
            }
            if (IsPressed(keyboard, Keys.PageUp)) ScrollGenreRows(-GenreVisibleRows);
            if (IsPressed(keyboard, Keys.PageDown)) ScrollGenreRows(GenreVisibleRows);
        }
        if (IsPressed(keyboard, Keys.Enter) || IsPressed(keyboard, Keys.Space))
        {
            if (genreFocus < 0) OpenGenrePicker(genreRow, genreColumn);
            else if (genreEditorButtons[genreFocus].Button.IsEnabled) genreEditorButtons[genreFocus].Execute();
            return;
        }
        if (genrePickerColumn == 0 && mouse.ScrollWheelValue != previousMouse.ScrollWheelValue)
        {
            ScrollGenreRows(-Math.Sign(mouse.ScrollWheelValue - previousMouse.ScrollWheelValue) * 3);
            BuildGenreEditorButtons();
        }
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        foreach (var item in genreEditorButtons) item.Button.UpdatePointer(pointer);
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            pressedGenreButton = genreEditorButtons.Select(item => item.Button).FirstOrDefault(button => button.Press(pointer));
            if (pressedGenreButton is not null) genreFocus = genreEditorButtons.FindIndex(item => item.Button == pressedGenreButton);
            else if (genrePickerColumn == 0)
                for (var row = 0; row < GenreVisibleRows && genreScroll + row < draft.Rows.Count; row++)
                for (var column = 1; column <= 3; column++)
                    if (Contains(GenreCell(row, column), pointer)) { OpenGenrePicker(genreScroll + row, column); return; }
        }
        if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
        {
            var pressed = pressedGenreButton;
            pressedGenreButton = null;
            if (pressed?.Release(pointer) == true) genreEditorButtons.Single(item => item.Button == pressed).Execute();
        }
    }

    private void DrawGenreStyleEditor()
    {
        if (genreDraft is not { } draft) return;
        BuildGenreEditorButtons();
        void Text(string text, ScreenRectangle bounds, int size = 17, Color? color = null) =>
            textRenderer?.Draw(text, ToRectangle(bounds, 3), color ?? Color.White, Math.Max(10, (int)(size * GenreEditorScale)), true);
        Text("ジャンルコードと色・網掛けパターンの紐づけ", GenreBounds(20, 18, 960, 38), 26);
        Text("主色・副色・網掛けのセルをクリックして選択。黒＝主色、白＝副色。", GenreBounds(20, 68, 960, 30));
        var headers = new[] { "ジャンル", "主色", "副色", "網掛け（白黒見本）", "配色の見本" };
        for (var column = 0; column < headers.Length; column++)
            Text(headers[column], GenreBounds(GenreColumnEdges[column], 104, GenreColumnEdges[column + 1] - GenreColumnEdges[column] - 6, 30));
        for (var row = 0; row < GenreVisibleRows && genreScroll + row < draft.Rows.Count; row++)
        {
            var style = draft.Rows[genreScroll + row];
            for (var column = 0; column < 5; column++)
            {
                var bounds = GenreCell(row, column);
                DrawRectangle(bounds, new Color(35, 43, 54));
                if (column == 0) Text(style.GenreId, bounds);
                else if (column is 1 or 2)
                {
                    if (column == 2 && style.Pattern == "solid") Text("単色では未使用", bounds, 14, Color.Gray);
                    else
                    {
                        var id = column == 1 ? style.PrimaryColor : style.SecondaryColor;
                        var color = GenreColorFromId(id, Color.Gray);
                        DrawRectangle(bounds, color);
                        Text(GenreStyleDraft.Colors.FirstOrDefault(choice => choice.Id == id).Label ?? id, bounds, 17, GenreColorText(color));
                    }
                }
                else
                {
                    var swatch = new ScreenRectangle(bounds.X + 4, bounds.Y + 3, bounds.Width - 8, column == 3 ? bounds.Height * 0.54 : bounds.Height - 6);
                    DrawRectangle(swatch, column == 3 ? Color.Black : GenreColorFromId(style.PrimaryColor, Color.Gray));
                    DrawGenrePattern(swatch, GenrePatternFromId(style.Pattern), column == 3 ? Color.White : GenreColorFromId(style.SecondaryColor, Color.White), column == 3 ? (byte)255 : (byte)180);
                    if (column == 3) Text(GenreStyleDraft.Patterns.FirstOrDefault(choice => choice.Id == style.Pattern).Label ?? style.Pattern,
                        new(bounds.X, bounds.Y + bounds.Height * 0.58, bounds.Width, bounds.Height * 0.4), 12);
                }
                DrawOutline(bounds, 1, new Color(100, 119, 130));
                if (genrePickerColumn == 0 && genreFocus < 0 && genreRow == genreScroll + row && genreColumn == column)
                    DrawOutline(bounds, 2, Color.Turquoise);
            }
        }
        if (draft.Rows.Count == 0) Text("ジャンルが設定されたサークルはありません。", GenreBounds(20, 142, 960, 46));
        Text($"{draft.Rows.Count} ジャンル　矢印：セル移動　Enter：選択　Tab：操作ボタンへ", GenreBounds(370, 576, 610, 30), 14);
        Text("Ctrl+S：設定を反映　Esc：キャンセル　イベントのファイル保存は編集画面のCtrl+S", GenreBounds(20, 620, 610, 25), 12);
        if (genrePickerColumn > 0)
        {
            DrawRectangle(new(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 190));
            var panel = GenreBounds(60, 90, 880, 470);
            DrawRectangle(panel, new Color(24, 29, 36));
            DrawOutline(panel, 2, Color.LightSlateGray);
            Text(genrePickerColumn == 3 ? "網掛けを選択 — 黒が主色、白が副色" : genrePickerColumn == 1 ? "主色を選択" : "副色を選択", GenreBounds(80, 108, 840, 38), 23);
            Text("見本をクリックして選択　Tab・矢印で移動　Enterで決定　Escでキャンセル", GenreBounds(80, 146, 840, 26), 14);
        }
        for (var index = 0; index < genreEditorButtons.Count; index++)
        {
            var item = genreEditorButtons[index];
            var button = item.Button;
            StationeryButtonRenderer.Draw(button,
                (area, color) => DrawRectangle(area, ToButtonColor(color)),
                (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)),
                (area, color) => { if (item.ColorId is null && item.PatternId is null) Text(button.AccessibleName, area, 17, ToButtonColor(color)); });
            var bounds = button.Bounds;
            if (item.ColorId is { } colorId)
            {
                var color = GenreColorFromId(colorId, Color.Gray);
                var inside = new ScreenRectangle(bounds.X + 4, bounds.Y + 4, bounds.Width - 8, bounds.Height - 8);
                DrawRectangle(inside, color);
                Text(button.AccessibleName, inside, 17, GenreColorText(color));
            }
            if (item.PatternId is { } pattern)
            {
                var inside = new ScreenRectangle(bounds.X + 8, bounds.Y + 8, bounds.Width - 16, bounds.Height * 0.65);
                DrawRectangle(inside, Color.Black);
                DrawGenrePattern(inside, GenrePatternFromId(pattern), Color.White, 255);
                Text(button.AccessibleName, new(bounds.X + 4, bounds.Y + bounds.Height * 0.73, bounds.Width - 8, bounds.Height * 0.23), 15);
            }
            if (genreFocus == index) DrawOutline(bounds, 2, Color.Turquoise);
        }
    }

    private static Color GenreColorText(Color color) => color.R * 0.299 + color.G * 0.587 + color.B * 0.114 < 145 ? Color.White : Color.Black;
}
