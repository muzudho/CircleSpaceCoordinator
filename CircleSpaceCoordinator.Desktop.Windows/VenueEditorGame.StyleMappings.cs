namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Core.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private sealed record MappingEditorButton(IconButtonModel Button, Action Execute, string? ColorId = null, string? PatternId = null, string? Tooltip = null);
    private bool mappingButtonsHaveChanges;
    private bool mappingKnowledgeComments;
    private StyleMappingDraft? mappingDraft;
    private string mappingKeyLabel = "";
    private string mappingEmptyMessage = "";
    private Action<StyleMappingEntry[], string, string, DateOnly>? applyStyleMapping;
    private ChangeTagEditor? mappingChangeTag;
    private bool mappingExitConfirmationOpen;
    private PersonCredits? mappingPreviousCredits;
    private StyleMappingEntry[] mappingAppliedStyles = [];
    private readonly List<MappingEditorButton> mappingEditorButtons = [];
    private IconButtonModel? pressedMappingButton;
    private int mappingRow;
    private int mappingColumn = 1;
    private int mappingScroll;
    private int mappingFocus = -1;
    private int mappingPickerColumn;
    private int mappingWidth = -1;
    private int mappingHeight = -1;
    private const int MappingVisibleRows = 6;
    private static readonly double[] DefaultMappingColumnEdges = [20, 290, 450, 610, 810, 980];
    private static readonly double[] GenreMappingColumnEdges = [20, 290, 370, 450, 530, 610, 980];
    private double[] MappingColumnEdges => mappingKnowledgeComments ? GenreMappingColumnEdges : DefaultMappingColumnEdges;
    private double MappingEditorScale => Math.Max(0.1, Math.Min(GraphicsDevice.Viewport.Width / 1000d, (GraphicsDevice.Viewport.Height - WorkerBarHeight - StatusBarHeight) / 660d));
    private ScreenRectangle MappingBounds(double x, double y, double width, double height)
    {
        var scale = MappingEditorScale;
        return new((GraphicsDevice.Viewport.Width - 1000 * scale) / 2 + x * scale,
            WorkerBarHeight + (GraphicsDevice.Viewport.Height - WorkerBarHeight - StatusBarHeight - 660 * scale) / 2 + y * scale, width * scale, height * scale);
    }
    private ScreenRectangle MappingCell(int visibleRow, int column) =>
        MappingBounds(MappingColumnEdges[column], 142 + visibleRow * 52, MappingColumnEdges[column + 1] - MappingColumnEdges[column] - 6, 46);

    /// <summary>Opens the shared editor; the caller owns persistence and undo.</summary>
    private void OpenStyleMappingEditor(StyleMappingDraft draft, string keyLabel, string emptyMessage,
        Action<StyleMappingEntry[], string, string, DateOnly> apply, PersonCredits? previousCredits = null, bool knowledgeComments = false)
    {
        CancelInProgressPointerInteraction();
        mappingDraft = draft;
        mappingKnowledgeComments = knowledgeComments;
        mappingKeyLabel = keyLabel;
        mappingEmptyMessage = emptyMessage;
        applyStyleMapping = apply;
        mappingPreviousCredits = previousCredits;
        mappingChangeTag = new ChangeTagEditor(ValidateMappingChangeLog);
        mappingTextSuppressExit = false;
        mappingTextRange = (0, 0);
        mappingAppliedStyles = draft.Build();
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
        SetMappingTextFocus(false);
        mappingChangeTag = null;
        applyStyleMapping = null;
        mappingEditorButtons.Clear();
        mappingPickerColumn = 0;
        modalInputDrain = true;
    }

    private void SaveStyleMapping()
    {
        TryFinishStyleMapping();
    }

    private void DiscardStyleMapping()
    {
        mappingDraft?.RestoreOpeningSnapshot();
        CloseStyleMappingEditor();
    }

    private bool TryExitStyleMapping()
    {
        if (mappingExitConfirmationOpen) return false;
        SyncMappingChangeTag();
        if (mappingDraft?.HasChanges != true ||
            mappingChangeTag?.CanClose == true && mappingComposition.Length == 0)
            return TryFinishStyleMapping();

        // Cancel the OS close request now; resume Exit only after an explicit choice.
        mappingExitConfirmationOpen = true;
        SetMappingTextFocus(false);
        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "終了の確認",
            "変更を破棄してアプリケーションを終了しますか"), action =>
        {
            mappingExitConfirmationOpen = false;
            if (action != ModalDialogAction.Accept) return;
            // Draft edits never touch the project before confirmation. Restore its
            // page-opening copy, then discard the input without creating a new tag.
            DiscardStyleMapping();
            Exit();
        }, [("終了する", ModalDialogAction.Accept), ("キャンセル", ModalDialogAction.Cancel)]);
        return false;
    }

    private bool TryFinishStyleMapping()
    {
        if (mappingDraft is null || applyStyleMapping is null) return true;
        if (mappingComposition.Length > 0) return false;
        SyncMappingChangeTag();
        if (mappingChangeTag is null) return false;
        if (mappingDraft.HasChanges && !EnsureHandle()) return false;
        if (!mappingChangeTag.TryBeginSave(out var changeLog))
        {
            SetMappingTextFocus(true);
            return false;
        }
        SetMappingTextFocus(false);
        var applied = false;
        var previousStyles = mappingAppliedStyles;
        try
        {
            var styles = mappingDraft.Build();
            if (!styles.SequenceEqual(mappingAppliedStyles))
            {
                applyStyleMapping(styles, changeLog!, Handle, WorkDate);
                mappingAppliedStyles = styles;
                applied = true;
            }
            if (!FlushAutoSave())
            {
                if (applied) { workspace!.Undo(); mappingAppliedStyles = previousStyles; }
                mappingChangeTag.SaveFailed("保存できませんでした。入力を保持しています。閉じるで再試行してください。");
                return false;
            }
            CloseStyleMappingEditor();
            return true;
        }
        catch (Exception ex) { mappingChangeTag?.SaveFailed(ex.Message); return false; }
    }

    private void OpenMappingPicker(int row, int column)
    {
        if (mappingKnowledgeComments && column == 5)
        {
            OpenGenreKnowledgeComment(row);
            return;
        }
        if (mappingDraft is null || row < 0 || row >= mappingDraft.Rows.Count || column is < 1 or > 3) return;
        if (column == 2 && mappingDraft.Rows[row].Pattern == "solid") return;
        mappingRow = row;
        mappingColumn = column;
        mappingPickerColumn = column;
        SetMappingTextFocus(false);
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
        SyncMappingChangeTag();
        if (mappingPickerColumn == 0 && mappingEditorButtons.FirstOrDefault(item => item.Button.AccessibleName == "閉じる") is { } close)
            close.Button.IsEnabled = mappingChangeTag?.CanClose == true && mappingComposition.Length == 0;
        if (mappingWidth == GraphicsDevice.Viewport.Width && mappingHeight == GraphicsDevice.Viewport.Height &&
            (mappingPickerColumn > 0 || mappingButtonsHaveChanges == draft.HasChanges)) return;
        mappingButtonsHaveChanges = draft.HasChanges;
        mappingWidth = GraphicsDevice.Viewport.Width;
        mappingHeight = GraphicsDevice.Viewport.Height;
        mappingEditorButtons.Clear();
        pressedMappingButton = null;
        void Add(string label, ScreenRectangle bounds, Action action, bool enabled = true, string? color = null, string? pattern = null, string? tooltip = null) =>
            mappingEditorButtons.Add(new(new IconButtonModel(bounds, label) { IsEnabled = enabled }, action, color, pattern, tooltip));
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
            Add("前のページ", MappingBounds(20, 460, 160, 32), () => ScrollMappingRows(-MappingVisibleRows), mappingScroll > 0,
                tooltip: "前のページの行を表示します。PageUpでも移動できます。");
            Add("次のページ", MappingBounds(192, 460, 160, 32), () => ScrollMappingRows(MappingVisibleRows), mappingScroll + MappingVisibleRows < draft.Rows.Count,
                tooltip: "次のページの行を表示します。PageDownでも移動できます。");
            Add("閉じる", MappingCloseBounds, SaveStyleMapping, mappingChangeTag?.CanClose == true && mappingComposition.Length == 0,
                tooltip: draft.HasChanges ? "変更は自動で保存されます。前のページに戻ります。" : "前のページに戻ります。");
            if (draft.HasChanges)
                Add("破棄", MappingDiscardBounds, DiscardStyleMapping,
                    tooltip: "変更を元に戻して、前のページに戻ります。");
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
        if (mappingPickerColumn == 0 && UpdateMappingChangeTag(keyboard, mouse)) return;
        if (IsPressed(keyboard, Keys.Escape))
        {
            if (mappingPickerColumn > 0) CloseMappingPicker(); else SaveStyleMapping();
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
            if (IsPressed(keyboard, Keys.Left)) mappingColumn = mappingColumn == 5 ? 3 : Math.Max(1, mappingColumn - 1);
            if (IsPressed(keyboard, Keys.Right)) mappingColumn = mappingKnowledgeComments && mappingColumn >= 3 ? 5 : Math.Min(3, mappingColumn + 1);
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
                for (var column = 1; column <= (mappingKnowledgeComments ? 5 : 3); column++)
                    if (column != 4)
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
        var headers = mappingKnowledgeComments
            ? new[] { "ジャンル", "主色", "副色", "網掛け", "見本", "コメント" }
            : new[] { mappingKeyLabel, "主色", "副色", "網掛け（白黒見本）", "配色の見本" };
        for (var column = 0; column < headers.Length; column++)
            Text(headers[column], MappingBounds(MappingColumnEdges[column], 104, MappingColumnEdges[column + 1] - MappingColumnEdges[column] - 6, 30));
        for (var row = 0; row < MappingVisibleRows && mappingScroll + row < draft.Rows.Count; row++)
        {
            var style = draft.Rows[mappingScroll + row];
            for (var column = 0; column < headers.Length; column++)
            {
                var bounds = MappingCell(row, column);
                var plainCell = mappingKnowledgeComments && column is 0 or 5;
                if (!plainCell) DrawRectangle(bounds, new Color(35, 43, 54));
                if (column == 0) Text(style.Key, bounds);
                else if (column == 5) DrawGenreKnowledgeComment(style.KnowledgeComment, bounds);
                else if (column is 1 or 2)
                {
                    if (column == 2 && style.Pattern == "solid") Text(mappingKnowledgeComments ? "未使用" : "単色では未使用", bounds, 14, Color.Gray);
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
                    var swatch = new ScreenRectangle(bounds.X + 4, bounds.Y + 3, bounds.Width - 8,
                        column == 3 && !mappingKnowledgeComments ? bounds.Height * 0.54 : bounds.Height - 6);
                    DrawRectangle(swatch, column == 3 ? Color.Black : GenreColorFromId(style.PrimaryColor, Color.Gray));
                    DrawGenrePattern(swatch, GenrePatternFromId(style.Pattern), column == 3 ? Color.White : GenreColorFromId(style.SecondaryColor, Color.White), column == 3 ? (byte)255 : (byte)180);
                    if (column == 3 && !mappingKnowledgeComments) Text(StyleMappingDraft.Patterns.FirstOrDefault(choice => choice.Id == style.Pattern).Label ?? style.Pattern,
                        new(bounds.X, bounds.Y + bounds.Height * 0.58, bounds.Width, bounds.Height * 0.4), 12);
                }
                if (!plainCell) DrawOutline(bounds, 1, new Color(100, 119, 130));
                if (mappingPickerColumn == 0 && mappingFocus < 0 && mappingRow == mappingScroll + row && mappingColumn == column)
                {
                    if (plainCell)
                        DrawLine(new(bounds.X + 6, bounds.Y + bounds.Height - 7), new(bounds.X + bounds.Width - 6, bounds.Y + bounds.Height - 7), 2, OperationTargetColor);
                    else DrawOutline(bounds, 2, OperationTargetColor);
                }
            }
        }
        if (draft.Rows.Count == 0) Text(mappingEmptyMessage, MappingBounds(20, 142, 960, 46));
        DrawMappingChangeTag();
        if (mappingPickerColumn > 0)
        {
            DrawRectangle(new(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 190));
            var panel = MappingBounds(60, 90, 880, 470);
            DrawRectangle(panel, new Color(24, 29, 36));
            DrawOutline(panel, 2, Color.LightSlateGray);
            Text(mappingPickerColumn == 3 ? "網掛けを選択 — 黒が主色、白が副色" : mappingPickerColumn == 1 ? "主色を選択" : "副色を選択", MappingBounds(80, 108, 840, 38), 23);
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
        DrawMappingButtonTooltip();
    }

    private void DrawMappingButtonTooltip()
    {
        if (modalDialog is not null) return;
        var mouse = Mouse.GetState();
        var hovered = CanShowEditorHover ? mappingEditorButtons.FirstOrDefault(item => Contains(item.Button.Bounds, new(mouse.X, mouse.Y))) : null;
        var focused = !mappingTextFocused && mappingFocus >= 0 && mappingFocus < mappingEditorButtons.Count
            ? mappingEditorButtons[mappingFocus] : null;
        var tooltip = hovered?.Tooltip;
        if (mappingPickerColumn > 0)
            tooltip ??= "見本をクリックして選択　Tab・矢印：移動　Enter：決定　Esc：キャンセル";
        else if (tooltip is null && mappingDraft is { } draft)
        {
            if (CanShowEditorHover)
                for (var row = 0; row < MappingVisibleRows && mappingScroll + row < draft.Rows.Count; row++)
                    for (var column = 1; column < MappingColumnEdges.Length - 1; column++)
                        if (Contains(MappingCell(row, column), new(mouse.X, mouse.Y)))
                            tooltip = column switch
                            {
                                5 => "コメントをクリックして全文を編集します。1000文字以内、空欄で削除できます。",
                                3 or 4 => "黒＝主色、白＝副色。網掛けのセルをクリックしてパターンを選択します。",
                                _ => "色のセルをクリックして主色・副色を選択します。単色の副色は未使用です。",
                            };
            tooltip ??= focused?.Tooltip;
            tooltip ??= mappingKnowledgeComments
                ? "色・網掛け・コメントをクリックして編集　矢印：セル移動　Enter：選択　Tab：操作ボタンへ"
                : "色・網掛けをクリックして編集　矢印：セル移動　Enter：選択　Tab：操作ボタンへ";
        }
        DrawStatusBar(tooltip, $"{mappingKeyLabel}：{mappingDraft?.Rows.Count ?? 0} 件");
    }

    private static Color MappingColorText(Color color) => color.R * 0.299 + color.G * 0.587 + color.B * 0.114 < 145 ? Color.White : Color.Black;
}
