namespace CircleSpaceCoordinator.Core.Evaluation;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;

public sealed record VenueTopologyGraph(
    IReadOnlyDictionary<GridPosition, IReadOnlySet<GridPosition>> Neighbors,
    IReadOnlyList<(GridPosition FirstCell, GridPosition SecondCell)> FacingCellPairs,
    IReadOnlyDictionary<GridPosition, string> DeskIdByCell);

