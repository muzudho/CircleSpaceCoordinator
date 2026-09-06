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

public static class PlanViewService
{
    public static PlanSnapshot Build(CircleSpaceProject project, string planId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);

        var plan = project.Plans.SingleOrDefault(item => item.Id == planId)
            ?? throw new KeyNotFoundException($"Plan '{planId}' does not exist.");
        var deskTypes = project.DeskTypes.ToDictionary(item => item.Id);
        var participants = project.Participants.ToDictionary(item => item.Id);
        var assignedParticipantIds = plan.Assignments.Select(item => item.ParticipantId).ToHashSet();
        var evaluation = ProjectEvaluator.Evaluate(project).Single(result => result.PlanId == planId);

        var desks = plan.DeskPlacements.Select(placement =>
        {
            var deskType = deskTypes[placement.DeskTypeId];
            return new DeskView(
                placement.Id,
                deskType.Id,
                deskType.Name,
                placement.Anchor,
                placement.Orientation,
                placement.GetOccupiedCells(deskType));
        }).ToArray();
        var assignments = plan.Assignments.Select(assignment =>
        {
            var participant = participants[assignment.ParticipantId];
            return new AssignmentView(
                participant.Id,
                participant.DisplayName,
                participant.RequiredCellCount,
                assignment.OccupiedCells.ToHashSet(),
                assignment.ScoringPosition,
                assignment.CombinedSpaceId,
                participant.GenreId);
        }).ToArray();
        var unassigned = project.Participants
            .Where(participant => !assignedParticipantIds.Contains(participant.Id))
            .Select(participant => new UnassignedParticipantView(
                participant.Id,
                participant.DisplayName,
                participant.RequiredCellCount))
            .ToArray();

        var audienceEvaluation = GeneralAttendeeEvaluator.Evaluate(project).Single(result => result.PlanId == planId);
        return new PlanSnapshot(plan.Id, plan.Name, desks, assignments, unassigned, evaluation, audienceEvaluation);
    }
}
