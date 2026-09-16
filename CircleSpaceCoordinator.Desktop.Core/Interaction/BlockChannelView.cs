namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;

/// <summary>Seat-cell block backgrounds and sparse labels, anchored to the venue grid.</summary>
public sealed class BlockChannelView
{
    public IReadOnlyDictionary<GridPosition, string> Cells { get; }
    public IReadOnlyDictionary<GridPosition, string> Labels { get; }

    private BlockChannelView(Dictionary<GridPosition, string> cells)
    {
        Cells = cells;
        // One label per block in each 4x4 tile. Choose an actual member cell near
        // its centre, so thin and small blocks remain labelled too.
        Labels = cells.Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .GroupBy(item => (item.Value, X: (int)Math.Floor(item.Key.X / 4d), Y: (int)Math.Floor(item.Key.Y / 4d)))
            .Select(group => group.OrderBy(item =>
                Math.Pow(item.Key.X - (group.Key.X * 4 + 1.5), 2) +
                Math.Pow(item.Key.Y - (group.Key.Y * 4 + 1.5), 2))
                .ThenBy(item => item.Key.Y).ThenBy(item => item.Key.X).First())
            .ToDictionary(item => item.Key, item => item.Value);
    }

    public static BlockChannelView Create(CircleSpaceProject project, Plan plan)
    {
        var types = project.DeskTypes.ToDictionary(type => type.Id);
        var labels = plan.SeatLabels.ToDictionary(label => (label.DeskPlacementId, label.RelativeCell));
        var cells = new Dictionary<GridPosition, string>();
        foreach (var placement in plan.DeskPlacements)
        foreach (var cell in placement.GetSeatCells(types[placement.DeskTypeId]))
        {
            var relative = new GridPosition(cell.X - placement.Anchor.X, cell.Y - placement.Anchor.Y)
                .Rotate((QuarterTurn)((4 - (int)placement.Orientation) % 4));
            cells[cell] = labels.GetValueOrDefault((placement.Id, relative))?.BlockName ?? "";
        }
        return new(cells);
    }
}
