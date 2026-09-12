namespace CircleSpaceCoordinator.Desktop.Windows;

using StationeryUI.Canvas;
using StationeryUI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

public sealed partial class VenueEditorGame
{
    private sealed record ToolRingEntry(ToolbarAction? Action, string Label, string Description);
    private sealed record ToolRingDefinition(ToolbarAction Menu, IReadOnlyList<ToolRingEntry> Entries, bool CloseAfterAction = true);
    private static readonly ToolRingEntry CancelRingEntry = new(null, "キャンセル", "キャンセル：ツールの選択を変えずにリングを閉じます");
    private static readonly ToolRingDefinition[] ToolRings =
    [
        new(ToolbarAction.NextDirectionMenu,
        [
            new(ToolbarAction.FaceNorth, "上向き", "上向き：次に配置するスペースを上向きにします"),
            new(ToolbarAction.FaceEast, "右向き", "右向き：次に配置するスペースを右向きにします"),
            new(ToolbarAction.FaceSouth, "下向き", "下向き：次に配置するスペースを下向きにします"),
            new(ToolbarAction.FaceWest, "左向き", "左向き：次に配置するスペースを左向きにします"),
            new(null, "キャンセル", "キャンセル：向きを変えずにリングを閉じます"),
        ]),
        new(ToolbarAction.DeskMenu,
        [
            new(ToolbarAction.AddDesk, "スペース追加", "スペース追加：選択後、会場のセルをクリックしてスペースを追加します"),
            new(ToolbarAction.RemoveDesk, "スペース削除", "スペース削除：選択後、スペースをクリックして削除します（サークルが割り当てられたスペースは削除できません）"),
            CancelRingEntry,
        ]),
        new(ToolbarAction.PillarMenu,
        [
            new(ToolbarAction.AddPillar, "柱追加", "柱追加：選択後、会場のセルをクリックして柱を置きます"),
            new(ToolbarAction.RemovePillar, "柱削除", "柱削除：選択後、柱のあるセルをクリックして柱を削除します"),
            CancelRingEntry,
        ]),
        new(ToolbarAction.VenueSizeMenu,
        [
            new(ToolbarAction.ExpandTop, "上側を伸ばす", "会場の上側を１セル伸ばします"),
            new(ToolbarAction.ShrinkTop, "上側を縮める", "会場の上側を１セル縮めます（スペース・柱などがはみ出す場合は変更しません）"),
            new(ToolbarAction.IncreaseWidth, "右側を伸ばす", "会場の右側を１セル伸ばします"),
            new(ToolbarAction.DecreaseWidth, "右側を縮める", "会場の右側を１セル縮めます（スペース・柱などがはみ出す場合は変更しません）"),
            new(ToolbarAction.IncreaseHeight, "下側を伸ばす", "会場の下側を１セル伸ばします"),
            new(ToolbarAction.DecreaseHeight, "下側を縮める", "会場の下側を１セル縮めます（スペース・柱などがはみ出す場合は変更しません）"),
            new(ToolbarAction.ExpandLeft, "左側を伸ばす", "会場の左側を１セル伸ばします"),
            new(ToolbarAction.ShrinkLeft, "左側を縮める", "会場の左側を１セル縮めます（スペース・柱などがはみ出す場合は変更しません）"),
            CancelRingEntry,
        ], CloseAfterAction: false),
    ];
    private ToolRingDefinition toolRingDefinition = ToolRings[0];

    private static ToolbarAction GetToolMenu(ToolbarAction action) =>
        ToolRings.FirstOrDefault(ring => ring.Entries.Any(entry => entry.Action == action))?.Menu ?? action;

    private bool toolRingOpen;
    private string? toolRingResult;
    private bool ShowVenueAboveRingCover => toolRingOpen && toolRingDefinition.Menu == ToolbarAction.VenueSizeMenu;
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
        toolRingResult = null;
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
        var anchor = toolRingDefinition.Menu == ToolbarAction.NextDirectionMenu
            ? NextDirectionBounds : toolbarButtons.Single(button => button.Action == toolRingDefinition.Menu).Model.Bounds;
        var layout = CircleSpaceCoordinator.ReusableControls.RingMenuLayout.Create(
            anchor, width, Math.Max(1, height - StatusBarHeight), toolRingDefinition.Entries.Count);
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
            ActivateToolRingEntry(-1);
            return true;
        }
        if (IsPressed(keyboard, Keys.Tab) || IsPressed(keyboard, Keys.Right) || IsPressed(keyboard, Keys.Down))
            toolRingFocus = (toolRingFocus + (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift) ? toolRingButtons.Count - 1 : 1)) % toolRingButtons.Count;
        if (IsPressed(keyboard, Keys.Left) || IsPressed(keyboard, Keys.Up))
            toolRingFocus = (toolRingFocus + toolRingButtons.Count - 1) % toolRingButtons.Count;
        if (IsPressed(keyboard, Keys.Enter) || IsPressed(keyboard, Keys.Space))
        {
            ActivateToolRingEntry(toolRingFocus);
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
            if (pressed?.Release(pointer) == true) ActivateToolRingEntry(toolRingButtons.IndexOf(pressed));
        }
        return true;
    }

    private void ActivateToolRingEntry(int index)
    {
        if (index >= 0 && toolRingDefinition.Entries[index].Action is { } action)
        {
            var outcome = ExecuteToolbarAction(action, toolRingCenter);
            Log("toolbar_action", success: outcome.Success, detail: $"action={action};{outcome.Detail}");
            if (toolRingDefinition.Menu == ToolbarAction.VenueSizeMenu)
                toolRingResult = rangeSwapStatus = outcome.Success ? "会場サイズを変更しました（リングを閉じてCtrl+Zで元に戻す）"
                    : "会場サイズを変更できません。スペース・柱などが会場外に出ないか、サイズが１セル未満にならないか確認してください";
            if (!toolRingDefinition.CloseAfterAction)
            {
                // Keep the same layout and hover target for repeated clicks. Key input
                // uses press edges, so holding Enter does not repeat the operation.
                pressedToolRingButton?.CancelPress();
                pressedToolRingButton = null;
                return;
            }
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
        if (!ShowVenueAboveRingCover)
            DrawRectangle(new ScreenRectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 170));
        DrawToolRingBand();
        var hoveredIndex = toolRingButtons.FindIndex(button => button.IsPointerOver);
        var description = hoveredIndex >= 0 ? toolRingDefinition.Entries[hoveredIndex].Description
            : "リングのボタンにマウスを合わせると操作説明を表示します　｜　Esc：キャンセル　Ctrl+P：撮影";
        DrawStatusBar(toolRingResult is null ? description : $"{description}　｜　{toolRingResult}");
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

    private void DrawVenueRingPanelCover()
    {
        // The canvas was drawn above its cover. Keep editor panels dimmed and inert.
        var cover = new Color(0, 0, 0, 170);
        DrawRectangle(new ScreenRectangle(0, 0, GraphicsDevice.Viewport.Width, ToolbarHeight), cover);
        var visiblePlans = Math.Min(GetDisplayedPlans().Count, GetVisiblePlanRowCount());
        DrawRectangle(GetPlanListBounds(visiblePlans), cover);
        if (editorMode == EditorMode.DeskPlacement) DrawRectangle(NextSpaceBounds, cover);
        if (ShowsChannels) DrawRectangle(GetChannelPanelBounds(), cover);
    }

    private void DrawVenueSizeIcon(ToolbarAction action, ScreenRectangle bounds, Color color)
    {
        var center = new ScreenPoint(bounds.X + bounds.Width / 2d, bounds.Y + bounds.Height / 2d);
        var scale = Math.Min(bounds.Width, bounds.Height) / 44d;
        DrawOutline(new ScreenRectangle(center.X - 9 * scale, center.Y - 9 * scale, 18 * scale, 18 * scale), 2 * scale, color);
        if (action == ToolbarAction.VenueSizeMenu) return;
        var direction = action switch
        {
            ToolbarAction.ExpandTop or ToolbarAction.ShrinkTop => new ScreenPoint(0, -1),
            ToolbarAction.IncreaseWidth or ToolbarAction.DecreaseWidth => new ScreenPoint(1, 0),
            ToolbarAction.IncreaseHeight or ToolbarAction.DecreaseHeight => new ScreenPoint(0, 1),
            _ => new ScreenPoint(-1, 0),
        };
        var expanding = action is ToolbarAction.ExpandTop or ToolbarAction.IncreaseWidth or ToolbarAction.IncreaseHeight or ToolbarAction.ExpandLeft;
        var startDistance = expanding ? 7d : 19d;
        var endDistance = expanding ? 19d : 1d;
        ScreenPoint Point(double distance, double side = 0) => new(
            center.X + (direction.X * distance - direction.Y * side) * scale,
            center.Y + (direction.Y * distance + direction.X * side) * scale);
        DrawLine(Point(startDistance), Point(endDistance), 2 * scale, color);
        var wingDistance = endDistance + (expanding ? -5 : 5);
        DrawLine(Point(endDistance), Point(wingDistance, -4), 2 * scale, color);
        DrawLine(Point(endDistance), Point(wingDistance, 4), 2 * scale, color);
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
