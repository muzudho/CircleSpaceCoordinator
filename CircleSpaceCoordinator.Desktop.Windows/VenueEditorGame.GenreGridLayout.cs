namespace CircleSpaceCoordinator.Desktop.Windows;

using Microsoft.Xna.Framework;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private enum GenreGridLayoutMode { FullWidth, SplitPane }
    private GenreGridLayoutMode genreGridLayoutMode;
    private PortableMaterial? packageGenreTable;
    private int packageGenreScroll;

    private StyleMappingEntry[] PackageGenreRows()
    {
        var rows = packageGenreTable?.Rows() ?? [];
        if (genreOrdinalSort) return rows.OrderBy(row => row.Key, StringComparer.Ordinal).ToArray();
        var order = packageGenreTable?.Metadata().RowOrder.ToArray() ?? [];
        return rows.OrderBy(row => Array.IndexOf(order, row.Key) is var index && index >= 0 ? index : int.MaxValue).ToArray();
    }

    private void ShowPackageGenreTable(PortableMaterial table)
    {
        shadingSelection.Clear();
        packageGenreTable = table;
        genreRowActionsRight = false;
        selectedPackageGenreKey = null;
        packageGenreScroll = 0;
        genrePageTab = 0;
        genreGridLayoutMode = GenreGridLayoutMode.SplitPane;
        mappingWidth = -1;
        mappingFocus = -1;
    }

    private void ScrollPackageGenreRows(int offset)
    {
        SetGenreScroll(true, packageGenreScroll + offset);
    }
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
        var contentWidth = Math.Max(1, paneWidth - 18 * MappingEditorScale);
        return new(margin + (right ? paneWidth + gap : 0) + (x - 20) * contentWidth / 960,
            vertical.Y, width * contentWidth / 960, vertical.Height);
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
            shadingSelection.Clear();
            genreGridLayoutMode = GenreGridSplit ? GenreGridLayoutMode.FullWidth : GenreGridLayoutMode.SplitPane;
            if (!GenreGridSplit) selectedPackageGenreKey = null;
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

    private void DrawPackageGenreGrid()
    {
        var headers = new[] { "順", MappingRowLabel, "太線色", "細線色", "網掛け", "見本", "コメント" };
        DrawRectangle(MappingGridBounds(20, 102, 960, 34, right: true), new Color(48, 65, 77));
        for (var column = 0; column < headers.Length; column++)
        {
            var width = MappingColumnEdges[column + 1] - MappingColumnEdges[column] - 6;
            textRenderer?.Draw(headers[column], ToRectangle(MappingGridBounds(MappingColumnEdges[column], 104, width, 30, right: true), 3),
                Color.White, Math.Max(10, (int)(13 * MappingEditorScale)), true);
            if (packageGenreTable is null)
                for (var row = 0; row < MappingVisibleRows; row++)
                    DrawOutline(MappingCell(row, column, right: true), 1, new Color(48, 65, 77));
        }
        if (packageGenreTable is { } loaded)
            DrawTableHeader(loaded.Name, loaded.OverallComment, right: true, editable: false);
        else
            textRenderer?.Draw("パッケージ ＞ 網掛け対応表（未選択）",
                ToRectangle(MappingGridBounds(20, 62, 960, 36, right: true), 6),
                Color.LightGray, Math.Max(10, (int)(15 * MappingEditorScale)), true);
        var rows = PackageGenreRows();
        void Text(string value, ScreenRectangle bounds, Color? color = null) =>
            textRenderer?.Draw(value, ToRectangle(bounds, 3), color ?? Color.White, Math.Max(10, (int)(17 * MappingEditorScale)), true);
        for (var row = 0; row < MappingVisibleRows && packageGenreScroll + row < rows.Length; row++)
        {
            var style = rows[packageGenreScroll + row];
            for (var column = 0; column < headers.Length; column++)
            {
                var bounds = MappingCell(row, column, right: true);
                if (column == 3 && style.Pattern == "solid") continue;
                var plain = column is 1 or 6;
                if (!plain) DrawRectangle(bounds, new Color(35, 43, 54));
                if (column == 0)
                {
                    var order = packageGenreTable?.Metadata().RowOrder.ToArray() ?? [];
                    var index = Array.IndexOf(order, style.Key);
                    Text((index >= 0 ? index + 1 : packageGenreScroll + row + 1).ToString(), bounds);
                }
                else if (column == 1) Text(style.Key, bounds);
                else if (column == 6) Text(style.KnowledgeComment ?? "コメントを入力", bounds);
                else if (column is 2 or 3)
                {
                    var id = column == 2 ? style.PrimaryColor : style.SecondaryColor;
                    var color = GenreColorFromId(id, Color.Gray);
                    DrawRectangle(bounds, color);
                    Text(StyleMappingDraft.Colors.FirstOrDefault(choice => choice.Id == id).Label ?? id, bounds, MappingColorText(color));
                }
                else
                {
                    var swatch = new ScreenRectangle(bounds.X + 4, bounds.Y + 3, bounds.Width - 8, bounds.Height - 6);
                    DrawRectangle(swatch, column == 4 ? Color.Black : GenreColorFromId(style.PrimaryColor, Color.Gray));
                    DrawGenrePattern(swatch, GenrePatternFromId(style.Pattern), column == 4 ? Color.White : GenreColorFromId(style.SecondaryColor, Color.White), 255);
                }
                if (!plain) DrawOutline(bounds, 1, new Color(100, 119, 130));
            }
            if (ShadingRowSelected(true, style.Key))
            {
                DrawOutline(GenreTargetRowBounds(row, right: true), 2 * MappingEditorScale, OperationTargetColor);
                DrawGenreCellHover(row, style.Pattern, true);
            }
        }
        if (packageGenreTable is not null && rows.Length == 0)
            Text("この表に行はありません。", MappingGridBounds(20, 142, 960, 46, right: true));
        var divider = MappingGridBounds(20, 102, 960, 390);
        var x = GraphicsDevice.Viewport.Width / 2d;
        DrawLine(new(x, divider.Y), new(x, divider.Y + divider.Height), 1, new Color(100, 119, 130));
    }
}
