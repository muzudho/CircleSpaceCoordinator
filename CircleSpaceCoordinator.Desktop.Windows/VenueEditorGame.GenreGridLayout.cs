namespace CircleSpaceCoordinator.Desktop.Windows;

using Microsoft.Xna.Framework;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private enum GenreGridLayoutMode { FullWidth, SplitPane }
    private GenreGridLayoutMode genreGridLayoutMode;
    private const string GenreGridLayoutButtonName = "データの読み書き（表示切り替え）";
    private bool GenreGridVisible => mappingKnowledgeComments && genrePageTab == 0;
    private bool GenreGridSplit => GenreGridVisible && genreGridLayoutMode == GenreGridLayoutMode.SplitPane;

    // Grid titles, cells and pagination share the same pane coordinates.
    private ScreenRectangle MappingGridBounds(double x, double y, double width, double height, bool right = false)
    {
        if (!GenreGridVisible) return MappingBounds(x, y, width, height);
        var vertical = MappingBounds(x, y, width, height);
        var margin = 20 * MappingEditorScale;
        var gap = 16 * MappingEditorScale;
        var totalWidth = Math.Max(1, GraphicsDevice.Viewport.Width - 2 * margin);
        var paneWidth = GenreGridSplit ? (totalWidth - gap) / 2 : totalWidth;
        return new(margin + (right ? paneWidth + gap : 0) + (x - 20) * paneWidth / 960,
            vertical.Y, width * paneWidth / 960, vertical.Height);
    }

    private void AddGenreGridLayoutButton()
    {
        if (!GenreGridVisible) return;
        mappingEditorButtons.Add(new(new IconButtonModel(MappingBounds(932, 18, 48, 36), GenreGridLayoutButtonName)
        {
            IsSelected = GenreGridSplit,
        }, () =>
        {
            if (mappingComposition.Length > 0) return;
            SetMappingTextFocus(false);
            genreGridLayoutMode = GenreGridSplit ? GenreGridLayoutMode.FullWidth : GenreGridLayoutMode.SplitPane;
            mappingFocus = -1;
            mappingWidth = -1;
            pressedMappingButton = null;
            OpenPackageReadDialog();
        }, Tooltip: GenreGridSplit
            ? "FullWidth（全幅表示）に切り替えて、パッケージ読込ダイアログを開きます。"
            : "SplitPane（左右分割）に切り替えて、パッケージ読込ダイアログを開きます。"));
    }

    private void DrawGenreGridShip(ScreenRectangle bounds, Color color)
    {
        ScreenPoint Point(double x, double y) => new(bounds.X + x * bounds.Width, bounds.Y + y * bounds.Height);
        void Line(double x1, double y1, double x2, double y2) =>
            DrawLine(Point(x1, y1), Point(x2, y2), Math.Max(1, 2 * MappingEditorScale), color);
        Line(.18, .56, .82, .56);
        Line(.18, .56, .30, .77);
        Line(.30, .77, .70, .77);
        Line(.70, .77, .82, .56);
        Line(.35, .56, .35, .34);
        Line(.35, .34, .65, .34);
        Line(.65, .34, .65, .56);
        Line(.48, .34, .48, .16);
        Line(.48, .16, .62, .23);
        Line(.62, .23, .48, .23);
        Line(.17, .86, .33, .89);
        Line(.33, .89, .50, .85);
        Line(.50, .85, .67, .89);
        Line(.67, .89, .83, .86);
    }

    private void DrawEmptyPackageGenreGrid()
    {
        var headers = new[] { "順", "ジャンル", "太線色", "細線色", "網掛け", "見本", "コメント" };
        DrawRectangle(MappingGridBounds(20, 102, 960, 34, right: true), new Color(48, 65, 77));
        for (var column = 0; column < headers.Length; column++)
        {
            var width = MappingColumnEdges[column + 1] - MappingColumnEdges[column] - 6;
            textRenderer?.Draw(headers[column], ToRectangle(MappingGridBounds(MappingColumnEdges[column], 104, width, 30, right: true), 3),
                Color.White, Math.Max(10, (int)(13 * MappingEditorScale)), true);
            for (var row = 0; row < MappingVisibleRows; row++)
                DrawOutline(MappingCell(row, column, right: true), 1, new Color(48, 65, 77));
        }
        var message = MappingGridBounds(20, 62, 960, 36, right: true);
        textRenderer?.Draw("パッケージ ＞ ジャンルコード表（未選択）", ToRectangle(message, 6),
            Color.LightGray, Math.Max(10, (int)(15 * MappingEditorScale)), true);
        var divider = MappingGridBounds(20, 102, 960, 390);
        var x = divider.X + divider.Width + 8 * MappingEditorScale;
        DrawLine(new(x, divider.Y), new(x, divider.Y + divider.Height), 1, new Color(100, 119, 130));
    }
}
