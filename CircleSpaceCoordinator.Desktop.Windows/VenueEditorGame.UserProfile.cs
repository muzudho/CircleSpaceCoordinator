namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private bool userProfileOpen;
    private bool userProfilePromptPending;
    private bool userProfileOpenedForStartup;
    private readonly List<IconButtonModel> userProfileButtons = [];
    private IconButtonModel? pressedUserProfileButton;
    private int userProfileFocus;
    private int userProfileWidth = -1;
    private int userProfileHeight = -1;

    private ScreenRectangle UserProfileHeaderBounds =>
        new(12, 2, Math.Min(204, Math.Max(1, WorkerInputBounds.X - 20)), 28);

    private ScreenRectangle UserProfilePanelBounds
    {
        get
        {
            var width = Math.Min(720, Math.Max(1, GraphicsDevice.Viewport.Width - 24));
            var height = Math.Min(380, Math.Max(1, GraphicsDevice.Viewport.Height - WorkerBarHeight - 72));
            return new((GraphicsDevice.Viewport.Width - width) / 2, WorkerBarHeight + 36, width, height);
        }
    }

    private void OpenUserProfile()
    {
        if (userProfileOpen) return;
        CancelInProgressPointerInteraction();
        userProfileOpen = true;
        userProfileFocus = 0;
        userProfileWidth = -1;
        EnsureUserProfileButtons();
        if (string.IsNullOrWhiteSpace(Handle)) EditUserProfileHandle();
    }

    private void EditUserProfileHandle() => OpenUnderlineInput(
        "作業者名", Handle, value =>
        {
            var handle = PersonCredits.NormalizeHandle(value);
            if (handle.Length == 0) throw new ArgumentException("作業者名を入力してください。");
            settings!.SaveHandle(handle);
            if (userProfileOpenedForStartup)
            {
                userProfileOpenedForStartup = false;
                userProfileOpen = false;
                if (workspace is not null) RequestReturnToEvents();
            }
        },
        "このPCで作業するときの名前を入力してください。変更履歴の作業者として記録します。\n後から［ユーザー・プロフィール］で変更できます。",
        maxLength: 64,
        validate: value =>
        {
            try
            {
                return PersonCredits.NormalizeHandle(value).Length == 0
                    ? "作業者名を入力してください。" : null;
            }
            catch (ArgumentException ex) { return ex.Message; }
        },
        requireValidInput: true,
        characterLimit: PersonCredits.MaximumHandleLength);

    private void OpenUserSettingsFolder()
    {
        try
        {
            var path = settings!.DirectoryPath;
            Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { ShowInAppMessage("設定フォルダーを開けませんでした", ex.Message); }
    }

    private void EnsureUserProfileButtons()
    {
        if (userProfileWidth == GraphicsDevice.Viewport.Width && userProfileHeight == GraphicsDevice.Viewport.Height) return;
        userProfileWidth = GraphicsDevice.Viewport.Width;
        userProfileHeight = GraphicsDevice.Viewport.Height;
        pressedUserProfileButton?.CancelPress();
        pressedUserProfileButton = null;
        userProfileButtons.Clear();
        var panel = UserProfilePanelBounds;
        var buttonWidth = Math.Max(1, panel.Width - 48);
        userProfileButtons.Add(new(new(panel.X + 24, panel.Y + 184, buttonWidth, 42), "作業者名を変更"));
        userProfileButtons.Add(new(new(panel.X + 24, panel.Y + 236, buttonWidth, 42), "設定フォルダーを開く"));
        userProfileButtons.Add(new(new(panel.X + 24, panel.Y + 288, buttonWidth, 42), "戻る"));
    }

    private void ActivateUserProfileButton(int index)
    {
        switch (index)
        {
            case 0: EditUserProfileHandle(); break;
            case 1: OpenUserSettingsFolder(); break;
            case 2 when !string.IsNullOrWhiteSpace(Handle): userProfileOpen = false; break;
        }
    }

    private void UpdateUserProfile(KeyboardState keyboard, MouseState mouse)
    {
        EnsureUserProfileButtons();
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        for (var i = 0; i < userProfileButtons.Count; i++)
        {
            userProfileButtons[i].IsEnabled = i != 2 || !string.IsNullOrWhiteSpace(Handle);
            userProfileButtons[i].UpdatePointer(pointer);
        }
        if (IsPressed(keyboard, Keys.Tab) || IsPressed(keyboard, Keys.Down))
            userProfileFocus = (userProfileFocus + 1) % userProfileButtons.Count;
        if (IsPressed(keyboard, Keys.Up))
            userProfileFocus = (userProfileFocus + userProfileButtons.Count - 1) % userProfileButtons.Count;
        if (IsPressed(keyboard, Keys.Escape) && !string.IsNullOrWhiteSpace(Handle))
        {
            userProfileOpen = false;
            return;
        }
        if ((IsPressed(keyboard, Keys.Enter) || IsPressed(keyboard, Keys.Space)) && userProfileButtons[userProfileFocus].IsEnabled)
        {
            ActivateUserProfileButton(userProfileFocus);
            return;
        }
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            pressedUserProfileButton = userProfileButtons.FirstOrDefault(button => button.Press(pointer));
            if (pressedUserProfileButton is not null) userProfileFocus = userProfileButtons.IndexOf(pressedUserProfileButton);
        }
        if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
        {
            var pressed = pressedUserProfileButton;
            pressedUserProfileButton = null;
            if (pressed?.Release(pointer) == true) ActivateUserProfileButton(userProfileButtons.IndexOf(pressed));
        }
    }

    private void DrawUserProfile()
    {
        EnsureUserProfileButtons();
        var panel = UserProfilePanelBounds;
        DrawRectangle(panel, new Color(28, 39, 50));
        DrawOutline(panel, 1, new Color(109, 155, 169));
        textRenderer?.Draw("ユーザー・プロフィール", ToRectangle(new(panel.X + 24, panel.Y + 18, panel.Width - 48, 42)), Color.White, 28, true);
        textRenderer?.Draw("作業者名", ToRectangle(new(panel.X + 24, panel.Y + 78, panel.Width - 48, 28)), Color.LightGray, 17);
        textRenderer?.Draw(string.IsNullOrWhiteSpace(Handle) ? "未設定" : Handle,
            ToRectangle(new(panel.X + 24, panel.Y + 110, panel.Width - 48, 40)), Color.White, 24, true);
        textRenderer?.Draw("設定フォルダー：" + settings!.DirectoryPath,
            ToRectangle(new(panel.X + 24, panel.Y + 150, panel.Width - 48, 25)), Color.LightGray, 14);
        for (var i = 0; i < userProfileButtons.Count; i++)
        {
            var button = userProfileButtons[i];
            button.IsEnabled = i != 2 || !string.IsNullOrWhiteSpace(Handle);
            OperationButtonRenderer.Draw(button,
                (area, color) => DrawRectangle(area, ToButtonColor(color)),
                (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)),
                (area, color) => textRenderer?.Draw(button.AccessibleName, ToRectangle(area, 10), ToButtonColor(color), 18, true));
            if (i == userProfileFocus && button.IsEnabled) DrawOutline(button.Bounds, 2, OperationTargetColor);
        }
        if (string.IsNullOrWhiteSpace(Handle))
            textRenderer?.Draw("作業者名を保存すると、作業を始められます。",
                ToRectangle(new(panel.X + 24, panel.Y + 340, panel.Width - 48, 26)), Color.LightGoldenrodYellow, 15);
    }
}
