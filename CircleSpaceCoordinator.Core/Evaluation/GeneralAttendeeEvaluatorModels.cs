namespace CircleSpaceCoordinator.Core.Evaluation;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;

public sealed record GenreEvaluationResult(
    string GenreId,
    int AssignedCircleCount,
    int ContinuousGroupCount,
    double Score);

public sealed record AudienceEvaluationResult(
    string PlanId,
    double GeneralAttendeeScore,
    double CircleParticipantScore,
    bool CombinedSpaceRequirementsSatisfied,
    IReadOnlyList<GenreEvaluationResult> Genres);

