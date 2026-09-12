namespace CircleSpaceCoordinator.Desktop.Windows;

using StationeryUI.Canvas;
using StationeryUI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

public sealed partial class VenueEditorGame
{
    private bool deskRingOpen;
    private bool deskRingInputDrain;
    private readonly List<IconButtonModel> deskRingButtons = [];
    private IconButtonModel? pressedDeskRingButton;
    private int deskRingFocus;
    private int deskRingWidth;
    private int deskRingHeight;
    private ScreenPoint deskRingCenter;
    private double deskRingRadius;

    private void OpenDeskRing()
    {
        CancelInProgressPointerInteraction();
        deskRingOpen = true;
        deskRingInputDrain = true;
        deskRingFocus = activeCanvasTool == ToolbarAction.RemoveDesk ? 1 : 0;
        deskRingWidth = -1;
        EnsureDeskRingButtons();
    }

    private void EnsureDeskRingButtons()
    {
        var width = GraphicsDevice.Viewport.Width;
        var height = GraphicsDevice.Viewport.Height;
        if (deskRingWidth == width && deskRingHeight == height) return;
        deskRingWidth = width;
        deskRingHeight = height;
        pressedDeskRingButton?.CancelPress();
        pressedDeskRingButton = null;
        deskRingButtons.Clear();
        var anchor = toolbarButtons.Single(button => button.Action == ToolbarAction.DeskMenu).Model.Bounds;
        // Shift the whole ring into the window when its toolbar anchor is near an edge.
        var radius = Math.Max(0d, Math.Min(72d, Math.Min((width - 60d) / 2d, (height - 60d) / 2d)));
        deskRingRadius = radius;
        var halfWidth = Math.Min(22d, Math.Min(width, height) / 2d);
        var halfHeight = halfWidth;
        var extentX = radius + halfWidth;
        var extentY = radius + halfHeight;
        deskRingCenter = new ScreenPoint(
            Math.Clamp(anchor.X + anchor.Width / 2d, extentX, Math.Max(extentX, width - extentX)),
            Math.Clamp(anchor.Y + anchor.Height / 2d, extentY, Math.Max(extentY, height - extentY)));
        string[] labels = ["机追加", "机削除", "キャンセル"];
        for (var index = 0; index < labels.Length; index++)
        {
            var angle = (-90d + index * 120d) * Math.PI / 180d;
            deskRingButtons.Add(new IconButtonModel(new ScreenRectangle(
                deskRingCenter.X + Math.Cos(angle) * radius - halfWidth,
                deskRingCenter.Y + Math.Sin(angle) * radius - halfHeight,
                halfWidth * 2d, halfHeight * 2d), labels[index]));
        }
    }

    private bool UpdateDeskRing(KeyboardState keyboard, MouseState mouse)
    {
        if (deskRingInputDrain)
        {
            // Drain every mouse button as well as keys so closing cannot pan, edit or exit.
            if (mouse.LeftButton == ButtonState.Released && mouse.RightButton == ButtonState.Released &&
                mouse.MiddleButton == ButtonState.Released && keyboard.GetPressedKeys().Length == 0)
                deskRingInputDrain = false;
            return true;
        }
        if (!deskRingOpen) return false;
        EnsureDeskRingButtons();
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        foreach (var button in deskRingButtons) button.UpdatePointer(pointer);
        if (IsPressed(keyboard, Keys.Escape))
        {
            CloseDeskRing(2);
            return true;
        }
        if (IsPressed(keyboard, Keys.Tab) || IsPressed(keyboard, Keys.Right) || IsPressed(keyboard, Keys.Down))
            deskRingFocus = (deskRingFocus + (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift) ? deskRingButtons.Count - 1 : 1)) % deskRingButtons.Count;
        if (IsPressed(keyboard, Keys.Left) || IsPressed(keyboard, Keys.Up))
            deskRingFocus = (deskRingFocus + deskRingButtons.Count - 1) % deskRingButtons.Count;
        if (IsPressed(keyboard, Keys.Enter) || IsPressed(keyboard, Keys.Space))
        {
            CloseDeskRing(deskRingFocus);
            return true;
        }
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            pressedDeskRingButton = deskRingButtons.FirstOrDefault(button => button.Press(pointer));
            if (pressedDeskRingButton is not null) deskRingFocus = deskRingButtons.IndexOf(pressedDeskRingButton);
        }
        if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
        {
            var pressed = pressedDeskRingButton;
            pressedDeskRingButton = null;
            if (pressed?.Release(pointer) == true) CloseDeskRing(deskRingButtons.IndexOf(pressed));
        }
        return true;
    }

    private void CloseDeskRing(int index)
    {
        if (index < 2)
        {
            var action = index == 0 ? ToolbarAction.AddDesk : ToolbarAction.RemoveDesk;
            var outcome = ExecuteToolbarAction(action, deskRingCenter);
            Log("toolbar_action", success: outcome.Success, detail: $"action={action};{outcome.Detail}");
        }
        deskRingOpen = false;
        deskRingInputDrain = true;
        CancelInProgressPointerInteraction();
        UpdateToolbar(new ScreenPoint(-1, -1));
    }

    private void DrawDeskRing()
    {
        if (!deskRingOpen) return;
        EnsureDeskRingButtons();
        DrawRectangle(new ScreenRectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 170));
        DrawDeskRingBand();
        var hoveredIndex = deskRingButtons.FindIndex(button => button.IsPointerOver);
        DrawStatusBar(hoveredIndex switch
        {
            0 => "机追加：選択後、会場のセルをクリックして机を追加します",
            1 => "机削除：選択後、机をクリックして削除します（サークルが割り当てられた机は削除できません）",
            2 => "キャンセル：ツールの選択を変えずにリングを閉じます",
            _ => "リングのボタンにマウスを合わせると操作説明を表示します　｜　Esc：キャンセル　Ctrl+P：撮影",
        });
        for (var index = 0; index < deskRingButtons.Count; index++)
        {
            var button = deskRingButtons[index];
            // Ring entries choose a tool; keyboard focus is not an active-tool selection.
            button.IsSelected = false;
            StationeryButtonRenderer.Draw(button,
                (area, color) => DrawRectangle(area, ToButtonColor(color)),
                (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)),
                (area, color) =>
                {
                    if (index == 2)
                    {
                        var center = new ScreenPoint(area.X + area.Width / 2d, area.Y + area.Height / 2d);
                        DrawLine(new ScreenPoint(center.X - 8, center.Y - 8), new ScreenPoint(center.X + 8, center.Y + 8), 3, ToButtonColor(color));
                        DrawLine(new ScreenPoint(center.X + 8, center.Y - 8), new ScreenPoint(center.X - 8, center.Y + 8), 3, ToButtonColor(color));
                    }
                    else
                        DrawToolbarIcon(index == 0 ? ToolbarAction.AddDesk : ToolbarAction.RemoveDesk,
                            area, ToButtonColor(color));
                });
        }
    }

    private void DrawDeskRingBand()
    {
        if (deskRingRadius <= 0d) return;
        var thickness = Math.Min(24d, deskRingRadius);
        DrawRing(deskRingRadius, thickness, new Color(57, 78, 86));
        DrawRing(deskRingRadius - thickness / 2d, 1.5d, new Color(111, 149, 157));
        DrawRing(deskRingRadius + thickness / 2d, 1.5d, new Color(111, 149, 157));

        void DrawRing(double radius, double lineWidth, Color color)
        {
            const int segments = 128;
            for (var index = 0; index < segments; index++)
            {
                var start = Math.Tau * index / segments;
                // Slightly overlap opaque segments to keep the band continuous.
                var end = Math.Tau * (index + 1.1d) / segments;
                DrawLine(
                    new ScreenPoint(deskRingCenter.X + Math.Cos(start) * radius, deskRingCenter.Y + Math.Sin(start) * radius),
                    new ScreenPoint(deskRingCenter.X + Math.Cos(end) * radius, deskRingCenter.Y + Math.Sin(end) * radius),
                    lineWidth, color);
            }
        }
    }
}
