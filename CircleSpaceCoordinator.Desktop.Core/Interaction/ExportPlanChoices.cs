namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Application.Plans;
using CircleSpaceCoordinator.Core.Model;

public sealed record ExportPlanChoice(Plan Plan, RankedPlan Evaluation);

public static class ExportPlanChoices
{
    public static IReadOnlyList<ExportPlanChoice> Filter(IReadOnlyList<ExportPlanChoice> choices, IReadOnlySet<string> pins, bool pinnedOnly) =>
        pinnedOnly ? choices.Where(item => pins.Contains(item.Plan.Id)).ToArray() : choices;

    public static IReadOnlyList<ExportPlanChoice> Build(CircleSpaceProject project, IReadOnlyList<RankedPlan> evaluations)
    {
        var plans = project.Plans.ToDictionary(plan => plan.Id, StringComparer.Ordinal);
        return evaluations.Where(item => plans.ContainsKey(item.PlanId))
            .OrderByDescending(item => item.GeneralAttendeeScore)
            .ThenByDescending(item => item.CircleParticipantScore)
            .ThenBy(item => item.PlanId, StringComparer.Ordinal)
            .Select(item => new ExportPlanChoice(plans[item.PlanId], item)).ToArray();
    }
}
