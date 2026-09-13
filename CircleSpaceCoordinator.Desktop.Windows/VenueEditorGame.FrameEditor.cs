namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private SpaceDefinitionDraft? frameDraft;
    private Action<SpaceDefinitionDraft>? frameSave;
    private readonly List<(IconButtonModel Button, Action Execute)> frameButtons = [];
    private (SpaceTarget Target, string Label)[] frameTargets = [];
    private IconButtonModel? pressedFrameButton;
    private bool framePainting;
    private int frameBrush = 1; // -1 erases; 0 occupies without a seat.
    private int frameX;
    private int frameY;
    private int frameFocus = -1; // Grid, or target list in request mode.
    private int frameTargetSelection;
    private int frameTargetScroll;
    private int frameLayoutWidth = -1;
    private int frameLayoutHeight = -1;
    private const int FrameTargetRows = 8;
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
    private double FrameCellSize => Math.Min(48, Math.Min(580d / frameDraft!.Width, 330d / frameDraft.Height));

    private static Color FrameAreaColor(int area) => area == 0 ? new Color(100, 107, 114)
        : new[] { Color.Teal, Color.SteelBlue, Color.DarkGoldenrod, Color.IndianRed, Color.MediumPurple,
            Color.OliveDrab, Color.DarkCyan, Color.DarkOrange, Color.DeepPink }[(area - 1) % 9];

    private void OpenFrameDefinitionEditor(SpaceDefinitionDraft draft, SpaceDefinitionCatalog catalog, Action<SpaceDefinitionDraft> save)
    {
        CancelInProgressPointerInteraction();
        frameDraft = draft;
        frameSave = save;
        frameBrush = 1;
        frameX = frameY = frameTargetSelection = frameTargetScroll = 0;
        frameFocus = -1;
        frameLayoutWidth = -1;
        frameTargets = catalog.Types.SelectMany(type => type.Cells.Where(cell => cell.Area > 0)
            .Select(cell => cell.Area).Distinct().Order().Select(area =>
                (new SpaceTarget(type.Id, area), $"{type.Name} ／ 区画{area}（{type.Cells.Count(cell => cell.Area == area)}セル）"))).ToArray();
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
        framePainting = false;
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
            Add("前の項目", 640, 550, 160, () => ScrollFrameTargets(-FrameTargetRows), frameTargetScroll > 0);
            Add("次の項目", 820, 550, 160, () => ScrollFrameTargets(FrameTargetRows), frameTargetScroll + FrameTargetRows < frameTargets.Length);
        }
        else
        {
            Add("種類：" + draft.Kind + " ▸", 700, 66, 280, () => draft.Kind = FrameKinds[(Array.IndexOf(FrameKinds, draft.Kind) + 1) % FrameKinds.Length]);
            Add("幅 −", 20, 114, 80, () => ResizeFrame(-1, 0), draft.Width > 1);
            Add("幅 ＋", 110, 114, 80, () => ResizeFrame(1, 0), draft.Width < 12);
            Add("高さ −", 240, 114, 90, () => ResizeFrame(0, -1), draft.Height > 1);
            Add("高さ ＋", 340, 114, 90, () => ResizeFrame(0, 1), draft.Height < 12);
            for (var brush = -1; brush <= 9; brush++)
            {
                var value = brush;
                Add(brush < 0 ? "消す" : brush == 0 ? "席なし" : brush.ToString(), 20 + (brush + 1) * 53, 170, 48,
                    () => frameBrush = value, selected: frameBrush == brush);
            }
            for (var edge = 0; edge < 4; edge++)
            {
                var index = edge;
                Add(FrameEdgeNames[edge] + "：" + draft.Edges[edge] + " ▸", 650, 230 + edge * 52, 330,
                    () => draft.Edges[index] = FrameEdgeValues[(Array.IndexOf(FrameEdgeValues, draft.Edges[index]) + 1) % FrameEdgeValues.Length]);
            }
        }
        Add("保存", 640, 610, 160, SaveFrameDefinitionEditor);
        Add("キャンセル", 820, 610, 160, CloseFrameDefinitionEditor);
    }

    private void ResizeFrame(int dx, int dy)
    {
        frameDraft!.Resize(frameDraft.Width + dx, frameDraft.Height + dy);
        frameX = Math.Min(frameX, frameDraft.Width - 1);
        frameY = Math.Min(frameY, frameDraft.Height - 1);
    }

    private void SelectFrameTarget(int index)
    {
        frameTargetSelection = Math.Clamp(index, 0, Math.Max(0, frameTargets.Length - 1));
        frameTargetScroll = Math.Clamp(frameTargetScroll, Math.Max(0, frameTargetSelection - FrameTargetRows + 1), frameTargetSelection);
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
        frameTargetScroll = Math.Clamp(frameTargetScroll + offset, 0, Math.Max(0, frameTargets.Length - FrameTargetRows));
        frameTargetSelection = Math.Clamp(frameTargetSelection, frameTargetScroll,
            Math.Max(frameTargetScroll, Math.Min(frameTargets.Length - 1, frameTargetScroll + FrameTargetRows - 1)));
        frameLayoutWidth = -1;
    }

    private void UpdateFrameDefinitionEditor(KeyboardState keyboard, MouseState mouse)
    {
        if (frameDraft is not { } draft) return;
        BuildFrameButtons();
        if (IsPressed(keyboard, Keys.Escape)) { CloseFrameDefinitionEditor(); return; }
        if (IsControlDown(keyboard) && IsPressed(keyboard, Keys.S)) { SaveFrameDefinitionEditor(); return; }
        if (IsPressed(keyboard, Keys.Tab))
            frameFocus = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift)
                ? frameFocus < 0 ? frameButtons.Count - 1 : frameFocus - 1
                : (frameFocus + 2) % (frameButtons.Count + 1) - 1;
        if (frameFocus < 0)
        {
            if (draft.IsRequest)
            {
                if (IsPressed(keyboard, Keys.Up)) SelectFrameTarget(frameTargetSelection - 1);
                if (IsPressed(keyboard, Keys.Down)) SelectFrameTarget(frameTargetSelection + 1);
                if (IsPressed(keyboard, Keys.PageUp)) SelectFrameTarget(frameTargetSelection - FrameTargetRows);
                if (IsPressed(keyboard, Keys.PageDown)) SelectFrameTarget(frameTargetSelection + FrameTargetRows);
                if (IsPressed(keyboard, Keys.Space) || IsPressed(keyboard, Keys.Enter)) ToggleFrameTarget(frameTargetSelection);
            }
            else
            {
                if (IsPressed(keyboard, Keys.Left)) frameX = Math.Max(0, frameX - 1);
                if (IsPressed(keyboard, Keys.Right)) frameX = Math.Min(draft.Width - 1, frameX + 1);
                if (IsPressed(keyboard, Keys.Up)) frameY = Math.Max(0, frameY - 1);
                if (IsPressed(keyboard, Keys.Down)) frameY = Math.Min(draft.Height - 1, frameY + 1);
                if (IsPressed(keyboard, Keys.Space) || IsPressed(keyboard, Keys.Enter)) draft.Paint(frameX, frameY, frameBrush < 0 ? null : frameBrush);
                if (IsPressed(keyboard, Keys.Delete) || IsPressed(keyboard, Keys.Back)) draft.Paint(frameX, frameY, null);
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
            ScrollFrameTargets(-Math.Sign(mouse.ScrollWheelValue - previousMouse.ScrollWheelValue) * 3);
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
            for (var row = 0; row < FrameTargetRows && frameTargetScroll + row < frameTargets.Length; row++)
                if (Contains(FrameBounds(20, 206 + row * 42, 960, 36), pointer))
                {
                    frameFocus = -1;
                    frameTargetSelection = frameTargetScroll + row;
                    ToggleFrameTarget(frameTargetSelection);
                    break;
                }
        }
        if (!draft.IsRequest)
        {
            var grid = FrameBounds(20, 222, FrameCellSize * draft.Width, FrameCellSize * draft.Height);
            if ((leftPress || rightPress) && Contains(grid, pointer)) { framePainting = true; frameFocus = -1; }
            if (framePainting && Contains(grid, pointer) && (mouse.LeftButton == ButtonState.Pressed || mouse.RightButton == ButtonState.Pressed))
            {
                frameX = Math.Min(draft.Width - 1, (int)((pointer.X - grid.X) / (FrameCellSize * FrameScale)));
                frameY = Math.Min(draft.Height - 1, (int)((pointer.Y - grid.Y) / (FrameCellSize * FrameScale)));
                draft.Paint(frameX, frameY, mouse.RightButton == ButtonState.Pressed || frameBrush < 0 ? null : frameBrush);
            }
            if (mouse.LeftButton == ButtonState.Released && mouse.RightButton == ButtonState.Released) framePainting = false;
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
            Text("割当可能な型・区画を選択してください。複数の型へ振替できます。", 20, 162, 960);
            for (var row = 0; row < FrameTargetRows && frameTargetScroll + row < frameTargets.Length; row++)
            {
                var index = frameTargetScroll + row;
                var item = frameTargets[index];
                var bounds = FrameBounds(20, 206 + row * 42, 960, 36);
                DrawRectangle(bounds, draft.Targets.Contains(item.Target) ? new Color(30, 115, 94) : new Color(35, 43, 54));
                if (frameFocus < 0 && index == frameTargetSelection) DrawOutline(bounds, 2, Color.Turquoise);
                Text((draft.Targets.Contains(item.Target) ? "☑ " : "☐ ") + item.Label, 28, 208 + row * 42, 944);
            }
            Text(frameTargets.Length == 0 ? "割当先がありません。先に配置物の型へ区画を定義してください。"
                : $"{draft.Targets.Count} 区画を選択 ／ 候補 {frameTargets.Length} 件　ホイール・PageUp/Downでスクロール", 20, 550, 600, 15);
        }
        else
        {
            Text($"{draft.Width} × {draft.Height} セル　塗る区画を下から選択", 460, 116, 520);
            Text("種類・各辺のボタンは押すたびに切替", 650, 190, 330, 16);
            for (var y = 0; y < draft.Height; y++)
            for (var x = 0; x < draft.Width; x++)
            {
                var area = draft.AreaAt(x, y);
                var bounds = FrameBounds(20 + x * FrameCellSize, 222 + y * FrameCellSize, FrameCellSize - 2, FrameCellSize - 2);
                DrawRectangle(bounds, area is null ? new Color(40, 45, 53) : FrameAreaColor(area.Value));
                DrawOutline(bounds, 1, Color.Gray);
                if (area is not null) textRenderer?.Draw(area == 0 ? "—" : area.ToString()!, ToRectangle(bounds, 2), Color.White, Math.Max(10, (int)(20 * FrameScale)), true);
                if (frameFocus < 0 && x == frameX && y == frameY) DrawOutline(bounds, 2, Color.White);
            }
            Text("同じ番号のセル ＝ 1サークル用の区画", 650, 448, 330, 16);
            Text("灰色（—）＝ 占有するが席ではない", 650, 478, 330, 16);
            Text("暗い空欄 ＝ 占有しない", 650, 508, 330, 16);
            Text("縮小時は保存で範囲外のセルを除去します。代表セル（区画1～9）が1つ以上必要です。", 20, 558, 960, 16);
        }
        Text("Tab：部品移動　矢印：セル／項目移動　Space：塗る／選択", 20, 600, 610, 15);
        Text("右クリック・Delete：消す　Ctrl+S：保存　Esc：キャンセル　Ctrl+P：撮影", 20, 626, 610, 14);
        for (var i = 0; i < frameButtons.Count; i++)
        {
            var button = frameButtons[i].Button;
            StationeryButtonRenderer.Draw(button,
                (area, color) => DrawRectangle(area, ToButtonColor(color)),
                (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)),
                (area, color) => textRenderer?.Draw(button.AccessibleName, ToRectangle(area, 3), ToButtonColor(color), Math.Max(10, (int)(17 * FrameScale)), true));
            if (frameFocus == i) DrawOutline(button.Bounds, 2, Color.White);
        }
    }
}
