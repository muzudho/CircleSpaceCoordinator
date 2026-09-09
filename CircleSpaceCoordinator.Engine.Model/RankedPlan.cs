namespace CircleSpaceCoordinator.Application.Plans;

using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;

public sealed record RankedPlan(
    int Rank,
    string PlanId,
    string PlanName,
    double TotalScore,
    double GeneralAttendeeScore,
    double CircleParticipantScore,
    bool CombinedSpaceRequirementsSatisfied,
    IReadOnlyList<FeatureEvaluationResult> Features);

