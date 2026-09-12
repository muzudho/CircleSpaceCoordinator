namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Persistence;
using StationeryUI.Canvas;
using StationeryUI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

public sealed partial class VenueEditorGame
{
    private SpaceDefinitionStore? spaceDefinitions;
    private bool spaceRequestsTab;
    private int spaceSelected;
    private int spaceScroll;
    private readonly List<(IconButtonModel Button, Action Execute)> spaceButtons = [];
    private IconButtonModel? pressedSpaceButton;
    private string spaceDefinitionStatus = "型と申込スペースは全イベント・全配置案で共通です。";

    private SpaceDefinitionStore SpaceDefinitions => spaceDefinitions ??= new SpaceDefinitionStore(
        Path.Combine(AppContext.BaseDirectory, "space-definitions.json"));

    private int SpaceRows => Math.Max(1, (GraphicsDevice.Viewport.Height - StatusBarHeight - 226) / 42);
    private int SpaceCount => spaceRequestsTab ? SpaceDefinitions.Current.Requests.Count : SpaceDefinitions.Current.Types.Count;
    private ScreenRectangle SpaceRow(int row) => new(12, 224 + row * 42, 330, 38);

    private void OpenSpaceDefinitions()
    {
        try
        {
            _ = SpaceDefinitions;
            editorMode = EditorMode.SpaceDefinitions;
            CancelInProgressPointerInteraction();
            CreateToolbar();
            BuildSpaceButtons();
        }
        catch (Exception ex) { ShowInAppMessage("スペース定義を開けません", ex.Message); }
    }

    private void BuildSpaceButtons()
    {
        spaceButtons.Clear();
        pressedSpaceButton = null;
        void Add(string label, double x, double y, double width, Action action) =>
            spaceButtons.Add((new IconButtonModel(new ScreenRectangle(x, y, width, 34), label), action));
        Add("配置物の型", 12, 122, 155, () => SelectSpaceTab(false));
        Add("申込スペース", 177, 122, 165, () => SelectSpaceTab(true));
        Add("追加", 12, 174, 70, () => EditSpaceDefinition(true, false));
        Add("編集", 90, 174, 70, () => EditSpaceDefinition(false, false));
        Add("複製", 168, 174, 70, () => EditSpaceDefinition(true, true));
        Add("削除", 246, 174, 96, DeleteSpaceDefinition);
    }

    private void SelectSpaceTab(bool requests)
    {
        spaceRequestsTab = requests;
        spaceSelected = spaceScroll = 0;
    }

    private void SaveSpaceDefinitions(SpaceDefinitionCatalog catalog)
    {
        SpaceDefinitions.Save(catalog);
        spaceDefinitionStatus = "アプリ共通のスペース定義を保存しました。";
        spaceSelected = Math.Clamp(spaceSelected, 0, Math.Max(0, SpaceCount - 1));
    }

    private sealed class SpaceDialogOwner : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle => System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
    }

    private void EditSpaceDefinition(bool create, bool duplicate)
    {
        if ((!create || duplicate) && SpaceCount == 0) return;
        CancelInProgressPointerInteraction();
        var catalog = SpaceDefinitions.Current;
        if (spaceRequestsTab)
        {
            var source = create && !duplicate ? new SpaceRequestDefinition(Guid.NewGuid().ToString("N"), "", "", []) : catalog.Requests[spaceSelected];
            if (duplicate) source = source with { Id = Guid.NewGuid().ToString("N"), Value = source.Value + "-コピー" };
            SpaceDefinitionDialog.EditRequest(new SpaceDialogOwner(), source, catalog, updated =>
            {
                SaveSpaceDefinitions(catalog with { Requests = create ? [.. catalog.Requests, updated] : catalog.Requests.Select(r => r.Id == updated.Id ? updated : r).ToArray() });
                if (create) spaceSelected = SpaceCount - 1;
            });
        }
        else
        {
            var source = create && !duplicate ? new SpaceTypeDefinition(Guid.NewGuid().ToString("N"), "新しい型", "机", 3, 3,
                [new(0, 0, 1)], ["開放", "開放", "開放", "開放"]) : catalog.Types[spaceSelected];
            if (duplicate) source = source with { Id = Guid.NewGuid().ToString("N"), Name = source.Name + " コピー" };
            SpaceDefinitionDialog.EditType(new SpaceDialogOwner(), source, updated =>
            {
                SaveSpaceDefinitions(catalog with { Types = create ? [.. catalog.Types, updated] : catalog.Types.Select(t => t.Id == updated.Id ? updated : t).ToArray() });
                if (create) spaceSelected = SpaceCount - 1;
            });
        }
        spaceScroll = Math.Clamp(spaceSelected - SpaceRows + 1, 0, Math.Max(0, SpaceCount - SpaceRows));
        modalInputDrain = true;
    }

    private void DeleteSpaceDefinition()
    {
        if (SpaceCount == 0) return;
        var catalog = SpaceDefinitions.Current;
        var index = spaceSelected;
        var name = spaceRequestsTab ? catalog.Requests[index].Value : catalog.Types[index].Name;
        var requests = spaceRequestsTab;
        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "スペース定義を削除", $"「{name}」をアプリ共通の定義から削除します。"), action =>
        {
            if (action != ModalDialogAction.Accept) return;
            try
            {
                SaveSpaceDefinitions(requests ? catalog with { Requests = catalog.Requests.Where((_, i) => i != index).ToArray() }
                    : catalog with { Types = catalog.Types.Where((_, i) => i != index).ToArray() });
            }
            catch (Exception ex) { ShowInAppMessage("削除できません", ex.Message); }
        });
    }

    private void UpdateSpaceDefinitions(MouseState mouse, ScreenPoint pointer)
    {
        if (spaceButtons.Count == 0) BuildSpaceButtons();
        for (var i = 0; i < spaceButtons.Count; i++)
        {
            var button = spaceButtons[i].Button;
            button.IsEnabled = i is 0 or 1 or 2 || SpaceCount > 0;
            button.IsSelected = i == (spaceRequestsTab ? 1 : 0);
            button.UpdatePointer(pointer);
        }
        if (mouse.ScrollWheelValue != previousMouse.ScrollWheelValue)
            spaceScroll = Math.Clamp(spaceScroll + (mouse.ScrollWheelValue < previousMouse.ScrollWheelValue ? 1 : -1), 0, Math.Max(0, SpaceCount - SpaceRows));
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            pressedToolbarButton = toolbarButtons.FirstOrDefault(b => b.Model.Press(pointer));
            if (pressedToolbarButton is null)
                pressedSpaceButton = spaceButtons.Select(b => b.Button).FirstOrDefault(b => b.Press(pointer));
            for (var row = 0; row < SpaceRows && row + spaceScroll < SpaceCount; row++)
                if (Contains(SpaceRow(row), pointer)) spaceSelected = row + spaceScroll;
        }
        if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
        {
            var toolbar = pressedToolbarButton;
            var pressed = pressedSpaceButton;
            pressedToolbarButton = null;
            pressedSpaceButton = null;
            if (toolbar?.Model.Release(pointer) == true) ExecuteToolbarAction(toolbar.Action, pointer);
            else if (pressed?.Release(pointer) == true) spaceButtons.Single(b => b.Button == pressed).Execute();
        }
    }

    private void DrawSpaceDefinitions()
    {
        if (spaceButtons.Count == 0) BuildSpaceButtons();
        foreach (var item in spaceButtons)
            StationeryButtonRenderer.Draw(item.Button,
                (area, color) => DrawRectangle(area, ToButtonColor(color)),
                (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)),
                (area, color) => textRenderer?.Draw(item.Button.AccessibleName, ToRectangle(area, 4), ToButtonColor(color), 17, true));
        var catalog = SpaceDefinitions.Current;
        spaceScroll = Math.Clamp(spaceScroll, 0, Math.Max(0, SpaceCount - SpaceRows));
        for (var row = 0; row < SpaceRows && row + spaceScroll < SpaceCount; row++)
        {
            var index = row + spaceScroll;
            var bounds = SpaceRow(row);
            DrawRectangle(bounds, index == spaceSelected ? new Color(35, 126, 111) : new Color(35, 43, 54));
            textRenderer?.Draw(spaceRequestsTab ? catalog.Requests[index].Value : catalog.Types[index].Name, ToRectangle(bounds, 6), Color.White, 17);
        }
        void Text(string value, int y, int size = 18) => textRenderer?.Draw(value,
            new Rectangle(370, y, Math.Max(1, GraphicsDevice.Viewport.Width - 390), 30), Color.White, size);
        Text("アプリ共通のスペース定義", 124, 23);
        Text("すべてのイベント・スペース配置・サークル配置案で同じ定義を使います", 162, 16);
        if (SpaceCount == 0) { Text("［追加］から定義を作成してください", 222); return; }
        if (spaceRequestsTab)
        {
            var request = catalog.Requests[spaceSelected];
            Text($"申込スペース：{request.Value}", 218, 22);
            Text(request.Description, 252);
            Text("割当可能な区画", 299);
            var targets = request.Targets.Select(t => $"{catalog.Types.Single(type => type.Id == t.TypeId).Name} ／ 区画{t.Area}");
            var y = 334;
            foreach (var target in targets.Take(Math.Max(1, (GraphicsDevice.Viewport.Height - StatusBarHeight - y) / 32))) { Text(target, y, 16); y += 32; }
            return;
        }
        var type = catalog.Types[spaceSelected];
        Text($"{type.Name}　({type.Kind}・{type.Width}×{type.Height})", 218, 21);
        Text("同じ番号＝1区画 ／ 灰色＝占有するが席ではない", 255, 16);
        Text($"上：{type.Edges[0]}　右：{type.Edges[1]}　下：{type.Edges[2]}　左：{type.Edges[3]}", 288, 16);
        var cellSize = Math.Max(1, Math.Min(64, Math.Min((GraphicsDevice.Viewport.Width - 410) / type.Width,
            (GraphicsDevice.Viewport.Height - StatusBarHeight - 350) / type.Height)));
        foreach (var cell in type.Cells)
        {
            var bounds = new ScreenRectangle(385 + cell.X * cellSize, 338 + cell.Y * cellSize, cellSize - 2, cellSize - 2);
            var fill = SpaceDefinitionDialog.AreaColor(cell.Area);
            DrawRectangle(bounds, new Color(fill.R, fill.G, fill.B));
            DrawOutline(bounds, 1, Color.LightGray);
            textRenderer?.Draw(cell.Area == 0 ? "—" : cell.Area.ToString(), ToRectangle(bounds, 3), Color.White, 20, true);
        }
    }
}
