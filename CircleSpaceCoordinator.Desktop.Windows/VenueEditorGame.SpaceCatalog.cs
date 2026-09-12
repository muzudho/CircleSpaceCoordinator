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
                cachedNextType = definition is null ? null : SpaceTypeFactory.Create(definition);
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
            catalogOrientation = nextDeskOrientation;
            catalogPage = Math.Max(0, SpaceDefinitions.Current.Types.ToList().FindIndex(t => t.Id == catalogChoice)) / 6;
            spaceCatalogOpen = true;
            spaceCatalogDrain = true;
            BuildCatalogButtons();
        }
        catch (Exception ex) { ShowInAppMessage("スペースカタログ", ex.Message); }
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
        var cardHeight = (panel.Height - 168) / 2;
        var types = SpaceDefinitions.Current.Types;
        catalogPage = Math.Clamp(catalogPage, 0, Math.Max(0, (types.Count - 1) / 6));
        void Button(string label, ScreenRectangle area, Action run, bool enabled = true) =>
            catalogButtons.Add((new IconButtonModel(area, label) { IsEnabled = enabled }, run));
        foreach (var (type, index) in types.Skip(catalogPage * 6).Take(6).Select((t, i) => (t, i)))
        {
            Button(type.Name, new ScreenRectangle(panel.X + 12 + index % 3 * cardWidth, panel.Y + 62 + index / 3 * cardHeight,
                cardWidth - 6, cardHeight - 8), () => { catalogChoice = type.Id; BuildCatalogButtons(); });
            catalogButtons[^1].Model.IsSelected = type.Id == catalogChoice;
        }
        var right = panel.X + panel.Width * 0.67;
        var controlY = panel.Y + panel.Height - 148;
        Button("左回転", new ScreenRectangle(right, controlY, 104, 36), () => catalogOrientation = (QuarterTurn)(((int)catalogOrientation + 3) % 4));
        Button("右回転", new ScreenRectangle(right + 110, controlY, 104, 36), () => catalogOrientation = (QuarterTurn)(((int)catalogOrientation + 1) % 4));
        Button("前のページ", new ScreenRectangle(panel.X + 12, panel.Y + panel.Height - 92, 120, 34), () => { catalogPage--; BuildCatalogButtons(); }, catalogPage > 0);
        Button("次のページ", new ScreenRectangle(panel.X + 140, panel.Y + panel.Height - 92, 120, 34), () => { catalogPage++; BuildCatalogButtons(); }, (catalogPage + 1) * 6 < types.Count);
        Button("キャンセル", new ScreenRectangle(panel.X + panel.Width - 252, panel.Y + panel.Height - 48, 116, 36), () => CloseSpaceCatalog(false));
        Button("決定", new ScreenRectangle(panel.X + panel.Width - 128, panel.Y + panel.Height - 48, 116, 36), () => CloseSpaceCatalog(true), catalogChoice is not null);
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
        if (catalogWidth != GraphicsDevice.Viewport.Width || catalogHeight != GraphicsDevice.Viewport.Height) BuildCatalogButtons();
        if (IsPressed(keyboard, Keys.Escape)) { CloseSpaceCatalog(false); return true; }
        if (IsPressed(keyboard, Keys.Enter) && catalogChoice is not null) { CloseSpaceCatalog(true); return true; }
        if (IsPressed(keyboard, Keys.Q)) catalogOrientation = (QuarterTurn)(((int)catalogOrientation + 3) % 4);
        if (IsPressed(keyboard, Keys.E)) catalogOrientation = (QuarterTurn)(((int)catalogOrientation + 1) % 4);
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
        var bounds = NextSpaceBounds;
        DrawRectangle(bounds, new Color(24, 34, 44));
        DrawOutline(bounds, 2, new Color(110, 160, 170));
        textRenderer?.Draw("次に配置　▾", ToRectangle(new ScreenRectangle(bounds.X + 8, bounds.Y + 5, 204, 26)), Color.White, 18, true);
        if (NextSpace is not { } type)
        {
            textRenderer?.Draw("スペース定義で型を追加", ToRectangle(new ScreenRectangle(bounds.X + 8, bounds.Y + 60, 204, 40)), Color.LightGray, 16);
            return;
        }
        DrawSpaceTypePreview(type, nextDeskOrientation, new ScreenRectangle(bounds.X + 12, bounds.Y + 35, 90, 80));
        textRenderer?.Draw(type.Name, ToRectangle(new ScreenRectangle(bounds.X + 108, bounds.Y + 40, 104, 50)), Color.White, 15);
        textRenderer?.Draw(FormatOrientation(nextDeskOrientation), ToRectangle(new ScreenRectangle(bounds.X + 110, bounds.Y + 94, 100, 22)), Color.LightGray, 15);
        var button = NextDirectionButton;
        button.UpdatePointer(CanShowEditorHover ? new ScreenPoint(Mouse.GetState().X, Mouse.GetState().Y) : new ScreenPoint(-1, -1));
        StationeryButtonRenderer.Draw(button,
            (area, color) => DrawRectangle(area, ToButtonColor(color)),
            (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)),
            (area, color) =>
            {
                textRenderer?.Draw("向き", ToRectangle(area, 4), ToButtonColor(color), 16, true);
                DrawCircle(new ScreenPoint(area.X + area.Width - 6, area.Y + area.Height - 6), 3.5, ToButtonColor(color));
            });
        textRenderer?.Draw("クリックで型を選ぶ", ToRectangle(new ScreenRectangle(bounds.X + 8, bounds.Y + 174, 204, 22)), Color.LightGray, 14);
    }

    private void DrawSpaceCatalog()
    {
        if (!spaceCatalogOpen) return;
        if (catalogWidth != GraphicsDevice.Viewport.Width || catalogHeight != GraphicsDevice.Viewport.Height) BuildCatalogButtons();
        var panel = CatalogBounds;
        DrawRectangle(new ScreenRectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 170));
        DrawRectangle(panel, new Color(24, 32, 42));
        DrawOutline(panel, 2, new Color(110, 160, 170));
        textRenderer?.Draw("スペースカタログ ― 型と方向を選ぶ", ToRectangle(new ScreenRectangle(panel.X + 16, panel.Y + 12, panel.Width - 32, 32)), Color.White, 22, true);
        var types = SpaceDefinitions.Current.Types.Skip(catalogPage * 6).Take(6).ToArray();
        for (var index = 0; index < catalogButtons.Count; index++)
        {
            var button = catalogButtons[index].Model;
            StationeryButtonRenderer.Draw(button,
                (area, color) => DrawRectangle(area, ToButtonColor(color)),
                (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)),
                (area, color) =>
                {
                    if (index < types.Length)
                    {
                        DrawSpaceTypePreview(types[index], QuarterTurn.North, new ScreenRectangle(area.X + 10, area.Y + 10, area.Width - 20, area.Height - 50));
                        textRenderer?.Draw(types[index].Name, ToRectangle(new ScreenRectangle(area.X + 6, area.Y + area.Height - 36, area.Width - 12, 30)), ToButtonColor(color), 15);
                    }
                    else textRenderer?.Draw(button.AccessibleName, ToRectangle(area, 4), ToButtonColor(color), 16, true);
                });
        }
        if (SpaceDefinitions.Current.Types.FirstOrDefault(t => t.Id == catalogChoice) is { } chosen)
        {
            var x = panel.X + panel.Width * 0.68;
            DrawSpaceTypePreview(chosen, catalogOrientation, new ScreenRectangle(x, panel.Y + 90, panel.Width * 0.29, panel.Height - 290));
            textRenderer?.Draw($"{chosen.Name} ／ {FormatOrientation(catalogOrientation)}", ToRectangle(new ScreenRectangle(x, panel.Y + panel.Height - 204, panel.Width * 0.29, 42)), Color.White, 17);
        }
        DrawStatusBar("型を選び、左回転・右回転（Q / E）で方向を調整。決定 / Enterで反映、Escでキャンセル。Ctrl+Pで撮影。");
    }

    private void DrawSpaceTypePreview(SpaceTypeDefinition type, QuarterTurn orientation, ScreenRectangle area)
    {
        var cells = type.Cells.Select(c => (Cell: c, Position: new GridPosition(c.X, c.Y).Rotate(orientation))).ToArray();
        if (cells.Length == 0) return;
        var minX = cells.Min(c => c.Position.X);
        var minY = cells.Min(c => c.Position.Y);
        var columns = cells.Max(c => c.Position.X) - minX + 1;
        var rows = cells.Max(c => c.Position.Y) - minY + 1;
        var size = Math.Max(1, Math.Min((area.Width - 12) / columns, (area.Height - 12) / rows));
        var x = area.X + (area.Width - columns * size) / 2;
        var y = area.Y + (area.Height - rows * size) / 2;
        foreach (var item in cells)
        {
            var color = SpaceDefinitionDialog.AreaColor(item.Cell.Area);
            var rect = new ScreenRectangle(x + (item.Position.X - minX) * size, y + (item.Position.Y - minY) * size, size - 1, size - 1);
            DrawRectangle(rect, new Color(color.R, color.G, color.B));
            DrawOutline(rect, 1, Color.LightGray);
            textRenderer?.Draw(item.Cell.Area == 0 ? "—" : item.Cell.Area.ToString(), ToRectangle(rect, 2), Color.White, Math.Min(18, (int)size / 2), true);
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
