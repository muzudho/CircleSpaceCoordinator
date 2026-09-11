namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Desktop.Windows.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private ParticipantTableView? participantTable;
    private CircleSpaceProject? tableProject;
    private readonly TableScrollPosition tableScroll = new();
    private DynamicTextRenderer? tableTextRenderer;
    private (int Row, int Column, int Width, int Height)? tableTextPage;
    private bool? tableScrollDragVertical;
    private double tableScrollGrabOffset;

    private ParticipantTableView GetParticipantTable()
    {
        if (!ReferenceEquals(tableProject, workspace!.Project))
        {
            tableProject = workspace.Project;
            participantTable = new ParticipantTableView(tableProject);
            tableTextPage = null;
        }
        return participantTable!;
    }

    private (ScreenRectangle Grid, int Rows, int Columns, double CellWidth) GetParticipantTableLayout()
    {
        var width = Math.Max(1, GraphicsDevice.Viewport.Width - 24 - 60 - 18);
        var cellWidth = Math.Min(180d, width);
        var columns = Math.Max(1, (int)(width / cellWidth));
        var rows = Math.Max(1, (GraphicsDevice.Viewport.Height - StatusBarHeight - 182 - 30 - 18 - 12) / 28);
        return (new ScreenRectangle(72, 182, columns * cellWidth, 30 + rows * 28), rows, columns, cellWidth);
    }

    private void MoveParticipantTable(int row, int column)
    {
        var table = GetParticipantTable();
        var layout = GetParticipantTableLayout();
        tableScroll.MoveTo(row, column, table.RowCount, table.ColumnCount, layout.Rows, layout.Columns);
    }

    private (ScreenRectangle Track, ScreenRectangle Thumb, int Maximum) GetTableScrollbar(bool vertical)
    {
        var table = GetParticipantTable();
        var layout = GetParticipantTableLayout();
        var grid = layout.Grid;
        var total = vertical ? table.RowCount : table.ColumnCount;
        var visible = vertical ? layout.Rows : layout.Columns;
        var maximum = Math.Max(0, total - visible);
        var track = vertical
            ? new ScreenRectangle(grid.X + grid.Width + 2, grid.Y + 30, 16, grid.Height - 30)
            : new ScreenRectangle(grid.X, grid.Y + grid.Height + 2, grid.Width, 16);
        var length = vertical ? track.Height : track.Width;
        var thumbLength = Math.Min(length, Math.Max(24, length * Math.Min(1, visible / (double)Math.Max(1, total))));
        var offset = maximum == 0 ? 0 : (length - thumbLength) * (vertical ? tableScroll.Row : tableScroll.Column) / maximum;
        var thumb = vertical
            ? new ScreenRectangle(track.X, track.Y + offset, track.Width, thumbLength)
            : new ScreenRectangle(track.X + offset, track.Y, thumbLength, track.Height);
        return (track, thumb, maximum);
    }

    private void UpdateParticipantDataInput(KeyboardState keyboard, MouseState mouse, ScreenPoint pointer)
    {
        // This branch owns input completely; table gestures cannot edit or zoom the venue behind it.
        hoveredPlanId = null;
        hoveredPlanCopy = hoveredPlanRename = hoveredLayoutAdd = hoveredLayoutDelete = hoveredLayoutBind = hoveredLayoutRename = false;
        MoveParticipantTable(tableScroll.Row, tableScroll.Column);
        if (IsControlDown(keyboard) && IsPressed(keyboard, Keys.Z)) commandController?.Undo();
        if (IsControlDown(keyboard) && IsPressed(keyboard, Keys.Y)) commandController?.Redo();
        var layout = GetParticipantTableLayout();
        var row = tableScroll.Row;
        var column = tableScroll.Column;
        if (IsPressed(keyboard, Keys.Down)) row++;
        if (IsPressed(keyboard, Keys.Up)) row--;
        if (IsPressed(keyboard, Keys.Right)) column++;
        if (IsPressed(keyboard, Keys.Left)) column--;
        if (IsPressed(keyboard, Keys.PageDown)) row += layout.Rows;
        if (IsPressed(keyboard, Keys.PageUp)) row -= layout.Rows;
        if (IsPressed(keyboard, Keys.Home)) { row = 0; if (IsControlDown(keyboard)) column = 0; }
        if (IsPressed(keyboard, Keys.End)) { row = GetParticipantTable().RowCount; if (IsControlDown(keyboard)) column = GetParticipantTable().ColumnCount; }
        var wheel = mouse.ScrollWheelValue - previousMouse.ScrollWheelValue;
        if (wheel != 0 && pointer.Y >= 182 && pointer.Y < GraphicsDevice.Viewport.Height - StatusBarHeight)
        {
            var delta = Math.Sign(wheel) * Math.Max(1, Math.Abs(wheel) / 120) * 3;
            if (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift)) column -= delta;
            else row -= delta;
        }
        MoveParticipantTable(row, column);

        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released)
        {
            pressedToolbarButton = toolbarButtons.LastOrDefault(button => button.Model.Contains(pointer));
            if (pressedToolbarButton is not null) pressedToolbarButton.Model.Press(pointer);
            else
            {
                foreach (var vertical in new[] { true, false })
                {
                    var bar = GetTableScrollbar(vertical);
                    if (!Contains(bar.Track, pointer) || bar.Maximum == 0) continue;
                    tableScrollDragVertical = vertical;
                    tableScrollGrabOffset = Contains(bar.Thumb, pointer)
                        ? (vertical ? pointer.Y - bar.Thumb.Y : pointer.X - bar.Thumb.X)
                        : (vertical ? bar.Thumb.Height : bar.Thumb.Width) / 2;
                    break;
                }
                if (tableScrollDragVertical is null && Contains(layout.Grid, pointer) && pointer.Y >= layout.Grid.Y + 30)
                {
                    var selectedRow = tableScroll.Row + (int)((pointer.Y - layout.Grid.Y - 30) / 28);
                    var selectedColumn = tableScroll.Column + (int)((pointer.X - layout.Grid.X) / layout.CellWidth);
                    var table = GetParticipantTable();
                    if (selectedRow < table.RowCount && selectedColumn < table.ColumnCount)
                        ShowParticipantCell(selectedRow, selectedColumn);
                }
            }
        }
        if (mouse.LeftButton == ButtonState.Pressed && tableScrollDragVertical is { } isVertical)
        {
            var bar = GetTableScrollbar(isVertical);
            var travel = isVertical ? bar.Track.Height - bar.Thumb.Height : bar.Track.Width - bar.Thumb.Width;
            var location = isVertical ? pointer.Y - bar.Track.Y : pointer.X - bar.Track.X;
            var fraction = travel <= 0 ? 0 : Math.Clamp((location - tableScrollGrabOffset) / travel, 0, 1);
            var position = (int)Math.Round(fraction * bar.Maximum);
            MoveParticipantTable(isVertical ? position : tableScroll.Row, isVertical ? tableScroll.Column : position);
        }
        if (mouse.LeftButton == ButtonState.Released)
        {
            tableScrollDragVertical = null;
            if (previousMouse.LeftButton == ButtonState.Pressed && pressedToolbarButton is { } button)
            {
                pressedToolbarButton = null;
                if (button.Model.Release(pointer))
                {
                    var outcome = ExecuteToolbarAction(button.Action, pointer);
                    Log("toolbar_click", outcome.Success, $"action={button.Action};{outcome.Detail}");
                }
            }
        }
    }

    private void ShowParticipantCell(int row, int column)
    {
        var table = GetParticipantTable();
        using var form = new System.Windows.Forms.Form
        {
            Text = $"{row + 1} 行 / {column + 1} 列: {table.Headers[column]}（閲覧専用）",
            Width = 760, Height = 400, StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen,
            MinimizeBox = false, ShowInTaskbar = false,
        };
        var valueBox = new System.Windows.Forms.TextBox
        {
            Multiline = true, ReadOnly = true, WordWrap = false,
            ScrollBars = System.Windows.Forms.ScrollBars.Both, Dock = System.Windows.Forms.DockStyle.Fill,
            Text = table.GetValue(row, column), Font = new System.Drawing.Font("Meiryo", 12),
        };
        form.Controls.Add(valueBox);
        form.ShowDialog();
    }

    private void DrawParticipantData()
    {
        if (workspace is null || spriteBatch is null) return;
        var table = GetParticipantTable();
        MoveParticipantTable(tableScroll.Row, tableScroll.Column);
        var layout = GetParticipantTableLayout();
        var grid = layout.Grid;
        var page = (tableScroll.Row, tableScroll.Column, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        if (tableTextPage != page)
        {
            // Bound GPU text resources to one visible page, including when scrolling through unique long values.
            tableTextRenderer?.Dispose();
            tableTextRenderer = new DynamicTextRenderer(GraphicsDevice, spriteBatch);
            tableTextPage = page;
        }
        var ink = new Color(222, 234, 240);
        tableTextRenderer!.Draw($"{table.SourceDescription}　{table.RowCount:N0} 行 × {table.ColumnCount:N0} 列",
            new Rectangle(12, 118, Math.Max(1, GraphicsDevice.Viewport.Width - 24), 28), ink, 17, true);
        var exportPlan = CircleSeatExportBuilder.GetExportPlan(workspace.Project);
        var exportTarget = exportPlan is null ? "配置案　未決定" : $"配置決定案: {exportPlan.Name}";
        tableTextRenderer.Draw($"{exportTarget}　｜　表示: {Math.Min(table.RowCount, tableScroll.Row + 1)}～{Math.Min(table.RowCount, tableScroll.Row + layout.Rows)} 行 / {tableScroll.Column + 1}～{Math.Min(table.ColumnCount, tableScroll.Column + layout.Columns)} 列",
            new Rectangle(12, 150, Math.Max(1, GraphicsDevice.Viewport.Width - 24), 26), ink, 15);
        for (var c = 0; c < Math.Min(layout.Columns, table.ColumnCount - tableScroll.Column); c++)
        {
            var column = tableScroll.Column + c;
            DrawTableCell($"{column + 1}: {table.Headers[column]}", new ScreenRectangle(grid.X + c * layout.CellWidth, grid.Y, layout.CellWidth, 30), new Color(29, 88, 82), true);
        }
        for (var r = 0; r < Math.Min(layout.Rows, table.RowCount - tableScroll.Row); r++)
        {
            var row = tableScroll.Row + r;
            var y = grid.Y + 30 + r * 28;
            DrawTableCell((row + 1).ToString(), new ScreenRectangle(12, y, 60, 28), new Color(36, 53, 63), true);
            for (var c = 0; c < Math.Min(layout.Columns, table.ColumnCount - tableScroll.Column); c++)
                DrawTableCell(table.GetValue(row, tableScroll.Column + c), new ScreenRectangle(grid.X + c * layout.CellWidth, y, layout.CellWidth, 28),
                    row % 2 == 0 ? new Color(30, 37, 47) : new Color(37, 45, 55));
        }
        if (table.RowCount == 0)
            tableTextRenderer.Draw("データがありません。左上の［Excel / CSV 読込］から読み込んでください。",
                new Rectangle(80, 230, Math.Max(1, GraphicsDevice.Viewport.Width - 100), 28), ink, 17);
        foreach (var vertical in new[] { true, false })
        {
            var bar = GetTableScrollbar(vertical);
            DrawRectangle(bar.Track, new Color(16, 22, 29));
            DrawRectangle(bar.Thumb, bar.Maximum > 0 ? new Color(69, 148, 143) : new Color(50, 64, 73));
        }
    }

    private void DrawTableCell(string text, ScreenRectangle bounds, Color background, bool bold = false)
    {
        DrawRectangle(bounds, background);
        DrawOutline(bounds, 1, new Color(66, 82, 94));
        // Full values remain available in the detail dialog. Do not rasterize an unbounded cell string.
        var preview = text.Length > 100 ? text[..100] + "…" : text;
        preview = preview.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        tableTextRenderer!.Draw(preview, ToRectangle(bounds, 4), Color.White, 14, bold);
    }
}
