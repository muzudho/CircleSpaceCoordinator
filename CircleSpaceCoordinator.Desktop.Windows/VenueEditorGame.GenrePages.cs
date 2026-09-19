namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Interaction;
using Microsoft.Xna.Framework;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private int genrePageTab;
    private int genrePreviewScroll;
    private static readonly string[] GenrePageTabs = ["ジャンルコードと色網掛け", "カタログ", "スペース数比率", "円グラフ"];
    private int GenrePreviewPageSize => genrePageTab == 1 ? 18 : 6;

    private void OpenGenreMenu()
    {
        if (workspace is null) return;
        OpenProjectMenu(genre: true);
    }

    private void AddGenreTabs()
    {
        for (var index = 0; index < GenrePageTabs.Length; index++)
        {
            var tab = index;
            var button = new IconButtonModel(MappingBounds(20 + index * 240, -38, 232, 36), GenrePageTabs[index])
                { IsSelected = index == genrePageTab };
            mappingEditorButtons.Add(new(button, () =>
            {
                if (mappingComposition.Length > 0) return;
                SetMappingTextFocus(false);
                genrePageTab = tab;
                genrePreviewScroll = 0;
                mappingFocus = -1;
                mappingWidth = -1;
            }, Tooltip: $"{GenrePageTabs[index]}を表示します。編集中の内容は保持されます。"));
        }
    }

    private IReadOnlyList<GenreDataGroup> BuildGenrePreviewGroups()
    {
        var groups = BuildGenreDataGroups().ToList();
        foreach (var style in mappingDraft?.Build() ?? [])
            if (!groups.Any(group => group.GenreId == style.Key)) groups.Add(new(style.Key, 0, 0));
        return groups.OrderByDescending(group => group.SpaceCount).ThenBy(group => group.GenreId, StringComparer.Ordinal).ToArray();
    }

    private void ScrollGenreOrMapping(int offset)
    {
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
        var groups = BuildGenrePreviewGroups();
        var total = groups.Sum(group => group.SpaceCount);
        GenrePreviewText($"{GenrePageTabs[genrePageTab]}　合計 {total} sp / {groups.Sum(group => group.CircleCount)} サークル", 20, 102, 960, 20);
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
            var area = MappingBounds(20, 146, 380, 290);
            var center = new ScreenPoint(area.X + area.Width / 2, area.Y + area.Height / 2);
            var radius = Math.Min(area.Width, area.Height) / 2;
            var positive = groups.Where(group => group.SpaceCount > 0).ToArray();
            for (var segment = 0; segment < 720; segment++)
            {
                var slice = FindGenreSlice(positive, (segment + .5) / 720 * total);
                var angle = -Math.PI / 2 + segment * Math.PI * 2 / 720;
                DrawLine(center, new(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius), 2.5,
                    GetGenreStyle(slice.GenreId == "（未設定）" ? null : slice.GenreId).Primary);
            }
            DrawPiePatterns(center, radius, positive.Select(group => (group.GenreId, group.SpaceCount)).ToArray(), total);
            DrawCircle(center, radius, Color.LightGray);
        }
        if (genrePageTab != 1 && total == 0) GenrePreviewText("スペース数が 0 のため、グラフは表示されません。", 20, 142, 960);
        var visible = groups.Skip(genrePreviewScroll).Take(GenrePreviewPageSize).ToArray();
        for (var index = 0; index < visible.Length; index++)
        {
            var group = visible[index];
            var catalog = genrePageTab == 1;
            var x = catalog ? 20 + index % 3 * 320 : genrePageTab == 3 ? 440 : 20 + index % 2 * 480;
            var y = catalog ? 146 + index / 3 * 50 : genrePageTab == 3 ? 146 + index * 48 : 286 + index / 2 * 50;
            DrawGenreTile(MappingBounds(x, y, catalog ? 74 : 64, 36), group.GenreId == "（未設定）" ? null : group.GenreId, Color.Transparent, 0);
            var percent = total > 0 ? 100d * group.SpaceCount / total : 0;
            GenrePreviewText($"{group.GenreId}　{group.SpaceCount} sp ({percent:0.0}%)", x + (catalog ? 80 : 74), y,
                catalog ? 232 : 396, 15);
        }
        GenrePreviewText($"{genrePreviewScroll + 1}–{genrePreviewScroll + visible.Length} / {groups.Count} ジャンル", 380, 462, 600, 14);
    }

    private string GenrePreviewTooltip(ScreenPoint pointer)
    {
        var groups = BuildGenrePreviewGroups();
        var total = groups.Sum(group => group.SpaceCount);
        double? fraction = null;
        var bar = MappingBounds(20, 150, 960, 76);
        if (genrePageTab == 2 && Contains(bar, pointer)) fraction = (pointer.X - bar.X) / bar.Width;
        var pie = MappingBounds(20, 146, 380, 290);
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
}
