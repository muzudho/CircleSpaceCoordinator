namespace CircleSpaceCoordinator.Core.Model;

using CircleSpaceCoordinator.Core.Geometry;

public sealed record IslandConnector(
    string Id,
    string FirstDeskId,
    string SecondDeskId,
    GridPosition? FirstCell = null,
    GridPosition? SecondCell = null);

/// <summary>One physical, automatically detected cell-to-cell link that is intentionally not part of an island.</summary>
public sealed record DisabledIslandConnection(GridPosition FirstCell, GridPosition SecondCell);

public sealed record FacingRegion(string Id, GridPosition FirstCorner, GridPosition SecondCorner);

/// <summary>A maze root attached to a frame; both cell and direction are relative to that frame.</summary>
public sealed record IslandStart(string DeskPlacementId, GridPosition RelativeCell, QuarterTurn Direction)
{
    public GridPosition GetCell(DeskPlacement desk) => desk.Anchor + RelativeCell.Rotate(desk.Orientation);
    public QuarterTurn GetDirection(DeskPlacement desk) => (QuarterTurn)(((int)Direction + (int)desk.Orientation) % 4);
}
