namespace CircleSpaceCoordinator.Core.Model;

using CircleSpaceCoordinator.Core.Geometry;

public sealed record ParticipantAssignment(
    string ParticipantId,
    IReadOnlySet<GridPosition> OccupiedCells,
    GridPosition ScoringPosition)
{
    /// <summary>
    /// Two participants with the same ID jointly use cells on one physical desk.
    /// This is called a "合体スペース" in the existing operation.
    /// </summary>
    public string? CombinedSpaceId { get; init; }
}

/// <summary>A public-facing block and seat name assigned to one cell of a physical desk.</summary>
public sealed record DeskSeatLabel(
    string DeskPlacementId,
    GridPosition RelativeCell,
    string BlockName,
    string SeatName);

/// <summary>Physical venue arrangement shared by one or more circle layouts.</summary>
public sealed record DeskLayout(
    string Id,
    string Name,
    IReadOnlyList<DeskPlacement> DeskPlacements)
{
    public string? Description { get; init; }
    public IReadOnlyList<IslandConnector> IslandConnectors { get; init; } = [];
    public IReadOnlyList<DisabledIslandConnection> DisabledIslandConnections { get; init; } = [];
    public IReadOnlyList<FacingRegion> FacingRegions { get; init; } = [];
    public IReadOnlyList<DeskSeatLabel> SeatLabels { get; init; } = [];
}

/// <summary>Circle assignments evaluated against exactly one physical desk layout.</summary>
public sealed record CircleLayout(
    string Id,
    string Name,
    string DeskLayoutId,
    IReadOnlyList<ParticipantAssignment> Assignments)
{
    public string? Description { get; init; }
    public IReadOnlyList<ParticipantAssignment> TemporaryPlacements { get; init; } = [];
}

public sealed record Plan(
    string Id,
    string Name,
    IReadOnlyList<DeskPlacement> DeskPlacements,
    IReadOnlyList<ParticipantAssignment> Assignments)
{
    public string? Description { get; init; }
    public IReadOnlyList<ParticipantAssignment> TemporaryPlacements { get; init; } = [];

    public IReadOnlyList<IslandConnector> IslandConnectors { get; init; } = [];

    /// <summary>Automatically detected physical links excluded from logical island connectivity.</summary>
    public IReadOnlyList<DisabledIslandConnection> DisabledIslandConnections { get; init; } = [];

    public IReadOnlyList<FacingRegion> FacingRegions { get; init; } = [];

    public IReadOnlyList<DeskSeatLabel> SeatLabels { get; init; } = [];
}
