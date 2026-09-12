namespace CircleSpaceCoordinator.Desktop.Windows;

using StationeryUI.Canvas;
using StationeryUI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

public sealed partial class VenueEditorGame
{
    private sealed record ToolRingEntry(ToolbarAction? Action, string Label, string Description);
    private sealed record ToolRingDefinition(ToolbarAction Menu, IReadOnlyList<ToolRingEntry> Entries);
    private static readonly ToolRingEntry CancelRingEntry = new(null, "キャンセル", "キャンセル：ツールの選択を変えずにリングを閉じます");
    private static readonly ToolRingDefinition[] ToolRings =
    [
        new(ToolbarAction.DeskMenu,
        [
            new(ToolbarAction.AddDesk, "机追加", "机追加：選択後、会場のセルをクリックして机を追加します"),
            new(ToolbarAction.RemoveDesk, "机削除", "机削除：選択後、机をクリックして削除します（サークルが割り当てられた机は削除できません）"),
            CancelRingEntry,
        ]),
        new(ToolbarAction.PillarMenu,
        [
            new(ToolbarAction.AddPillar, "柱追加", "柱追加：選択後、会場のセルをクリックして柱を置きます"),
            new(ToolbarAction.RemovePillar, "柱削除", "柱削除：選択後、柱のあるセルをクリックして柱を削除します"),
            CancelRingEntry,
        ]),
    ];
    private ToolRingDefinition toolRingDefinition = ToolRings[0];

    private static ToolbarAction GetToolMenu(ToolbarAction action) =>
        ToolRings.FirstOrDefault(ring => ring.Entries.Any(entry => entry.Action == action))?.Menu ?? action;

    private bool toolRingOpen;
    private bool toolRingInputDrain;
    private readonly List<IconButtonModel> toolRingButtons = [];
    private IconButtonModel? pressedToolRingButton;
    private int toolRingFocus;
    private int toolRingWidth;
    private int toolRingHeight;
    private ScreenPoint toolRingCenter;
    private double toolRingRadius;

    private void OpenToolRing(ToolbarAction menu)
    {
        CancelInProgressPointerInteraction();
        toolRingDefinition = ToolRings.Single(ring => ring.Menu == menu);
        toolRingOpen = true;
        toolRingInputDrain = true;
        toolRingFocus = Math.Max(0, toolRingDefinition.Entries.ToList().FindIndex(entry => entry.Action == activeCanvasTool));
        toolRingWidth = -1;
        EnsureToolRingButtons();
    }

    private void EnsureToolRingButtons()
    {
        var width = GraphicsDevice.Viewport.Width;
        var height = GraphicsDevice.Viewport.Height;
        if (toolRingWidth == width && toolRingHeight == height) return;
        toolRingWidth = width;
        toolRingHeight = height;
        pressedToolRingButton?.CancelPress();
        pressedToolRingButton = null;
        toolRingButtons.Clear();
        var anchor = toolbarButtons.Single(button => button.Action == toolRingDefinition.Menu).Model.Bounds;
        var layout = CircleSpaceCoordinator.ReusableControls.RingMenuLayout.Create(
            anchor, width, height, toolRingDefinition.Entries.Count);
        toolRingCenter = layout.Center;
        toolRingRadius = layout.Radius;
        for (var index = 0; index < layout.Buttons.Count; index++)
            toolRingButtons.Add(new IconButtonModel(layout.Buttons[index], toolRingDefinition.Entries[index].Label));
    }
    private bool UpdateToolRing(KeyboardState keyboard, MouseState mouse)
    {
        if (toolRingInputDrain)
        {
            // Drain every mouse button as well as keys so closing cannot pan, edit or exit.
            if (mouse.LeftButton == ButtonState.Released && mouse.RightButton == ButtonState.Released &&
                mouse.MiddleButton == ButtonState.Released && keyboard.GetPressedKeys().Length == 0)
                toolRingInputDrain = false;
            return true;
        }
        if (!toolRingOpen) return false;
        EnsureToolRingButtons();
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        foreach (var button in toolRingButtons) button.UpdatePointer(pointer);
        if (IsPressed(keyboard, Keys.Escape))
        {
            CloseToolRing(-1);
            return true;
        }
        if (IsPressed(keyboard, Keys.Tab) || IsPressed(keyboard, Keys.Right) || IsPressed(keyboard, Keys.Down))
            toolRingFocus = (toolRingFocus + (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift) ? toolRingButtons.Count - 1 : 1)) % toolRingButtons.Count;
        if (IsPressed(keyboard, Keys.Left) || IsPressed(keyboard, Keys.Up))
            toolRingFocus = (toolRingFocus + toolRingButtons.Count - 1) % toolRingButtons.Count;
        if (IsPressed(keyboard, Keys.Enter) || IsPressed(keyboard, Keys.Space))
        {
            CloseToolRing(toolRingFocus);
            return true;
        }
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            pressedToolRingButton = toolRingButtons.FirstOrDefault(button => button.Press(pointer));
            if (pressedToolRingButton is not null) toolRingFocus = toolRingButtons.IndexOf(pressedToolRingButton);
        }
        if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
        {
            var pressed = pressedToolRingButton;
            pressedToolRingButton = null;
            if (pressed?.Release(pointer) == true) CloseToolRing(toolRingButtons.IndexOf(pressed));
        }
        return true;
    }

    private void CloseToolRing(int index)
    {
        if (index >= 0 && toolRingDefinition.Entries[index].Action is { } action)
        {
            var outcome = ExecuteToolbarAction(action, toolRingCenter);
            Log("toolbar_action", success: outcome.Success, detail: $"action={action};{outcome.Detail}");
        }
        toolRingOpen = false;
        toolRingInputDrain = true;
        CancelInProgressPointerInteraction();
        UpdateToolbar(new ScreenPoint(-1, -1));
    }

    private void DrawToolRing()
    {
        if (!toolRingOpen) return;
        EnsureToolRingButtons();
        DrawRectangle(new ScreenRectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 170));
        DrawToolRingBand();
        var hoveredIndex = toolRingButtons.FindIndex(button => button.IsPointerOver);
        DrawStatusBar(hoveredIndex >= 0 ? toolRingDefinition.Entries[hoveredIndex].Description
            : "リングのボタンにマウスを合わせると操作説明を表示します　｜　Esc：キャンセル　Ctrl+P：撮影");
        for (var index = 0; index < toolRingButtons.Count; index++)
        {
            var button = toolRingButtons[index];
            // Ring entries choose a tool; keyboard focus is not an active-tool selection.
            button.IsSelected = false;
            StationeryButtonRenderer.Draw(button,
                (area, color) => DrawRectangle(area, ToButtonColor(color)),
                (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)),
                (area, color) =>
                {
                    if (toolRingDefinition.Entries[index].Action is not { } action)
                    {
                        var center = new ScreenPoint(area.X + area.Width / 2d, area.Y + area.Height / 2d);
                        DrawLine(new ScreenPoint(center.X - 8, center.Y - 8), new ScreenPoint(center.X + 8, center.Y + 8), 3, ToButtonColor(color));
                        DrawLine(new ScreenPoint(center.X + 8, center.Y - 8), new ScreenPoint(center.X - 8, center.Y + 8), 3, ToButtonColor(color));
                    }
                    else
                        DrawToolbarIcon(action, area, ToButtonColor(color));
                });
        }
    }

    private void DrawToolRingBand()
    {
        if (toolRingRadius <= 0d) return;
        var thickness = Math.Min(24d, toolRingRadius);
        DrawRing(toolRingRadius, thickness, new Color(57, 78, 86));
        DrawRing(toolRingRadius - thickness / 2d, 1.5d, new Color(111, 149, 157));
        DrawRing(toolRingRadius + thickness / 2d, 1.5d, new Color(111, 149, 157));

        void DrawRing(double radius, double lineWidth, Color color)
        {
            const int segments = 128;
            for (var index = 0; index < segments; index++)
            {
                var start = Math.Tau * index / segments;
                // Slightly overlap opaque segments to keep the band continuous.
                var end = Math.Tau * (index + 1.1d) / segments;
                DrawLine(
                    new ScreenPoint(toolRingCenter.X + Math.Cos(start) * radius, toolRingCenter.Y + Math.Sin(start) * radius),
                    new ScreenPoint(toolRingCenter.X + Math.Cos(end) * radius, toolRingCenter.Y + Math.Sin(end) * radius),
                    lineWidth, color);
            }
        }
    }
}
