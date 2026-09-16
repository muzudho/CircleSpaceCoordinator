namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private SpaceDefinitionDraft? frameDraft;
    private Action<SpaceDefinitionDraft>? frameSave;
    private readonly List<(IconButtonModel Button, Action Execute)> frameButtons = [];
    private (SpaceTarget Target, SpaceTypeDefinition Type)[] frameTargets = [];
    private IconButtonModel? pressedFrameButton;
    private bool frameConnectionMode;
    private GridPosition? frameConnectionStart;
    private int frameX;
    private int frameY;
    private int frameFocus = -1; // Grid, or target catalog in request mode.
    private int frameTargetSelection;
    private int frameTargetScroll;
    private int frameLayoutWidth = -1;
    private int frameLayoutHeight = -1;
    private const int FrameTargetPageSize = 6;
    private const int FrameTargetColumns = 3;
    private ScreenRectangle FrameTargetCard(int index) => FrameBounds(20 + index % FrameTargetColumns * 224, 206 + index / FrameTargetColumns * 164, 214, 156);
    private static readonly string[] FrameKinds = ["机", "場所", "ブース"];
    private static readonly string[] FrameEdgeValues = ["開放", "壁", "入口", "正面"];
    private static readonly string[] FrameEdgeNames = ["上辺", "右辺", "下辺", "左辺"];
    private double FrameScale => Math.Max(0.1, Math.Min(GraphicsDevice.Viewport.Width / 1000d, GraphicsDevice.Viewport.Height / 660d));
    private ScreenRectangle FrameBounds(double x, double y, double width, double height)
    {
        var scale = FrameScale;
        return new((GraphicsDevice.Viewport.Width - 1000 * scale) / 2 + x * scale,
            (GraphicsDevice.Viewport.Height - 660 * scale) / 2 + y * scale, width * scale, height * scale);
    }
    private double FrameCellSize => Math.Min(64, Math.Min(320d / frameDraft!.Width, 220d / frameDraft.Height));
    private ScreenRectangle FrameGridLayout => new(350 - FrameCellSize * frameDraft!.Width / 2,
        360 - FrameCellSize * frameDraft.Height / 2, FrameCellSize * frameDraft.Width, FrameCellSize * frameDraft.Height);

    private static readonly Color FrameCellColor = new(100, 107, 114);

    private void DrawFrameCellMarker(ScreenRectangle bounds)
    {
        var center = new ScreenPoint(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        var radius = Math.Min(12, Math.Min(bounds.Width, bounds.Height) * 0.26);
        DrawCircle(center, radius + 1, new Color(20, 25, 32));
        DrawCircle(center, radius, VacancyColor);
    }

    private void OpenFrameDefinitionEditor(SpaceDefinitionDraft draft, SpaceDefinitionCatalog catalog, Action<SpaceDefinitionDraft> save)
    {
        CancelInProgressPointerInteraction();
        frameDraft = draft;
        frameSave = save;
        frameConnectionMode = false;
        frameConnectionStart = null;
        frameX = frameY = frameTargetSelection = frameTargetScroll = 0;
        frameFocus = -1;
        frameLayoutWidth = -1;
        frameTargets = catalog.Types.Select(type => (new SpaceTarget(type.Id, 1), type)).ToArray();
        modalInputDrain = true;
    }

    private void CloseFrameDefinitionEditor()
    {
        CancelInProgressPointerInteraction();
        frameDraft = null;
        frameSave = null;
        frameButtons.Clear();
        frameTargets = [];
        RefreshCatalogAfterDefinitionEdit();
        spaceScroll = Math.Clamp(spaceSelected - SpaceRows + 1, 0, Math.Max(0, SpaceCount - SpaceRows));
        modalInputDrain = true;
    }

    private void SaveFrameDefinitionEditor()
    {
        if (frameDraft is null) return;
        try { frameSave!(frameDraft); CloseFrameDefinitionEditor(); }
        catch (Exception ex) { ShowInAppMessage("定義を確認してください", ex.Message); }
    }

    private void BuildFrameButtons()
    {
        if (frameDraft is not { } draft) return;
        if (frameLayoutWidth == GraphicsDevice.Viewport.Width && frameLayoutHeight == GraphicsDevice.Viewport.Height) return;
        frameLayoutWidth = GraphicsDevice.Viewport.Width;
        frameLayoutHeight = GraphicsDevice.Viewport.Height;
        pressedFrameButton = null;
        frameButtons.Clear();
        void Add(string label, double x, double y, double width, Action execute, bool enabled = true, bool selected = false) =>
            frameButtons.Add((new IconButtonModel(FrameBounds(x, y, width, 36), label) { IsEnabled = enabled, IsSelected = selected }, execute));
        void EditName() => OpenUnderlineInput(draft.IsRequest ? "読込み値" : "型の名前", draft.Name,
            value => { draft.Name = value; frameLayoutWidth = -1; }, "名前を入力し、［確定］を選んでください（100 文字まで）。");
        Add((draft.IsRequest ? "読込み値：" : "名前：") + draft.Name + " ✎", 20, 66, draft.IsRequest ? 960 : 660, EditName);
        if (draft.IsRequest)
        {
            Add("説明：" + draft.Description + " ✎", 20, 114, 960, () =>
                OpenUnderlineInput("申込スペースの説明", draft.Description, value => { draft.Description = value; frameLayoutWidth = -1; },
                    "説明を入力してください（200 文字まで）。", 200));
            Add("前のページ", 640, 550, 160, () => ScrollFrameTargets(-FrameTargetPageSize), frameTargetScroll > 0);
            Add("次のページ", 820, 550, 160, () => ScrollFrameTargets(FrameTargetPageSize), frameTargetScroll + FrameTargetPageSize < frameTargets.Length);
        }
        else
        {
            Add("種類：" + draft.Kind + " ▸", 700, 66, 280, () => draft.Kind = FrameKinds[(Array.IndexOf(FrameKinds, draft.Kind) + 1) % FrameKinds.Length]);
            Add("幅 −", 20, 114, 80, () => ResizeFrame(-1, 0), draft.Width > 1);
            Add("幅 ＋", 110, 114, 80, () => ResizeFrame(1, 0), draft.Width < 12);
            Add("高さ −", 240, 114, 90, () => ResizeFrame(0, -1), draft.Height > 1);
            Add("高さ ＋", 340, 114, 90, () => ResizeFrame(0, 1), draft.Height < 12);
            for (var edge = 0; edge < 4; edge++)
            {
                var index = edge;
                var grid = FrameGridLayout;
                var position = edge switch
                {
                    0 => new ScreenPoint(350 - 75, grid.Y - 48),
                    1 => new ScreenPoint(grid.X + grid.Width + 12, 360 - 18),
                    2 => new ScreenPoint(350 - 75, grid.Y + grid.Height + 12),
                    _ => new ScreenPoint(grid.X - 162, 360 - 18),
                };
                Add(FrameEdgeNames[edge] + "：" + draft.Edges[edge] + " ▸", position.X, position.Y, 150,
                    () => draft.SetEdge(index, FrameEdgeValues[(Array.IndexOf(FrameEdgeValues, draft.Edges[index]) + 1) % FrameEdgeValues.Length]));
            }
            Add("接続を編集", 710, 390, 270, () =>
            {
                frameConnectionMode = !frameConnectionMode;
                frameConnectionStart = null;
            }, selected: frameConnectionMode);
            Add("隣接セルの接続に戻す", 710, 438, 270, () =>
            {
                draft.ResetAdjacentConnections();
                frameConnectionStart = null;
            });
        }
        Add("保存", 640, 610, 160, SaveFrameDefinitionEditor);
        Add("キャンセル", 820, 610, 160, CloseFrameDefinitionEditor);
    }

    private void ResizeFrame(int dx, int dy)
    {
        frameConnectionStart = null;
        frameDraft!.Resize(frameDraft.Width + dx, frameDraft.Height + dy);
        frameX = Math.Min(frameX, frameDraft.Width - 1);
        frameY = Math.Min(frameY, frameDraft.Height - 1);
    }

    private void SelectFrameConnectionCell()
    {
        if (frameDraft!.AreaAt(frameX, frameY) is not > 0) return;
        var cell = new GridPosition(frameX, frameY);
        if (frameConnectionStart is not { } first) { frameConnectionStart = cell; return; }
        frameConnectionStart = null;
        if (first != cell) frameDraft.ToggleConnection(first, cell);
    }

    private void SelectFrameTarget(int index)
    {
        frameTargetSelection = Math.Clamp(index, 0, Math.Max(0, frameTargets.Length - 1));
        frameTargetScroll = frameTargetSelection / FrameTargetPageSize * FrameTargetPageSize;
        frameLayoutWidth = -1;
    }

    private void ToggleFrameTarget(int index)
    {
        if (index < 0 || index >= frameTargets.Length) return;
        var target = frameTargets[index].Target;
        if (!frameDraft!.Targets.Add(target)) frameDraft.Targets.Remove(target);
    }

    private void ScrollFrameTargets(int offset)
    {
        frameTargetScroll = Math.Clamp(frameTargetScroll + offset, 0, Math.Max(0, (frameTargets.Length - 1) / FrameTargetPageSize * FrameTargetPageSize));
        frameTargetSelection = Math.Clamp(frameTargetSelection, frameTargetScroll,
            Math.Max(frameTargetScroll, Math.Min(frameTargets.Length - 1, frameTargetScroll + FrameTargetPageSize - 1)));
        frameLayoutWidth = -1;
    }

    private void UpdateFrameDefinitionEditor(KeyboardState keyboard, MouseState mouse)
    {
        if (frameDraft is not { } draft) return;
        BuildFrameButtons();
        if (IsPressed(keyboard, Keys.Escape))
        {
            if (frameConnectionStart is not null) frameConnectionStart = null;
            else CloseFrameDefinitionEditor();
            return;
        }
        if (IsControlDown(keyboard) && IsPressed(keyboard, Keys.S)) { SaveFrameDefinitionEditor(); return; }
        if (IsPressed(keyboard, Keys.Tab))
            frameFocus = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift)
                ? frameFocus < 0 ? frameButtons.Count - 1 : frameFocus - 1
                : (frameFocus + 2) % (frameButtons.Count + 1) - 1;
        if (frameFocus < 0)
        {
            if (draft.IsRequest)
            {
                if (IsPressed(keyboard, Keys.Left)) SelectFrameTarget(frameTargetSelection - 1);
                if (IsPressed(keyboard, Keys.Right)) SelectFrameTarget(frameTargetSelection + 1);
                if (IsPressed(keyboard, Keys.Up)) SelectFrameTarget(frameTargetSelection - FrameTargetColumns);
                if (IsPressed(keyboard, Keys.Down)) SelectFrameTarget(frameTargetSelection + FrameTargetColumns);
                if (IsPressed(keyboard, Keys.PageUp)) SelectFrameTarget(frameTargetSelection - FrameTargetPageSize);
                if (IsPressed(keyboard, Keys.PageDown)) SelectFrameTarget(frameTargetSelection + FrameTargetPageSize);
                if (IsPressed(keyboard, Keys.Space) || IsPressed(keyboard, Keys.Enter)) ToggleFrameTarget(frameTargetSelection);
            }
            else
            {
                if (IsPressed(keyboard, Keys.Left)) frameX = Math.Max(0, frameX - 1);
                if (IsPressed(keyboard, Keys.Right)) frameX = Math.Min(draft.Width - 1, frameX + 1);
                if (IsPressed(keyboard, Keys.Up)) frameY = Math.Max(0, frameY - 1);
                if (IsPressed(keyboard, Keys.Down)) frameY = Math.Min(draft.Height - 1, frameY + 1);
                if (IsPressed(keyboard, Keys.Space) || IsPressed(keyboard, Keys.Enter))
                {
                    if (frameConnectionMode) SelectFrameConnectionCell();
                    else draft.ToggleCell(frameX, frameY);
                }
                if (IsPressed(keyboard, Keys.Delete) || IsPressed(keyboard, Keys.Back))
                {
                    if (frameConnectionMode) frameConnectionStart = null;
                    else draft.Paint(frameX, frameY, 0);
                }
            }
        }
        else if ((IsPressed(keyboard, Keys.Enter) || IsPressed(keyboard, Keys.Space)) && frameButtons[frameFocus].Button.IsEnabled)
        {
            frameButtons[frameFocus].Execute();
            frameLayoutWidth = -1;
            return;
        }
        if (draft.IsRequest && mouse.ScrollWheelValue != previousMouse.ScrollWheelValue)
        {
            ScrollFrameTargets(-Math.Sign(mouse.ScrollWheelValue - previousMouse.ScrollWheelValue) * FrameTargetPageSize);
        }
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        foreach (var item in frameButtons) item.Button.UpdatePointer(pointer);
        var leftPress = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
        var rightPress = mouse.RightButton == ButtonState.Pressed && previousMouse.RightButton == ButtonState.Released;
        if (leftPress)
        {
            pressedFrameButton = frameButtons.Select(item => item.Button).FirstOrDefault(button => button.Press(pointer));
            if (pressedFrameButton is not null) frameFocus = frameButtons.FindIndex(item => item.Button == pressedFrameButton);
        }
        if (draft.IsRequest && leftPress && pressedFrameButton is null)
        {
            for (var row = 0; row < FrameTargetPageSize && frameTargetScroll + row < frameTargets.Length; row++)
                if (Contains(FrameTargetCard(row), pointer))
                {
                    frameFocus = -1;
                    frameTargetSelection = frameTargetScroll + row;
                    ToggleFrameTarget(frameTargetSelection);
                    break;
                }
        }
        if (!draft.IsRequest)
        {
            var layout = FrameGridLayout;
            var grid = FrameBounds(layout.X, layout.Y, layout.Width, layout.Height);
            if (frameConnectionMode)
            {
                if (rightPress) frameConnectionStart = null;
                if (leftPress && Contains(grid, pointer))
                {
                    frameFocus = -1;
                    frameX = Math.Min(draft.Width - 1, (int)((pointer.X - grid.X) / (FrameCellSize * FrameScale)));
                    frameY = Math.Min(draft.Height - 1, (int)((pointer.Y - grid.Y) / (FrameCellSize * FrameScale)));
                    SelectFrameConnectionCell();
                }
            }
            else if (leftPress && Contains(grid, pointer))
            {
                frameFocus = -1;
                frameX = Math.Min(draft.Width - 1, (int)((pointer.X - grid.X) / (FrameCellSize * FrameScale)));
                frameY = Math.Min(draft.Height - 1, (int)((pointer.Y - grid.Y) / (FrameCellSize * FrameScale)));
                draft.ToggleCell(frameX, frameY);
            }
        }
        if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
        {
            var pressed = pressedFrameButton;
            pressedFrameButton = null;
            if (pressed?.Release(pointer) == true)
            {
                frameButtons.Single(item => item.Button == pressed).Execute();
                frameLayoutWidth = -1;
            }
        }
    }

    private void DrawFrameDefinitionEditor()
    {
        if (frameDraft is not { } draft) return;
        BuildFrameButtons();
        void Text(string text, double x, double y, double width, int size = 18, bool bold = false) =>
            textRenderer?.Draw(text, ToRectangle(FrameBounds(x, y, width, 30)), Color.White, Math.Max(10, (int)(size * FrameScale)), bold);
        Text(draft.IsRequest ? "申込スペースエディター（アプリ共通）" : "フレーム定義エディター（アプリ共通）", 20, 18, 960, 26, true);
        if (draft.IsRequest)
        {
            Text("割当可能なフレームを選択してください。複数のフレームへ振替できます。", 20, 162, 960);
            DrawFrameTargetCatalog();
        }
        else
        {
            Text($"{draft.Width} × {draft.Height} セル", 460, 116, 520);
            Text("セルをクリック：配置可能／配置未確定を切替", 20, 170, 620);
            Text("辺のボタンを押すたびに切替", 710, 210, 270, 16);
            Text("壁：オレンジの太線", 710, 246, 270, 16);
            Text("入口：緑の線・中央が開口", 710, 276, 270, 16);
            Text("正面：水色の線 ／ 開放：線なし", 710, 306, 270, 15);
            var layout = FrameGridLayout;
            for (var y = 0; y < draft.Height; y++)
            for (var x = 0; x < draft.Width; x++)
            {
                var area = draft.AreaAt(x, y);
                var bounds = FrameBounds(layout.X + x * FrameCellSize, layout.Y + y * FrameCellSize, FrameCellSize - 2, FrameCellSize - 2);
                DrawRectangle(bounds, FrameCellColor);
                DrawOutline(bounds, 1, Color.Gray);
                if (area > 0) DrawFrameCellMarker(bounds);
                if (frameFocus < 0 && x == frameX && y == frameY) DrawOutline(bounds, 2, OperationTargetColor);
            }
            ScreenPoint Centre(GridPosition cell)
            {
                var bounds = FrameBounds(layout.X + (cell.X + 0.5) * FrameCellSize, layout.Y + (cell.Y + 0.5) * FrameCellSize, 0, 0);
                return new(bounds.X, bounds.Y);
            }
            foreach (var link in frameConnectionMode ? draft.Connections : [])
            {
                var first = Centre(link.FirstCell);
                var second = Centre(link.SecondCell);
                DrawLine(first, second, 5 * FrameScale, new Color(20, 25, 32));
                DrawLine(first, second, 2 * FrameScale, Color.Gold);
                DrawCircle(first, 3 * FrameScale, Color.Gold);
                DrawCircle(second, 3 * FrameScale, Color.Gold);
            }
            DrawSpaceEdges(draft.Edges, QuarterTurn.North,
                FrameBounds(layout.X, layout.Y, layout.Width, layout.Height));
            if (frameConnectionStart is { } start) DrawCircle(Centre(start), 6 * FrameScale, Color.Turquoise);
            Text(frameConnectionMode ? "配置可能セルを2つ選択：接続を追加／削除。同じセル・右クリック：選択解除。"
                : "オレンジの丸＝配置可能セル ／ 丸なし＝配置未確定セル。縮小時は範囲外のセル・接続を保存時に除去。", 20, 558, 960, 16);
        }
        Text("Tab：部品移動　矢印：セル移動　Space：切替／接続端点を選択", 20, 600, 610, 15);
        Text(frameConnectionMode ? "右クリック・Delete：端点解除　Ctrl+S：保存　Esc：解除／終了"
            : "Delete：配置未確定にする　Ctrl+S：保存　Esc：キャンセル", 20, 626, 610, 14);
        for (var i = 0; i < frameButtons.Count; i++)
        {
            var button = frameButtons[i].Button;
            OperationButtonRenderer.Draw(button,
                (area, color) => DrawRectangle(area, ToButtonColor(color)),
                (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)),
                (area, color) => textRenderer?.Draw(button.AccessibleName, ToRectangle(area, 3), ToButtonColor(color), Math.Max(10, (int)(17 * FrameScale)), true));
            if (frameFocus == i && button.IsEnabled) DrawOutline(button.Bounds, 2, OperationTargetColor);
        }
    }
}
