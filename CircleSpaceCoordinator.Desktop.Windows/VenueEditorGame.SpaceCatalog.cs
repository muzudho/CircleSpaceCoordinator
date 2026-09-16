namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using StationeryUI.Canvas;
using StationeryUI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

public sealed partial class VenueEditorGame
{
    private string? nextSpaceId;
    private bool spaceCatalogOpen;
    private bool spaceCatalogDrain;
    private string? catalogChoice;
    private QuarterTurn catalogOrientation;
    private int catalogPage;
    private readonly List<(IconButtonModel Model, Action Run)> catalogButtons = [];
    private IconButtonModel? pressedCatalogButton;
    private bool pressedNextSpace;
    private bool pressedNextDirection;
    private IconButtonModel? nextSpaceSelectionButton;
    private IconButtonModel NextSpaceSelectionButton => nextSpaceSelectionButton ??= new(
        new ScreenRectangle(NextSpaceBounds.X + 12, NextSpaceBounds.Y + 124, 144, 44), "フレーム選択");
    private IconButtonModel? nextDirectionButton;
    private ScreenRectangle NextDirectionBounds => new(NextSpaceBounds.X + 164, NextSpaceBounds.Y + 124, 44, 44);
    private IconButtonModel NextDirectionButton => nextDirectionButton ??= new(NextDirectionBounds, "向き");
    private int catalogWidth;
    private int catalogHeight;

    private SpaceTypeDefinition? NextSpace => spaceDefinitions?.Current.Types.FirstOrDefault(t => t.Id == nextSpaceId)
        ?? spaceDefinitions?.Current.Types.FirstOrDefault();
    private SpaceTypeDefinition? cachedNextDefinition;
    private DeskType? cachedNextType;
    private DeskType? NextSpaceType
    {
        get
        {
            var definition = NextSpace;
            if (!ReferenceEquals(definition, cachedNextDefinition))
            {
                cachedNextDefinition = definition;
                cachedNextType = definition is null || !definition.Cells.Any(cell => cell.Area > 0) ? null : SpaceTypeFactory.Create(definition);
            }
            return cachedNextType;
        }
    }
    private ScreenRectangle NextSpaceBounds => new(12, ToolbarHeight + 12, 220, 200);

    private void OpenSpaceCatalog()
    {
        try
        {
            _ = SpaceDefinitions;
            CancelInProgressPointerInteraction();
            catalogChoice = NextSpace?.Id;
            spaceRequestsTab = false;
            spaceSelected = Math.Max(0, SpaceDefinitions.Current.Types.ToList().FindIndex(t => t.Id == catalogChoice));
            catalogOrientation = nextDeskOrientation;
            catalogPage = Math.Max(0, SpaceDefinitions.Current.Types.ToList().FindIndex(t => t.Id == catalogChoice)) / 6;
            spaceCatalogOpen = true;
            spaceCatalogDrain = true;
            BuildCatalogButtons();
        }
        catch (Exception ex) { ShowInAppMessage("フレームカタログ", ex.Message); }
    }

    private ScreenRectangle CatalogBounds => new(
        Math.Max(0, (GraphicsDevice.Viewport.Width - 950) / 2d),
        Math.Max(0, (GraphicsDevice.Viewport.Height - 560) / 2d),
        Math.Min(950, GraphicsDevice.Viewport.Width), Math.Min(560, GraphicsDevice.Viewport.Height));

    private void BuildCatalogButtons()
    {
        pressedCatalogButton?.CancelPress();
        pressedCatalogButton = null;
        catalogButtons.Clear();
        catalogWidth = GraphicsDevice.Viewport.Width;
        catalogHeight = GraphicsDevice.Viewport.Height;
        var panel = CatalogBounds;
        var cardWidth = (panel.Width * 0.65 - 32) / 3;
        var cardHeight = (panel.Height - 260) / 2;
        var types = SpaceDefinitions.Current.Types;
        catalogPage = Math.Clamp(catalogPage, 0, Math.Max(0, (SpaceCount - 1) / 6));
        void Button(string label, ScreenRectangle area, Action run, bool enabled = true) =>
            catalogButtons.Add((new IconButtonModel(area, label) { IsEnabled = enabled }, run));
        for (var index = 0; index < Math.Min(6, SpaceCount - catalogPage * 6); index++)
        {
            var selectedIndex = catalogPage * 6 + index;
            var label = spaceRequestsTab ? SpaceDefinitions.Current.Requests[selectedIndex].Value : types[selectedIndex].Name;
            Button(label, new ScreenRectangle(panel.X + 12 + index % 3 * cardWidth, panel.Y + 110 + index / 3 * cardHeight,
                cardWidth - 6, cardHeight - 8), () => {
                    spaceSelected = selectedIndex;
                    if (!spaceRequestsTab) catalogChoice = types[selectedIndex].Id;
                    BuildCatalogButtons();
                });
            catalogButtons[^1].Model.IsSelected = selectedIndex == spaceSelected;
        }
        Button("フレーム", new ScreenRectangle(panel.X + 12, panel.Y + 58, 132, 34), () => SelectCatalogTab(false));
        catalogButtons[^1].Model.IsSelected = !spaceRequestsTab;
        Button("申込スペース", new ScreenRectangle(panel.X + 152, panel.Y + 58, 150, 34), () => SelectCatalogTab(true));
        catalogButtons[^1].Model.IsSelected = spaceRequestsTab;
        var right = panel.X + panel.Width * 0.67;
        var controlY = panel.Y + panel.Height - 148;
        if (!spaceRequestsTab)
        {
            Button("左回転", new ScreenRectangle(right, controlY, 104, 36), () => catalogOrientation = (QuarterTurn)(((int)catalogOrientation + 3) % 4), SpaceCount > 0);
            Button("右回転", new ScreenRectangle(right + 110, controlY, 104, 36), () => catalogOrientation = (QuarterTurn)(((int)catalogOrientation + 1) % 4), SpaceCount > 0);
        }
        Button("追加", new ScreenRectangle(panel.X + 12, controlY, 80, 36), () => EditSpaceDefinition(true, false));
        Button("編集", new ScreenRectangle(panel.X + 100, controlY, 80, 36), () => EditSpaceDefinition(false, false), SpaceCount > 0);
        Button("複製", new ScreenRectangle(panel.X + 188, controlY, 80, 36), () => EditSpaceDefinition(true, true), SpaceCount > 0);
        Button("削除", new ScreenRectangle(panel.X + 276, controlY, 80, 36), DeleteSpaceDefinition, SpaceCount > 0);
        Button("前のページ", new ScreenRectangle(panel.X + 12, panel.Y + panel.Height - 92, 120, 34), () => { catalogPage--; BuildCatalogButtons(); }, catalogPage > 0);
        Button("次のページ", new ScreenRectangle(panel.X + 140, panel.Y + panel.Height - 92, 120, 34), () => { catalogPage++; BuildCatalogButtons(); }, (catalogPage + 1) * 6 < SpaceCount);
        Button("キャンセル", new ScreenRectangle(panel.X + panel.Width - 252, panel.Y + panel.Height - 48, 116, 36), () => CloseSpaceCatalog(false));
        Button("配置に決定", new ScreenRectangle(panel.X + panel.Width - 128, panel.Y + panel.Height - 48, 116, 36), () => CloseSpaceCatalog(true), !spaceRequestsTab && types.Any(t => t.Id == catalogChoice));
    }

    private void SelectCatalogTab(bool requests)
    {
        spaceRequestsTab = requests;
        spaceSelected = requests ? 0 : Math.Max(0, SpaceDefinitions.Current.Types.ToList().FindIndex(t => t.Id == catalogChoice));
        catalogPage = spaceSelected / 6;
        BuildCatalogButtons();
    }

    private void RefreshCatalogAfterDefinitionEdit()
    {
        if (!spaceCatalogOpen) return;
        spaceSelected = Math.Clamp(spaceSelected, 0, Math.Max(0, SpaceCount - 1));
        if (!spaceRequestsTab) catalogChoice = SpaceDefinitions.Current.Types.ElementAtOrDefault(spaceSelected)?.Id;
        catalogPage = spaceSelected / 6;
        BuildCatalogButtons();
        spaceCatalogDrain = true;
    }

    private void CloseSpaceCatalog(bool accept)
    {
        if (accept) { nextSpaceId = catalogChoice; nextDeskOrientation = catalogOrientation; }
        spaceCatalogOpen = false;
        spaceCatalogDrain = true;
        CancelInProgressPointerInteraction();
    }

    private bool UpdateSpaceCatalog(KeyboardState keyboard, MouseState mouse)
    {
        if (spaceCatalogDrain)
        {
            if (mouse.LeftButton == ButtonState.Released && mouse.RightButton == ButtonState.Released && mouse.MiddleButton == ButtonState.Released && keyboard.GetPressedKeys().Length == 0)
                spaceCatalogDrain = false;
            return true;
        }
        if (!spaceCatalogOpen) return false;
        if (UpdateModalDialog(keyboard, mouse)) return true;
        if (catalogWidth != GraphicsDevice.Viewport.Width || catalogHeight != GraphicsDevice.Viewport.Height) BuildCatalogButtons();
        if (IsPressed(keyboard, Keys.Escape)) { CloseSpaceCatalog(false); return true; }
        if (IsPressed(keyboard, Keys.Enter) && !spaceRequestsTab && SpaceDefinitions.Current.Types.Any(t => t.Id == catalogChoice)) { CloseSpaceCatalog(true); return true; }
        if (!spaceRequestsTab && IsPressed(keyboard, Keys.Q)) catalogOrientation = (QuarterTurn)(((int)catalogOrientation + 3) % 4);
        if (!spaceRequestsTab && IsPressed(keyboard, Keys.E)) catalogOrientation = (QuarterTurn)(((int)catalogOrientation + 1) % 4);
        if (mouse.ScrollWheelValue != previousMouse.ScrollWheelValue)
        {
            catalogPage += mouse.ScrollWheelValue < previousMouse.ScrollWheelValue ? 1 : -1;
            BuildCatalogButtons();
        }
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        foreach (var button in catalogButtons) button.Model.UpdatePointer(pointer);
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
            pressedCatalogButton = catalogButtons.Select(b => b.Model).FirstOrDefault(b => b.Press(pointer));
        if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
        {
            var pressed = pressedCatalogButton;
            pressedCatalogButton = null;
            if (pressed?.Release(pointer) == true) catalogButtons.Single(b => b.Model == pressed).Run();
        }
        return true;
    }

    private void DrawNextSpace()
    {
        if (editorMode != EditorMode.DeskPlacement) return;
        DrawSpaceCapacity();
        var bounds = NextSpaceBounds;
        DrawRectangle(bounds, new Color(24, 34, 44));
        DrawOutline(bounds, 2, new Color(110, 160, 170));
        textRenderer?.Draw("次に配置", ToRectangle(new ScreenRectangle(bounds.X + 8, bounds.Y + 5, 204, 26)), Color.White, 18, true);
        var selectionButton = NextSpaceSelectionButton;
        selectionButton.UpdatePointer(CanShowEditorHover ? new ScreenPoint(Mouse.GetState().X, Mouse.GetState().Y) : new ScreenPoint(-1, -1));
        OperationButtonRenderer.Draw(selectionButton,
            (area, color) => DrawRectangle(area, ToButtonColor(color)),
            (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)),
            (area, color) => textRenderer?.Draw(selectionButton.AccessibleName, ToRectangle(area, 4), ToButtonColor(color), 16, true));
        if (NextSpace is not { } type)
        {
            textRenderer?.Draw("［フレーム選択］から追加", ToRectangle(new ScreenRectangle(bounds.X + 8, bounds.Y + 60, 204, 40)), Color.LightGray, 16);
            return;
        }
        DrawSpaceTypePreview(type, nextDeskOrientation, new ScreenRectangle(bounds.X + 12, bounds.Y + 35, 90, 80));
        textRenderer?.Draw(type.Name, ToRectangle(new ScreenRectangle(bounds.X + 108, bounds.Y + 40, 104, 50)), Color.White, 15);
        textRenderer?.Draw(FormatOrientation(nextDeskOrientation), ToRectangle(new ScreenRectangle(bounds.X + 110, bounds.Y + 94, 100, 22)), Color.LightGray, 15);
        var button = NextDirectionButton;
        button.UpdatePointer(CanShowEditorHover ? new ScreenPoint(Mouse.GetState().X, Mouse.GetState().Y) : new ScreenPoint(-1, -1));
        OperationButtonRenderer.Draw(button,
            (area, color) => DrawRectangle(area, ToButtonColor(color)),
            (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)),
            (area, color) =>
            {
                textRenderer?.Draw("向き", ToRectangle(area, 4), ToButtonColor(color), 16, true);
                DrawCircle(new ScreenPoint(area.X + area.Width - 6, area.Y + area.Height - 6), 3.5, ToButtonColor(color));
            });
    }

    private void DrawSpaceCatalog()
    {
        if (!spaceCatalogOpen) return;
        if (catalogWidth != GraphicsDevice.Viewport.Width || catalogHeight != GraphicsDevice.Viewport.Height) BuildCatalogButtons();
        var panel = CatalogBounds;
        DrawRectangle(new ScreenRectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 170));
        DrawRectangle(panel, new Color(24, 32, 42));
        DrawOutline(panel, 2, new Color(110, 160, 170));
        textRenderer?.Draw("フレームカタログ（アプリ共通）", ToRectangle(new ScreenRectangle(panel.X + 16, panel.Y + 12, panel.Width - 32, 32)), Color.White, 22, true);
        var types = SpaceDefinitions.Current.Types.Skip(catalogPage * 6).Take(6).ToArray();
        for (var index = 0; index < catalogButtons.Count; index++)
        {
            var button = catalogButtons[index].Model;
            OperationButtonRenderer.Draw(button,
                (area, color) => DrawRectangle(area, ToButtonColor(color)),
                (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)),
                (area, color) =>
                {
                    if (!spaceRequestsTab && index < types.Length)
                    {
                        DrawSpaceTypePreview(types[index], QuarterTurn.North, new ScreenRectangle(area.X + 10, area.Y + 10, area.Width - 20, area.Height - 50));
                        textRenderer?.Draw(types[index].Name, ToRectangle(new ScreenRectangle(area.X + 6, area.Y + area.Height - 36, area.Width - 12, 30)), ToButtonColor(color), 15);
                    }
                    else textRenderer?.Draw(button.AccessibleName, ToRectangle(area, 4), ToButtonColor(color), 16, true);
                });
        }
        if (!spaceRequestsTab && SpaceDefinitions.Current.Types.FirstOrDefault(t => t.Id == catalogChoice) is { } chosen)
        {
            var x = panel.X + panel.Width * 0.68;
            DrawSpaceTypePreview(chosen, catalogOrientation, new ScreenRectangle(x, panel.Y + 110, panel.Width * 0.29, panel.Height - 320));
            textRenderer?.Draw($"{chosen.Name} ／ {FormatOrientation(catalogOrientation)}", ToRectangle(new ScreenRectangle(x, panel.Y + panel.Height - 204, panel.Width * 0.29, 42)), Color.White, 17);
        }
        if (spaceRequestsTab && SpaceDefinitions.Current.Requests.ElementAtOrDefault(spaceSelected) is { } request)
        {
            var summary = request.Value + "\n" + request.Description + "\n割当可能なフレーム\n" + string.Join("\n", request.Targets.Select(target =>
                SpaceDefinitions.Current.Types.FirstOrDefault(t => t.Id == target.TypeId)?.Name));
            textRenderer?.Draw(summary, ToRectangle(new ScreenRectangle(panel.X + panel.Width * 0.68, panel.Y + 110,
                panel.Width * 0.29, panel.Height - 270)), Color.White, 16);
        }
        if (SpaceCount == 0)
            textRenderer?.Draw("［追加］から定義を作成してください", ToRectangle(new ScreenRectangle(panel.X + 16, panel.Y + 130, panel.Width * 0.6, 40)), Color.LightGray, 17);
        textRenderer?.Draw("編集画面の［保存］は全イベント共通。カタログのキャンセルでは戻りません。",
            ToRectangle(new ScreenRectangle(panel.X + 12, panel.Y + panel.Height - 48, panel.Width - 280, 40)), Color.LightGray, 13);
        DrawStatusBar(spaceRequestsTab ? "申込スペースを追加・編集できます。フレームの配置は［フレーム］タブから。Escで閉じます。"
            : "追加・編集は［保存］で共通定義に反映。［配置に決定］/ Enterで次のフレームを選択。Q / Eで回転。");
    }

    private void DrawSpaceTypePreview(SpaceTypeDefinition type, QuarterTurn orientation, ScreenRectangle area)
    {
        var states = type.Cells.ToDictionary(cell => new GridPosition(cell.X, cell.Y), cell => cell.Area > 0);
        var cells = Enumerable.Range(0, type.Height).SelectMany(y => Enumerable.Range(0, type.Width)
            .Select(x => (Placeable: states.GetValueOrDefault(new GridPosition(x, y)), Position: new GridPosition(x, y).Rotate(orientation)))).ToArray();
        var minX = cells.Min(c => c.Position.X);
        var minY = cells.Min(c => c.Position.Y);
        var columns = cells.Max(c => c.Position.X) - minX + 1;
        var rows = cells.Max(c => c.Position.Y) - minY + 1;
        var size = Math.Max(1, Math.Min((area.Width - 12) / columns, (area.Height - 12) / rows));
        var x = area.X + (area.Width - columns * size) / 2;
        var y = area.Y + (area.Height - rows * size) / 2;
        foreach (var item in cells)
        {
            var rect = new ScreenRectangle(x + (item.Position.X - minX) * size, y + (item.Position.Y - minY) * size, size - 1, size - 1);
            DrawRectangle(rect, FrameCellColor);
            DrawOutline(rect, 1, Color.Gray);
            if (item.Placeable) DrawFrameCellMarker(rect);
        }
        for (var edge = 0; edge < 4; edge++)
        {
            var side = (edge + (int)orientation) % 4;
            var kind = type.Edges[edge];
            var color = kind == "壁" ? Color.Orange : kind == "入口" ? Color.LightGreen : Color.LightBlue;
            var start = side switch { 0 => new ScreenPoint(x, y), 1 => new ScreenPoint(x + columns * size, y), 2 => new ScreenPoint(x + columns * size, y + rows * size), _ => new ScreenPoint(x, y + rows * size) };
            var end = side switch { 0 => new ScreenPoint(x + columns * size, y), 1 => new ScreenPoint(x + columns * size, y + rows * size), 2 => new ScreenPoint(x, y + rows * size), _ => new ScreenPoint(x, y) };
            if (kind is "壁" or "正面") DrawLine(start, end, kind == "壁" ? 4 : 2, color);
            if (kind == "入口")
            {
                var a = new ScreenPoint(start.X + (end.X - start.X) * 0.35, start.Y + (end.Y - start.Y) * 0.35);
                var b = new ScreenPoint(start.X + (end.X - start.X) * 0.65, start.Y + (end.Y - start.Y) * 0.65);
                DrawLine(start, a, 3, color); DrawLine(b, end, 3, color);
            }
        }
    }

    private void DrawSpaceOnCanvas(SpaceTypeDetails space, GridPosition anchor, QuarterTurn orientation, bool ghost, bool disabled, Color? ghostColor = null)
    {
        var cells = space.Cells.Select(cell => anchor + new GridPosition(cell.X, cell.Y).Rotate(orientation)).ToArray();
        if (cells.Length == 0) return;
        var topLeft = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(new GridPosition(cells.Min(cell => cell.X), cells.Min(cell => cell.Y))));
        var bottomRight = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(new GridPosition(cells.Max(cell => cell.X) + 1, cells.Max(cell => cell.Y) + 1)));
        var bounds = new ScreenRectangle(topLeft.X + 2, topLeft.Y + 2,
            Math.Max(1, bottomRight.X - topLeft.X - 4), Math.Max(1, bottomRight.Y - topLeft.Y - 4));
        var outline = ghostColor ?? (disabled ? Color.Gray : new Color(92, 60, 23));
        var fill = disabled ? new Color(104, 108, 112) : new Color(198, 145, 54);
        DrawRectangle(bounds, ghost ? (ghostColor ?? Color.LightBlue) * 0.22f : fill * (space.Kind == "場所" ? 0.55f : 1f));
        DrawOutline(bounds, 2, outline);
        DrawDeskOrientationMarker(bounds, orientation, ghost ? ghostColor ?? Color.LightBlue : disabled ? Color.Gray : new Color(145, 98, 38));

        // Walls and entrances belong to the whole space, not to each underlying cell.
        for (var edge = 0; edge < 4; edge++)
        {
            var side = (edge + (int)orientation) % 4;
            var kind = space.Edges[edge];
            if (kind is not ("壁" or "入口")) continue;
            var start = side switch { 0 => new ScreenPoint(bounds.X, bounds.Y), 1 => new ScreenPoint(bounds.X + bounds.Width, bounds.Y), 2 => new ScreenPoint(bounds.X + bounds.Width, bounds.Y + bounds.Height), _ => new ScreenPoint(bounds.X, bounds.Y + bounds.Height) };
            var end = side switch { 0 => new ScreenPoint(bounds.X + bounds.Width, bounds.Y), 1 => new ScreenPoint(bounds.X + bounds.Width, bounds.Y + bounds.Height), 2 => new ScreenPoint(bounds.X, bounds.Y + bounds.Height), _ => new ScreenPoint(bounds.X, bounds.Y) };
            var edgeColor = ghostColor ?? (disabled ? Color.Gray : kind == "壁" ? Color.Orange : Color.LightGreen);
            if (kind == "入口")
            {
                DrawLine(start, new ScreenPoint(start.X + (end.X - start.X) * 0.25, start.Y + (end.Y - start.Y) * 0.25), 3, edgeColor);
                DrawLine(new ScreenPoint(start.X + (end.X - start.X) * 0.75, start.Y + (end.Y - start.Y) * 0.75), end, 3, edgeColor);
            }
            else DrawLine(start, end, 4, edgeColor);
        }
    }
}
