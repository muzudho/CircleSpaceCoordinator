namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;
using StationeryUI.Windows;
using StationeryUI.MonoGame.Controls.ActionBadge;

public sealed partial class VenueEditorGame
{
    private readonly ChangeTagEditorView mappingTagView = new();
    private bool mappingTextFocused;
    private bool mappingTextSuppressExit;
    private string mappingComposition = "";
    private (int Start, int End) mappingTextRange;
    private ScreenRectangle MappingLogBounds => MappingBounds(36, 536, 680, 36);
    private ScreenRectangle MappingBadgeBounds => MappingBounds(608, 544, 100, 26);
    private ScreenRectangle MappingCloseBounds => MappingBounds(728, 536, 100, 36);
    private ScreenRectangle MappingDiscardBounds => MappingBounds(840, 536, 100, 36);

    private string? ValidateMappingChangeLog(string value)
    {
        try
        {
            PersonCredits.NormalizeChangeLog(value);
            if (PersonCredits.NormalizeHandle(Handle).Length == 0) return "画面上部で作業者を設定してください。";
            return null;
        }
        catch (ArgumentException ex) { return ex.Message; }
    }

    private void SyncMappingChangeTag()
    {
        if (mappingChangeTag is not null) mappingChangeTag.HasChanges = mappingDraft?.HasChanges == true;
    }

    private void SetMappingTextFocus(bool focused)
    {
        mappingTextFocused = focused;
        mappingComposition = "";
        ResetUnderlineKeyRepeat();
        if (focused)
        {
            textInputService ??= new WindowsTextInputService(Window.Handle);
            textInputService.Start();
            mappingFocus = -1;
        }
        else textInputService?.Stop();
    }

    private bool UpdateMappingChangeTag(KeyboardState keyboard, MouseState mouse)
    {
        if (mappingChangeTag is not { } tag) return false;
        var editor = tag.Editor;
        var hadComposition = mappingComposition.Length > 0;
        var updates = mappingTextFocused ? textInputService?.DrainUpdates() : null;
        if (updates is not null)
            foreach (var update in updates)
            {
                if (update.IsComposition) mappingComposition = update.Text;
                else { tag.Insert(update.Text); mappingComposition = ""; }
            }
        if (hadComposition || mappingComposition.Length > 0 || updates?.Count > 0) mappingTextSuppressExit = true;
        else if (keyboard.IsKeyUp(Keys.Enter) && keyboard.IsKeyUp(Keys.Escape)) mappingTextSuppressExit = false;

        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            if (Contains(MappingLogBounds, pointer) && tag.HasChanges)
            {
                if (mappingComposition.Length > 0) return true;
                SetMappingTextFocus(true);
                if (!Contains(MappingBadgeBounds, pointer))
                {
                    var start = Math.Min(mappingTextRange.Start, editor.Text.Length);
                    var end = Math.Min(mappingTextRange.End, editor.Text.Length);
                    var size = Math.Max(10, (int)(ChangeTagEditorView.InputFontSize * MappingEditorScale));
                    var boundaries = System.Globalization.StringInfo.ParseCombiningCharacters(editor.Text).Append(editor.Text.Length);
                    var nearest = boundaries.Where(i => i >= start && i <= end).DefaultIfEmpty(start)
                        .MinBy(i => Math.Abs(MappingLogBounds.X + (textRenderer?.Measure(editor.Text[start..i], size).X ?? 0) - mouse.X));
                    editor.MoveTo(nearest, keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
                }
                return true;
            }
            if (mappingTextFocused) SetMappingTextFocus(false);
        }
        if (IsPressed(keyboard, Keys.Tab))
        {
            var reverse = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
            var closeIndex = mappingEditorButtons.FindIndex(item => item.Button.AccessibleName == "閉じる");
            if (mappingTextFocused)
            {
                SetMappingTextFocus(false);
                mappingFocus = closeIndex - (reverse ? 1 : 0);
                return true;
            }
            if (tag.HasChanges && mappingFocus == closeIndex - (reverse ? 0 : 1))
            {
                SetMappingTextFocus(true);
                return true;
            }
        }
        // IME confirmation must never trigger Close, even on the same input frame.
        if (mappingTextSuppressExit && (keyboard.IsKeyDown(Keys.Escape) || keyboard.IsKeyDown(Keys.Enter))) return true;
        if (!mappingTextFocused) return false;
        if (IsPressed(keyboard, Keys.Escape) || IsControlDown(keyboard) && IsPressed(keyboard, Keys.S))
        {
            if (!mappingTextSuppressExit && mappingComposition.Length == 0) SaveStyleMapping();
            return true;
        }
        if (mappingComposition.Length > 0) return true;
        var shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        if (IsPressed(keyboard, Keys.Left)) editor.Move(-1, shift);
        if (IsPressed(keyboard, Keys.Right)) editor.Move(1, shift);
        if (IsPressed(keyboard, Keys.Home)) editor.MoveTo(0, shift);
        if (IsPressed(keyboard, Keys.End)) editor.MoveTo(editor.Text.Length, shift);
        if (underlineBackRepeat.Update(keyboard.IsKeyDown(Keys.Back), IsPressed(keyboard, Keys.Back), statusHintTime, true))
        { editor.Delete(true); tag.Edited(); }
        if (underlineDeleteRepeat.Update(keyboard.IsKeyDown(Keys.Delete), IsPressed(keyboard, Keys.Delete), statusHintTime, true))
        { editor.Delete(false); tag.Edited(); }
        if (IsControlDown(keyboard))
        {
            if (IsPressed(keyboard, Keys.A)) editor.SelectAll();
            if (IsPressed(keyboard, Keys.Z)) { editor.Undo(); tag.Edited(); }
            if (IsPressed(keyboard, Keys.Y)) { editor.Redo(); tag.Edited(); }
            try
            {
                if (editor.SelectionLength > 0 && (IsPressed(keyboard, Keys.C) || IsPressed(keyboard, Keys.X)))
                {
                    textInputService!.WriteClipboard(editor.SelectedText);
                    if (IsPressed(keyboard, Keys.X)) { editor.Delete(false); tag.Edited(); }
                }
                if (IsPressed(keyboard, Keys.V)) tag.Insert(textInputService!.ReadClipboard());
            }
            catch (System.Runtime.InteropServices.ExternalException)
            { tag.SaveFailed("クリップボードを使用できません。もう一度操作してください。"); }
        }
        return true;
    }

    private void DrawMappingChangeTag()
    {
        if (mappingChangeTag is not { } tag || mappingDraft is not { } draft) return;
        void Text(string value, ScreenRectangle bounds, int size = 14, Color? color = null) =>
            textRenderer?.Draw(value, ToRectangle(bounds), color ?? Color.White, Math.Max(10, (int)(size * MappingEditorScale)));
        var size = Math.Max(10, (int)(ChangeTagEditorView.InputFontSize * MappingEditorScale));
        var mouse = Mouse.GetState();
        mappingTagView.Draw(tag, MappingBounds(20, 502, tag.HasChanges ? 936 : 824, 98), MappingEditorScale,
            mappingTextFocused, Contains(MappingLogBounds, new(mouse.X, mouse.Y)),
            draft.AttributionSummary(mappingPreviousCredits, Handle, WorkDate),
            mappingComposition,
            value => textRenderer?.Measure(value, size).X ?? 0,
            (value, area, fontSize, ink) => Text(value, area, fontSize, MappingInk(ink)),
            (area, ink) => DrawRectangle(area, MappingInk(ink)),
            _ => DrawMappingActionBadge(), previousLog: mappingPreviousCredits?.ChangeLog,
            actionAreaWidth: tag.HasChanges ? 240 : 128);
        mappingTextRange = mappingTagView.VisibleRange;
        if (mappingTextFocused) textInputService?.SetInputArea(mappingTagView.CaretBounds);
    }

    private void DrawMappingActionBadge()
    {
        // Reuse the library's badge label, placement and visibility. Adapt its rounded
        // drawing to this app's pixel-coordinate SpriteBatch (no separate canvas/font).
        var badge = ActionBadgeComponent.Create("change", new Rectangle(0, 0, 680, 36));
        badge.Show();
        DrawMappingBadge(badge, MappingLogBounds);
    }

    private void DrawMappingBadge(ActionBadgeComponent badge, ScreenRectangle anchor)
    {
        if (!badge.IsVisible) return;
        var local = badge.Bounds;
        var bounds = new ScreenRectangle(anchor.X + local.X * MappingEditorScale, anchor.Y + local.Y * MappingEditorScale,
            local.Width * MappingEditorScale, local.Height * MappingEditorScale);
        var rectangle = ToRectangle(bounds);
        var radius = Math.Max(1, Math.Min((int)Math.Round(6 * MappingEditorScale), rectangle.Height / 2));
        // Scanlines provide the same six-pixel rounded silhouette on the host canvas.
        for (var y = 0; y < rectangle.Height; y++)
        {
            var dy = y < radius ? radius - y - 0.5 : y >= rectangle.Height - radius ? y - (rectangle.Height - radius) + 0.5 : 0;
            var inset = dy > 0 ? (int)Math.Ceiling(radius - Math.Sqrt(Math.Max(0, radius * radius - dy * dy))) : 0;
            DrawRectangle(new(rectangle.X + inset, rectangle.Y + y, rectangle.Width - 2 * inset, 1), MappingInk(ChangeTagInk.Badge));
        }
        var fontSize = Math.Max(10, (int)(16 * MappingEditorScale));
        var measured = textRenderer?.Measure(badge.Label, fontSize) ?? Point.Zero;
        textRenderer?.Draw(badge.Label, new Rectangle(rectangle.X + Math.Max(0, (rectangle.Width - measured.X) / 2),
            rectangle.Y, Math.Min(rectangle.Width, measured.X), rectangle.Height), MappingInk(ChangeTagInk.BadgeText), fontSize);
    }

    private static Color MappingInk(ChangeTagInk ink) => ink switch
    {
        ChangeTagInk.Muted => new Color(93, 83, 65),
        ChangeTagInk.Placeholder => new Color(115, 105, 86),
        ChangeTagInk.Selection => new Color(184, 211, 205),
        ChangeTagInk.Underline => new Color(45, 115, 89),
        ChangeTagInk.Badge => new Color(185, 196, 255),
        ChangeTagInk.BadgeText => new Color(15, 20, 31),
        ChangeTagInk.Error => new Color(155, 48, 35),
        ChangeTagInk.Composition => new Color(167, 105, 15),
        ChangeTagInk.Paper => new Color(235, 224, 200),
        ChangeTagInk.Shadow => new Color(255, 250, 240, 100),
        _ => new Color(49, 43, 33),
    };
}
