namespace CircleSpaceCoordinator.Application.Editing;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;

public static class DeskSeatLabelEditor
{
    public static CircleSpaceProject ReplaceLabels(
        CircleSpaceProject project,
        string planId,
        IReadOnlyList<DeskSeatLabel> labels)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        ArgumentNullException.ThrowIfNull(labels);
        var planIndex = FindPlanIndex(project, planId);
        return ReplacePlan(project, planIndex, project.Plans[planIndex] with
        {
            SeatLabels = labels.Select(item => item with { }).ToArray(),
        });
    }

    public static CircleSpaceProject SetLabel(
        CircleSpaceProject project,
        string planId,
        string deskPlacementId,
        GridPosition relativeCell,
        string blockName,
        string seatName)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deskPlacementId);
        ArgumentException.ThrowIfNullOrWhiteSpace(blockName);
        ArgumentException.ThrowIfNullOrWhiteSpace(seatName);

        var plan = project.Plans[FindPlanIndex(project, planId)];
        var labels = plan.SeatLabels
            .Where(item => item.DeskPlacementId != deskPlacementId || item.RelativeCell != relativeCell)
            .Append(new DeskSeatLabel(deskPlacementId, relativeCell, blockName.Trim(), seatName.Trim()))
            .ToArray();
        return ReplaceLabels(project, planId, labels);
    }

    public static CircleSpaceProject RemoveLabel(
        CircleSpaceProject project,
        string planId,
        string deskPlacementId,
        GridPosition relativeCell)
    {
        ArgumentNullException.ThrowIfNull(project);
        var planIndex = FindPlanIndex(project, planId);
        var plan = project.Plans[planIndex];
        return ReplaceLabels(project, planId, plan.SeatLabels
            .Where(item => item.DeskPlacementId != deskPlacementId || item.RelativeCell != relativeCell)
            .ToArray());
    }

    private static int FindPlanIndex(CircleSpaceProject project, string planId)
    {
        var index = project.Plans.ToList().FindIndex(plan => plan.Id == planId);
        return index >= 0 ? index : throw new KeyNotFoundException($"Plan '{planId}' does not exist.");
    }

    private static CircleSpaceProject ReplacePlan(CircleSpaceProject project, int planIndex, Plan plan)
    {
        var plans = project.Plans.ToArray();
        plans[planIndex] = plan;
        var edited = project with { Plans = plans };
        var issues = ProjectValidator.Validate(edited);
        if (issues.Count > 0)
            throw new ProjectValidationException(issues);
        return edited;
    }
}
