namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Interaction;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;
using StationeryUI.Text;
using global::StationeryUI.Windows;
using StationeryUI.MonoGame.Controls.ActionBadge;

public sealed partial class VenueEditorGame
{
    private int genrePageTab;
    private int genrePreviewScroll;
    private string? selectedGenreKey;
    private bool genreSpaceSort;
    private bool genreCodeSort;
    private bool genreOrdinalSort;
    private string[] genreCodeOrder = [];
    private string[] mappingGenreCodeOrder = [];
    private string? mappingGenreCodeOrderComment;
    private bool genreOrderDialogOpen;
    private int genreOrderDragIndex = -1;
    private int genreOrderPage;
    private const int GenreOrderPageSize = 16;
    private ScreenRectangle GenreOrderCommentBounds => new(80, 101, 700, 34);
    private UnderlineTextEditor? genreOrderCommentEditor;
    private bool genreOrderCommentEditing;
    private string genreOrderCommentComposition = "";

    private void SelectGenreTarget(string key, bool navigate = false)
    {
        if (mappingDraft is not { } draft) return;
        selectedGenreKey = key;
        selectedPackageGenreKey = null;
        var row = draft.Rows.ToList().FindIndex(style => style.Key == key);
        if (row < 0) return; // Unassigned participants have no editable genre code.
        mappingRow = row;
        mappingScroll = EnsureGenreRowVisible(mappingScroll, row, draft.Rows.Count);
        mappingFocus = -1;
        if (navigate) genrePageTab = 0;
        mappingWidth = -1;
    }

    private void OpenGenreCodeOrderEditor()
    {
        genreOrderDialogOpen = true;
        genreOrderDragIndex = -1;
        genreOrderPage = 0;
        SetMappingTextFocus(false);
        modalInputDrain = true;
    }

    private ScreenRectangle GenreOrderCard(int index)
    {
        var column = index / 8;
        var row = index % 8;
        return new(80 + column * 440, 160 + row * 52, 410, 46);
    }

    private void UpdateGenreOrderDialog(KeyboardState keyboard, MouseState mouse)
    {
        if (mappingDraft is not { } draft) return;
        if (genreOrderCommentEditing) { UpdateGenreOrderCommentInput(keyboard, mouse); return; }
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            if (Contains(new ScreenRectangle(860, 595, 120, 38), pointer)) { genreOrderDialogOpen = false; return; }
            if (Contains(new ScreenRectangle(580, 595, 120, 38), pointer) && genreOrderPage > 0) { genreOrderPage--; genreOrderDragIndex = -1; return; }
            if (Contains(new ScreenRectangle(710, 595, 120, 38), pointer) && genreOrderPage < Math.Max(0, (draft.Rows.Count - 1) / GenreOrderPageSize))
            {
                genreOrderPage = Math.Min(Math.Max(0, (draft.Rows.Count - 1) / GenreOrderPageSize), genreOrderPage + 1);
                genreOrderDragIndex = -1;
                return;
            }
            if (Contains(GenreOrderCommentBounds, pointer))
            {
                BeginGenreOrderCommentEdit();
                return;
            }
            var pageStart = genreOrderPage * GenreOrderPageSize;
            for (var local = 0; local < GenreOrderPageSize && pageStart + local < draft.Rows.Count; local++)
                if (Contains(GenreOrderCard(local), pointer)) { genreOrderDragIndex = pageStart + local; break; }
        }
        if (genreOrderDragIndex >= 0 && mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Pressed)
            for (var local = 0; local < GenreOrderPageSize; local++)
            {
                var index = genreOrderPage * GenreOrderPageSize + local;
                if (index >= draft.Rows.Count) break;
                if (index != genreOrderDragIndex && Contains(GenreOrderCard(local), pointer))
                {
                    draft.MoveRow(genreOrderDragIndex, index - genreOrderDragIndex);
                    genreOrderDragIndex = index;
                    mappingGenreCodeOrder = draft.Rows.Select(row => row.Key).ToArray();
                    genreCodeOrder = mappingGenreCodeOrder.ToArray();
                    genreCodeSort = true;
                    genreSpaceSort = false;
                    genreOrdinalSort = false;
                    mappingOrderChanged = true;
                    break;
                }
            }
        if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed) genreOrderDragIndex = -1;
        if (IsPressed(previousKeyboard, Keys.Escape)) { genreOrderDialogOpen = false; genreOrderDragIndex = -1; }
    }

    private void BeginGenreOrderCommentEdit()
    {
        genreOrderCommentEditor = new UnderlineTextEditor(mappingGenreCodeOrderComment ?? "", 1000);
        genreOrderCommentEditing = true;
        genreOrderCommentComposition = "";
        ResetUnderlineKeyRepeat();
        textInputService ??= new WindowsTextInputService(Window.Handle);
        textInputService.Start();
    }

    private void UpdateGenreOrderCommentInput(KeyboardState keyboard, MouseState mouse)
    {
        if (genreOrderCommentEditor is not { } editor || textInputService is null) return;
        foreach (var update in textInputService.DrainUpdates())
        {
            if (update.IsComposition) genreOrderCommentComposition = update.Text;
            else { editor.Insert(update.Text); genreOrderCommentComposition = ""; }
        }
        if (genreOrderCommentComposition.Length == 0)
        {
            if (IsPressed(keyboard, Keys.Enter)) { CommitGenreOrderCommentEdit(); return; }
            if (IsPressed(keyboard, Keys.Escape)) { CancelGenreOrderCommentEdit(); return; }
            var shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
            if (underlineLeftRepeat.Update(keyboard.IsKeyDown(Keys.Left), IsPressed(keyboard, Keys.Left), statusHintTime, true)) editor.Move(-1, shift);
            if (underlineRightRepeat.Update(keyboard.IsKeyDown(Keys.Right), IsPressed(keyboard, Keys.Right), statusHintTime, true)) editor.Move(1, shift);
            if (underlineBackRepeat.Update(keyboard.IsKeyDown(Keys.Back), IsPressed(keyboard, Keys.Back), statusHintTime, true)) editor.Delete(true);
            if (underlineDeleteRepeat.Update(keyboard.IsKeyDown(Keys.Delete), IsPressed(keyboard, Keys.Delete), statusHintTime, true)) editor.Delete(false);
        }
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released &&
            !Contains(GenreOrderCommentBounds, new ScreenPoint(mouse.X, mouse.Y))) CommitGenreOrderCommentEdit();
    }

    private void CommitGenreOrderCommentEdit()
    {
        if (genreOrderCommentEditor is not { } editor) return;
        var value = string.IsNullOrWhiteSpace(editor.Text) ? null : editor.Text.Trim();
        if (value != mappingGenreCodeOrderComment) { mappingGenreCodeOrderComment = value; mappingOrderChanged = true; }
        genreOrderCommentEditing = false;
        genreOrderCommentEditor = null;
        genreOrderCommentComposition = "";
        textInputService?.Stop();
        ResetUnderlineKeyRepeat();
    }

    private void CancelGenreOrderCommentEdit()
    {
        genreOrderCommentEditing = false;
        genreOrderCommentEditor = null;
        genreOrderCommentComposition = "";
        textInputService?.Stop();
        ResetUnderlineKeyRepeat();
    }

    private void DrawGenreOrderDialog()
    {
        if (mappingDraft is not { } draft) return;
        DrawRectangle(new ScreenRectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 200));
        var panel = new ScreenRectangle(40, 50, Math.Min(1000, GraphicsDevice.Viewport.Width - 80), Math.Min(610, GraphicsDevice.Viewport.Height - 100));
        DrawRectangle(panel, new Color(24, 29, 36));
        DrawOutline(panel, 2, Color.LightSlateGray);
        textRenderer?.Draw("ジャンルコードの並び順", ToRectangle(new(panel.X + 24, panel.Y + 18, panel.Width - 48, 34), 0), Color.White, 23, true);
        var commentBounds = GenreOrderCommentBounds;
        var comment = genreOrderCommentEditing ? genreOrderCommentEditor?.Text : mappingGenreCodeOrderComment;
        var commentDisplay = genreOrderCommentEditing && genreOrderCommentEditor is { } editingEditor && genreOrderCommentComposition.Length > 0
            ? editingEditor.Text.Insert(editingEditor.Caret, genreOrderCommentComposition) : comment;
        var commentText = string.IsNullOrWhiteSpace(commentDisplay) ? "並び順のコメント" : commentDisplay;
        textRenderer?.Draw(commentText, ToRectangle(commentBounds, 0), string.IsNullOrWhiteSpace(comment) ? new Color(150, 165, 170) : Color.White, 16, true);
        DrawLine(new(commentBounds.X, commentBounds.Y + commentBounds.Height - 3),
            new(commentBounds.X + commentBounds.Width, commentBounds.Y + commentBounds.Height - 3), genreOrderCommentEditing ? 3 : 2, new Color(99, 223, 185));
        var editBadge = ActionBadgeComponent.Create("EDIT", new Rectangle(0, 0, 100, 26));
        editBadge.Show();
        DrawMappingBadge(editBadge, new ScreenRectangle(commentBounds.X + commentBounds.Width - 100, commentBounds.Y, 100, commentBounds.Height));
        if (genreOrderCommentEditing && genreOrderCommentEditor is { } activeEditor)
        {
            var caretX = commentBounds.X + (textRenderer?.Measure(activeEditor.Text[..activeEditor.Caret], 16).X ?? 0);
            DrawRectangle(new ScreenRectangle(caretX, commentBounds.Y + 3, 2, commentBounds.Height - 8), new Color(147, 244, 200));
            textInputService?.SetInputArea(new ScreenRectangle(caretX, commentBounds.Y, Math.Max(1, commentBounds.Width - (caretX - commentBounds.X)), commentBounds.Height));
        }
        var pageStart = genreOrderPage * GenreOrderPageSize;
        for (var local = 0; local < GenreOrderPageSize && pageStart + local < draft.Rows.Count; local++)
        {
            var index = pageStart + local;
            var card = GenreOrderCard(local);
            DrawRectangle(card, index == genreOrderDragIndex ? new Color(45, 115, 112) : new Color(35, 43, 54));
            textRenderer?.Draw($"{index + 1,2}  {draft.Rows[index].Key}", ToRectangle(new(card.X + 8, card.Y + 3, card.Width - 16, 20), 0), Color.White, 17, true);
            var knowledge = draft.Rows[index].KnowledgeComment;
            if (!string.IsNullOrWhiteSpace(knowledge))
                textRenderer?.Draw(knowledge.Length > 42 ? knowledge[..42] + "…" : knowledge,
                    ToRectangle(new(card.X + 8, card.Y + 24, card.Width - 16, 18), 0), new Color(190, 205, 210), 12, true);
        }
        var lastPage = Math.Max(0, (draft.Rows.Count - 1) / GenreOrderPageSize);
        DrawRectangle(new ScreenRectangle(580, 595, 120, 38), genreOrderPage > 0 ? new Color(48, 70, 78) : new Color(40, 45, 50));
        textRenderer?.Draw("前へ", ToRectangle(new ScreenRectangle(580, 595, 120, 38), 5), genreOrderPage > 0 ? Color.White : new Color(110, 120, 125), 15, true);
        DrawRectangle(new ScreenRectangle(710, 595, 120, 38), genreOrderPage < lastPage ? new Color(48, 70, 78) : new Color(40, 45, 50));
        textRenderer?.Draw("次へ", ToRectangle(new ScreenRectangle(710, 595, 120, 38), 5), genreOrderPage < lastPage ? Color.White : new Color(110, 120, 125), 15, true);
        DrawRectangle(new ScreenRectangle(860, 595, 120, 38), new Color(48, 70, 78));
        textRenderer?.Draw("閉じる", ToRectangle(new ScreenRectangle(860, 595, 120, 38), 5), Color.White, 17, true);
        DrawStatusBar("ジャンル名のカードをドラッグして並び替えます。閉じると色網掛けの並び順へ反映します。", "ジャンルコード順");
    }
    private static readonly string[] GenrePageTabs = ["色網掛け", "色見本カタログ", "スペース数比率", "円グラフ"];
    private const int GenrePreviewPageSize = 6;

    private void OpenGenreMenu()
    {
        if (workspace is null) return;
        OpenGenreStyleEditor();
    }

    private void AddGenreTabs()
    {
        if (mappingKnowledgeComments && genrePageTab != 3)
        {
            foreach (var (label, spaceSort, codeSort) in new[] { ("スペース順", true, false), ("ジャンルコード順", false, true), ("文字コード順", false, false) })
            {
                var sort = spaceSort;
                var code = codeSort;
                var ordinal = !code && !sort;
                var x = code ? 700 : sort ? 795 : 862;
                mappingEditorButtons.Add(new(new IconButtonModel(MappingBounds(x, 18, code ? 90 : sort ? 62 : 66, 36), label)
                    { IsSelected = ordinal ? genreOrdinalSort : code ? genreCodeSort : genreSpaceSort }, () =>
                {
                    if (code && genreCodeOrder.Length == 0)
                    {
                        OpenUnderlineInput("ジャンルコード順（カンマ区切り）", string.Join(",", BuildGenrePreviewGroups().Select(group => group.GenreId)), value =>
                        {
                            genreCodeOrder = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray();
                            mappingGenreCodeOrder = genreCodeOrder;
                            mappingDraft?.ReorderRows(genreCodeOrder);
                            mappingOrderChanged = true;
                            genreCodeSort = true;
                            genreSpaceSort = false;
                            genreOrdinalSort = false;
                            genrePreviewScroll = 0;
                            mappingWidth = -1;
                        }, "ジャンルコードを表示したい順にカンマ区切りで入力してください。未入力のコードは末尾に並びます。", int.MaxValue);
                        return;
                    }
                    genreCodeSort = code;
                    genreSpaceSort = sort;
                    genreOrdinalSort = ordinal;
                    packageGenreScroll = 0;
                    if (mappingKnowledgeComments)
                        mappingDraft?.ReorderRows(BuildGenrePreviewGroups(chartOrder: sort).Select(group => group.GenreId));
                    if (code && genreCodeOrder.Length > 0)
                        mappingDraft?.ReorderRows(genreCodeOrder);
                    if (selectedGenreKey is { } selected)
                        SelectGenreTarget(selected);
                    genrePreviewScroll = 0;
                    mappingFocus = -1;
                    mappingWidth = -1;
                }, Tooltip: $"{label}でジャンルを並べ替えます。"));
            }
        }
        if (genrePageTab == 3)
            mappingEditorButtons.Add(new(new IconButtonModel(MappingBounds(740, 102, 240, 36), "画面いっぱいに表示"), () =>
            {
                if (mappingComposition.Length > 0) return;
                SetMappingTextFocus(false);
                genrePieExpanded = true;
                genrePieRestoreButton = null;
            }, Tooltip: "円グラフを画面いっぱいに表示します。元のサイズに戻すボタン、またはEscで戻ります。"));
        for (var index = 0; index < GenrePageTabs.Length; index++)
        {
            var tab = index;
            var button = new IconButtonModel(MappingBounds(105 + index * 145, 18, 140, 36), GenrePageTabs[index])
                { IsSelected = index == genrePageTab };
            mappingEditorButtons.Add(new(button, () =>
            {
                if (mappingComposition.Length > 0) return;
                SetMappingTextFocus(false);
                genrePageTab = tab;
                if (tab == 0 && selectedGenreKey is { } key) SelectGenreTarget(key);
                genrePreviewScroll = 0;
                mappingFocus = -1;
                mappingWidth = -1;
            }, Tooltip: $"{GenrePageTabs[index]}を表示します。編集中の内容は保持されます。"));
        }
    }

    private IReadOnlyList<GenreDataGroup> BuildGenrePreviewGroups(bool chartOrder = false)
    {
        var groups = BuildGenreDataGroups().ToList();
        foreach (var style in mappingDraft?.Build() ?? [])
            if (!groups.Any(group => group.GenreId == style.Key)) groups.Add(new(style.Key, 0, 0));
        if (chartOrder || genreSpaceSort)
            return groups.OrderByDescending(group => group.SpaceCount).ThenBy(group => group.GenreId, StringComparer.Ordinal).ToArray();
        if (genreOrdinalSort)
            return groups.OrderBy(group => group.GenreId, StringComparer.Ordinal).ToArray();
        if (genreCodeSort)
            return groups.OrderBy(group => Array.IndexOf(genreCodeOrder, group.GenreId) is var index && index >= 0 ? index : int.MaxValue)
                .ThenBy(group => group.GenreId, StringComparer.Ordinal).ToArray();
        var order = workspace?.Project.Participants
            .Select(item => string.IsNullOrWhiteSpace(item.GenreId) ? "（未設定）" : item.GenreId!)
            .Distinct(StringComparer.Ordinal).ToArray() ?? [];
        return groups.OrderBy(group => Array.IndexOf(order, group.GenreId) is var index && index >= 0 ? index : int.MaxValue)
            .ThenBy(group => group.GenreId, StringComparer.Ordinal).ToArray();
    }

    private void ScrollGenreOrMapping(int offset)
    {
        if (mappingKnowledgeComments && genrePageTab == 1) return;
        if (!GenreChartVisible) { ScrollMappingRows(offset); return; }
        genrePreviewScroll = Math.Clamp(genrePreviewScroll + Math.Sign(offset) * GenrePreviewPageSize,
            0, Math.Max(0, ((BuildGenrePreviewGroups().Count - 1) / GenrePreviewPageSize) * GenrePreviewPageSize));
        mappingWidth = -1;
    }

    private void GenrePreviewText(string text, double x, double y, double width, int size = 17)
        => textRenderer?.Draw(text, ToRectangle(MappingBounds(x, y, width, 28), 2), Color.White,
            Math.Max(10, (int)(size * MappingEditorScale)), true);

    private void DrawGenrePreview()
    {
        var groups = BuildGenrePreviewGroups(chartOrder: genrePageTab == 3);
        var total = groups.Sum(group => group.SpaceCount);
        if (genrePageTab == 1)
        {
            for (var index = 0; index < groups.Count; index++)
            {
                DrawGenreTile(GenreCatalogTile(index, groups.Count), groups[index].GenreId == "（未設定）" ? null : groups[index].GenreId, Color.Transparent, 0);
                if (groups[index].GenreId == selectedGenreKey)
                    DrawOutline(GenreCatalogTile(index, groups.Count), 3 * MappingEditorScale, OperationTargetColor);
            }
            return;
        }
        GenrePreviewText($"合計 {total} sp / {groups.Sum(group => group.CircleCount)} サークル", 20, 102, 700, 20);
        if (groups.Count == 0)
        {
            GenrePreviewText("ジャンルデータがありません。", 20, 150, 960);
            return;
        }
        if (genrePageTab == 2 && total > 0)
        {
            var cumulative = 0;
            foreach (var group in groups)
            {
                if (group.SpaceCount <= 0) continue;
                var start = 20 + 960d * cumulative / total;
                cumulative += group.SpaceCount;
                DrawGenreTile(MappingBounds(start, 150, 20 + 960d * cumulative / total - start, 76),
                    group.GenreId == "（未設定）" ? null : group.GenreId, Color.Transparent, 0);
            }
            GenrePreviewText("0%", 20, 228, 100, 14);
            GenrePreviewText("50%", 470, 228, 100, 14);
            GenrePreviewText("100%", 918, 228, 62, 14);
        }
        if (genrePageTab == 3 && total > 0)
        {
            DrawGenrePie(GenrePieArea, BuildGenrePreviewGroups(chartOrder: true));
        }
        if (genrePageTab != 1 && total == 0) GenrePreviewText("スペース数が 0 のため、グラフは表示されません。", 20, 142, 960);
        var visible = groups.Skip(genrePreviewScroll).Take(GenrePreviewPageSize).ToArray();
        for (var index = 0; index < visible.Length; index++)
        {
            var group = visible[index];
            var x = genrePageTab == 3 ? 440 : 20 + index % 2 * 480;
            var y = genrePageTab == 3 ? 146 + index * 48 : 286 + index / 2 * 50;
            DrawGenreTile(MappingBounds(x, y, 64, 36), group.GenreId == "（未設定）" ? null : group.GenreId, Color.Transparent, 0);
            var percent = total > 0 ? 100d * group.SpaceCount / total : 0;
            GenrePreviewText($"{group.GenreId}　{group.SpaceCount} sp ({percent:0.0}%)", x + 74, y, 396, 15);
        }
        GenrePreviewText($"{genrePreviewScroll + 1}–{genrePreviewScroll + visible.Length} / {groups.Count} ジャンル", 380, 462, 600, 14);
    }

    private string GenrePreviewTooltip(ScreenPoint pointer)
    {
        var groups = BuildGenrePreviewGroups(chartOrder: genrePageTab == 3);
        var total = groups.Sum(group => group.SpaceCount);
        if (genrePageTab == 1)
        {
            for (var index = 0; index < groups.Count; index++)
                if (Contains(GenreCatalogTile(index, groups.Count), pointer))
                    return $"{groups[index].GenreId}：{groups[index].SpaceCount} sp / {groups[index].CircleCount} サークル　" +
                        (mappingDraft?.Rows.Any(style => style.Key == groups[index].GenreId) == true
                            ? "クリックで色網掛けの該当行へ移動します。" : "ジャンル未設定のため、対応する編集行はありません。");
            return groups.Count == 0 ? "ジャンルデータがありません。" : "全ジャンルの色見本です。マウスを合わせるとジャンル名を確認できます。";
        }
        double? fraction = null;
        var bar = MappingBounds(20, 150, 960, 76);
        if (genrePageTab == 2 && Contains(bar, pointer)) fraction = (pointer.X - bar.X) / bar.Width;
        var pie = GenrePieArea;
        var dx = pointer.X - pie.X - pie.Width / 2;
        var dy = pointer.Y - pie.Y - pie.Height / 2;
        if (genrePageTab == 3 && dx * dx + dy * dy <= Math.Pow(Math.Min(pie.Width, pie.Height) / 2, 2))
        {
            var angle = Math.Atan2(dy, dx) + Math.PI / 2;
            fraction = (angle < 0 ? angle + Math.PI * 2 : angle) / (Math.PI * 2);
        }
        if (fraction is { } value && total > 0)
        {
            var group = FindGenreSlice(groups.Where(group => group.SpaceCount > 0).ToArray(), value * total);
            return $"{group.GenreId}：{group.SpaceCount} sp ({100d * group.SpaceCount / total:0.0}%) / {group.CircleCount} サークル";
        }
        return "必要スペース数で集計します。前・次のページ、マウスホイールで全ジャンルを確認できます。グラフは全ページの合計です。";
    }

    private ScreenRectangle GenreCatalogTile(int index, int count)
    {
        var area = MappingBounds(20, 104, 960, 384);
        var columns = Math.Min(count, Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count * area.Width / area.Height))));
        var rows = (count + columns - 1) / columns;
        var width = area.Width / columns;
        var height = area.Height / rows;
        var gap = Math.Min(4 * MappingEditorScale, Math.Min(width, height) * .08);
        return new(area.X + index % columns * width, area.Y + index / columns * height, width - gap, height - gap);
    }

    private bool genrePieExpanded;
    private IconButtonModel? genrePieRestoreButton;
    private ScreenRectangle GenrePieArea => genrePieExpanded
        ? new(12, WorkerBarHeight + 54, Math.Max(1, GraphicsDevice.Viewport.Width - 24),
            Math.Max(1, GraphicsDevice.Viewport.Height - WorkerBarHeight - StatusBarHeight - 66))
        : MappingBounds(20, 146, 380, 290);

    private IconButtonModel GenrePieRestoreButton()
    {
        var bounds = new ScreenRectangle(Math.Max(12, GraphicsDevice.Viewport.Width - 232), WorkerBarHeight + 8, 220, 36);
        if (genrePieRestoreButton?.Bounds != bounds)
            genrePieRestoreButton = new(bounds, "元のサイズに戻す");
        return genrePieRestoreButton;
    }

    private void UpdateExpandedGenrePie(KeyboardState keyboard, MouseState mouse)
    {
        var button = GenrePieRestoreButton();
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        button.UpdatePointer(pointer);
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released) button.Press(pointer);
        if (IsPressed(keyboard, Keys.Escape) || IsPressed(keyboard, Keys.Enter) ||
            mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed && button.Release(pointer))
        {
            genrePieExpanded = false;
            genrePieRestoreButton = null;
            mappingWidth = -1;
            mappingFocus = -1;
            modalInputDrain = true;
        }
    }

    private void DrawExpandedGenrePie()
    {
        var groups = BuildGenrePreviewGroups(chartOrder: true);
        if (groups.Sum(group => group.SpaceCount) > 0) DrawGenrePie(GenrePieArea, groups);
        else textRenderer?.Draw("スペース数が 0 のため、グラフは表示されません。", ToRectangle(GenrePieArea, 8), Color.White, 20);
        var button = GenrePieRestoreButton();
        OperationButtonRenderer.Draw(button,
            (area, color) => DrawRectangle(area, ToButtonColor(color)),
            (area, thickness, color) => DrawOutline(area, thickness, ToButtonColor(color)),
            (area, color) => textRenderer?.Draw(button.AccessibleName, ToRectangle(area, 5), ToButtonColor(color), 17, true));
        var mouse = Mouse.GetState();
        DrawStatusBar(button.IsPointerOver ? "元のサイズに戻します。Escでも戻れます。" : GenrePreviewTooltip(new(mouse.X, mouse.Y)), "円グラフ：Escで元のサイズに戻る");
    }

    private void DrawGenrePie(ScreenRectangle area, IReadOnlyList<GenreDataGroup> groups)
    {
        var total = groups.Sum(group => group.SpaceCount);
        if (total <= 0) return;
        var center = new ScreenPoint(area.X + area.Width / 2, area.Y + area.Height / 2);
        var radius = Math.Min(area.Width, area.Height) / 2;
        var positive = groups.Where(group => group.SpaceCount > 0).ToArray();
        var segments = Math.Max(720, (int)Math.Ceiling(radius * Math.PI * 2));
        for (var segment = 0; segment < segments; segment++)
        {
            var slice = FindGenreSlice(positive, (segment + .5) / segments * total);
            var angle = -Math.PI / 2 + segment * Math.PI * 2 / segments;
            DrawLine(center, new(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius), 2.5,
                GetGenreStyle(slice.GenreId == "（未設定）" ? null : slice.GenreId).Primary);
        }
        DrawPiePatterns(center, radius, positive.Select(group => (group.GenreId, group.SpaceCount)).ToArray(), total);
        DrawCircle(center, radius, Color.LightGray);
    }
}
