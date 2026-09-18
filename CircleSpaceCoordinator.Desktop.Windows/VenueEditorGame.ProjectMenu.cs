namespace CircleSpaceCoordinator.Desktop.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private bool projectMenuOpen;
    private int projectToolbarWidth;
    private bool projectMenuDrain;
    private readonly List<IconButtonModel> projectMenuButtons = [];
    private IconButtonModel? pressedProjectMenuButton;
    private int projectMenuFocus;
    private ScreenRectangle projectMenuBounds;
    private string projectMenuStatus = "";
    private (object Workspace, long Revision, string Path, double Zoom, double X, double Y)? savedProjectState;
    private bool IsCurrentProjectSaved => workspace is not null && savedProjectState is { } saved &&
        ReferenceEquals(saved.Workspace, workspace) && saved.Revision == workspace.Revision && saved.Path == projectSavePath &&
        saved.Zoom == viewport.Zoom && saved.X == viewport.Origin.X && saved.Y == viewport.Origin.Y;
    private string ProjectMenuDescription(int index) => index == 1 && IsCurrentProjectSaved
        ? "現在の変更は保存できています。変更すると再び保存できます。" : ProjectMenuDescriptions[index];
    private bool IsProjectMenuEntryEnabled(int index) => index == 5 || workspace is not null && optimizationTask is null &&
        (index != 1 || projectSavePath is not null && !IsCurrentProjectSaved);
    private static readonly string[] ProjectMenuLabels =
        ["開く…（イベント一覧から選択）", "保存", "一部を書き出す…", "一部を取り込む…", "閉じる（イベント一覧へ）", "×"];
    private static readonly string[] ProjectMenuDescriptions =
    [
        "現在のプロジェクトを閉じ、イベント一覧で開くプロジェクトを選びます。保存確認があります。",
        "現在のイベントプロジェクトを保存します。Ctrl+Sならメニューを開かずに保存できます。",
        "選んだ配置案・素材・知見をパッケージとして書き出します。",
        "パッケージから選んだ内容を現在のイベントプロジェクトへ取り込みます。",
        "保存するか確認してプロジェクトを閉じ、イベント一覧へ移ります。",
        "メニューを閉じて作業に戻ります。",
    ];

    private void OpenProjectMenu()
    {
        CancelInProgressPointerInteraction();
        projectMenuOpen = projectMenuDrain = true;
        projectMenuFocus = 0;
        projectMenuStatus = "";
        projectMenuButtons.Clear();
        EnsureProjectMenuButtons();
    }

    private void EnsureProjectMenuButtons()
    {
        var width = Math.Min(420d, Math.Max(1, GraphicsDevice.Viewport.Width - 24d));
        var height = Math.Min(360d, Math.Max(1, GraphicsDevice.Viewport.Height - StatusBarHeight - 64d));
        var bounds = new ScreenRectangle(12, 54, width, height);
        if (bounds == projectMenuBounds && projectMenuButtons.Count > 0) return;
        pressedProjectMenuButton?.CancelPress();
        pressedProjectMenuButton = null;
        projectMenuBounds = bounds;
        projectMenuButtons.Clear();
        var rowHeight = (height - 76) / 5;
        for (var i = 0; i < 5; i++)
            projectMenuButtons.Add(new(new ScreenRectangle(bounds.X + 10, bounds.Y + 54 + i * rowHeight,
                width - 20, rowHeight - 6), ProjectMenuLabels[i]));
        projectMenuButtons.Add(new(new ScreenRectangle(bounds.X + width - 46, bounds.Y + 8, 36, 34), "メニューを閉じる"));
    }

    private void UpdateProjectMenu(KeyboardState keyboard, MouseState mouse)
    {
        if (projectMenuDrain)
        {
            if (mouse.LeftButton == ButtonState.Released && mouse.RightButton == ButtonState.Released &&
                mouse.MiddleButton == ButtonState.Released && keyboard.GetPressedKeys().Length == 0)
                projectMenuDrain = false;
            return;
        }
        if (!projectMenuOpen) return;
        EnsureProjectMenuButtons();
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        if (IsControlDown(keyboard) && IsPressed(keyboard, Keys.S))
        {
            ActivateProjectMenu(1);
            return;
        }
        for (var i = 0; i < projectMenuButtons.Count; i++)
        {
            projectMenuButtons[i].IsEnabled = IsProjectMenuEntryEnabled(i);
            projectMenuButtons[i].UpdatePointer(pointer);
        }
        // Outside clicks, Escape, and focus changes never dismiss this menu.
        if (IsPressed(keyboard, Keys.Tab) || IsPressed(keyboard, Keys.Down))
            projectMenuFocus = (projectMenuFocus + (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift) ? 5 : 1)) % 6;
        if (IsPressed(keyboard, Keys.Up)) projectMenuFocus = (projectMenuFocus + 5) % 6;
        if (IsPressed(keyboard, Keys.Enter) || IsPressed(keyboard, Keys.Space))
        {
            ActivateProjectMenu(projectMenuFocus);
            return;
        }
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            pressedProjectMenuButton = projectMenuButtons.FirstOrDefault(button => button.Press(pointer));
            if (pressedProjectMenuButton is not null) projectMenuFocus = projectMenuButtons.IndexOf(pressedProjectMenuButton);
        }
        if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
        {
            var pressed = pressedProjectMenuButton;
            pressedProjectMenuButton = null;
            if (pressed?.Release(pointer) == true) ActivateProjectMenu(projectMenuButtons.IndexOf(pressed));
        }
    }

    private void ActivateProjectMenu(int index)
    {
        if (!IsProjectMenuEntryEnabled(index)) return;
        pressedProjectMenuButton?.CancelPress();
        pressedProjectMenuButton = null;
        projectMenuDrain = true;
        if (index == 5) { projectMenuOpen = false; return; }
        try
        {
            switch (index)
            {
                case 0:
                case 4: RequestReturnToEvents(); break;
                case 1:
                    projectMenuStatus = SaveProject().Success ? "イベントプロジェクトを保存しました。" : "保存できませんでした。";
                    break;
                case 2: ExportPortable(); break;
                case 3: ImportPortable(); break;
            }
        }
        catch (Exception ex) { ShowInAppMessage("プロジェクト", ex.Message); }
    }

    private void DrawProjectMenu()
    {
        if (!projectMenuOpen) return;
        EnsureProjectMenuButtons();
        DrawRectangle(new ScreenRectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 165));
        DrawRectangle(projectMenuBounds, new Color(28, 34, 44));
        DrawOutline(projectMenuBounds, 1, new Color(130, 145, 165));
        textRenderer?.Draw("プロジェクト", ToRectangle(new ScreenRectangle(projectMenuBounds.X + 16, projectMenuBounds.Y + 10,
            projectMenuBounds.Width - 70, 32), 0), Color.White, 20, true);
        for (var i = 0; i < projectMenuButtons.Count; i++)
        {
            var index = i;
            var button = projectMenuButtons[i];
            button.IsEnabled = IsProjectMenuEntryEnabled(i);
            OperationButtonRenderer.Draw(button,
                (area, color) => DrawRectangle(area, ToButtonColor(color)),
                (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)),
                (area, color) =>
                {
                    var labelArea = index == 1 ? new ScreenRectangle(area.X, area.Y, Math.Max(1, area.Width - 90), area.Height) : area;
                    textRenderer?.Draw(index == 1 && IsCurrentProjectSaved ? "保存できています" : ProjectMenuLabels[index],
                        ToRectangle(labelArea, 8), ToButtonColor(color), 18, true);
                    if (index == 1)
                        textRenderer?.Draw("Ctrl+S", ToRectangle(new ScreenRectangle(area.X + area.Width - 90, area.Y, 90, area.Height), 8),
                            ToButtonColor(color), 16);
                });
            if (i == projectMenuFocus) DrawOutline(button.Bounds, 2, new Color(110, 180, 230));
            if (i is 1 or 3)
                DrawRectangle(new ScreenRectangle(button.Bounds.X, button.Bounds.Y + button.Bounds.Height + 2, button.Bounds.Width, 1), new Color(100, 110, 125));
        }
        var hovered = projectMenuButtons.FindIndex(button => button.IsPointerOver);
        DrawStatusBar(hovered >= 0 ? ProjectMenuDescription(hovered) :
            projectMenuStatus.Length > 0 ? projectMenuStatus : "右上の［×］でメニューを閉じます。外側クリックやEscでは閉じません。");
    }
}
