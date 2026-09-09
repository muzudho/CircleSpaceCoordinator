namespace CircleSpaceCoordinator.Core.Model;

using CircleSpaceCoordinator.Core.Geometry;

public sealed record DeskRun(QuarterTurn Orientation, IReadOnlyList<string> DeskIds);

