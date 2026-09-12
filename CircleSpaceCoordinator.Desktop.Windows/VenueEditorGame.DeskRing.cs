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

    private string DeskMenuLabel => activeCanvasTool switch
    {
        ToolbarAction.AddDesk => "机追加",
        ToolbarAction.RemoveDesk => "机削除",
        _ => "机",
    };

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
        var radius = Math.Max(0d, Math.Min(105d, Math.Min((width - 128d) / 2d, (height - 68d) / 2d)));
        var halfWidth = Math.Min(56d, width / 2d);
        var halfHeight = Math.Min(26d, height / 2d);
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
            deskRingFocus = (deskRingFocus + (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift) ? 2 : 1)) % 3;
        if (IsPressed(keyboard, Keys.Left) || IsPressed(keyboard, Keys.Up))
            deskRingFocus = (deskRingFocus + 2) % 3;
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
        textRenderer?.Draw("机", ToRectangle(new ScreenRectangle(deskRingCenter.X - 25, deskRingCenter.Y - 18, 50, 36)), Color.White, 21, true);
        for (var index = 0; index < deskRingButtons.Count; index++)
        {
            var button = deskRingButtons[index];
            button.IsSelected = index == deskRingFocus;
            StationeryButtonRenderer.Draw(button,
                (area, color) => DrawRectangle(area, ToButtonColor(color)),
                (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)),
                (area, color) => textRenderer?.Draw(button.AccessibleName, ToRectangle(area, 5), ToButtonColor(color), 17, true));
        }
    }
}
