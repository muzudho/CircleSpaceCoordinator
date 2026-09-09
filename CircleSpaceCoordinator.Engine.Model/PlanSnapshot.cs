namespace CircleSpaceCoordinator.Application.Queries;

using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;

public sealed record DeskView(
    string Id,
    string DeskTypeId,
    string DeskTypeName,
    GridPosition Anchor,
    QuarterTurn Orientation,
    IReadOnlySet<GridPosition> OccupiedCells);

public sealed record AssignmentView(
    string ParticipantId,
    string ParticipantName,
    int RequiredCellCount,
    IReadOnlySet<GridPosition> OccupiedCells,
    GridPosition ScoringPosition,
    string? CombinedSpaceId,
    string? GenreId);

public sealed record UnassignedParticipantView(
    string ParticipantId,
    string ParticipantName,
    int RequiredCellCount);

public sealed record PlanSnapshot(
    string PlanId,
    string PlanName,
    IReadOnlyList<DeskView> Desks,
    IReadOnlyList<AssignmentView> Assignments,
    IReadOnlyList<UnassignedParticipantView> UnassignedParticipants,
    EvaluationResult Evaluation,
    AudienceEvaluationResult AudienceEvaluation);

