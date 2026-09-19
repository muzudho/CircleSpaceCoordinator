namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Interaction;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private int genrePageTab;
    private int genrePreviewScroll;
    private string? selectedGenreKey;
    private bool genreSpaceSort;
    private bool genreCodeSort;
    private string[] genreCodeOrder = [];
    private string[] mappingGenreCodeOrder = [];

    private void SelectGenreTarget(string key, bool navigate = false)
    {
        if (mappingDraft is not { } draft) return;
        selectedGenreKey = key;
        var row = draft.Rows.ToList().FindIndex(style => style.Key == key);
        if (row < 0) return; // Unassigned participants have no editable genre code.
        mappingRow = row;
        mappingScroll = row / MappingVisibleRows * MappingVisibleRows;
        mappingFocus = -1;
        if (navigate) genrePageTab = 0;
        mappingWidth = -1;
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
        if (genrePageTab is 1 or 2)
        {
            foreach (var (label, spaceSort, codeSort) in new[] { ("サークルデータ", false, false), ("スペース数", true, false), ("ジャンルコード", false, true) })
            {
                var sort = spaceSort;
                var code = codeSort;
                var x = code ? 890 : sort ? 795 : 700;
                mappingEditorButtons.Add(new(new IconButtonModel(MappingBounds(x, 18, code ? 90 : 90, 36), label)
                    { IsSelected = code ? genreCodeSort : !genreCodeSort && genreSpaceSort == sort }, () =>
                {
                    if (code && genreCodeOrder.Length == 0)
                    {
                        OpenUnderlineInput("ジャンルコード順（カンマ区切り）", string.Join(",", mappingDraft?.Rows.Select(row => row.Key) ?? []), value =>
                        {
                            genreCodeOrder = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray();
                            mappingGenreCodeOrder = genreCodeOrder;
                            genreCodeSort = true;
                            genreSpaceSort = false;
                            genrePreviewScroll = 0;
                            mappingWidth = -1;
                        }, "ジャンルコードを表示したい順にカンマ区切りで入力してください。未入力のコードは末尾に並びます。", int.MaxValue);
                        return;
                    }
                    genreCodeSort = code;
                    genreSpaceSort = sort;
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
