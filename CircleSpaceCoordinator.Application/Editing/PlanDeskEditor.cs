namespace CircleSpaceCoordinator.Application.Editing;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;

public static class PlanDeskEditor
{
    public static CircleSpaceProject AddDesk(
        CircleSpaceProject project,
        string planId,
        DeskPlacement placement, DeskType? type = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        ArgumentNullException.ThrowIfNull(placement);
        if (type is not null)
        {
            if (type.Id != placement.DeskTypeId) throw new InvalidOperationException("Space type does not match placement.");
            var existing = project.DeskTypes.SingleOrDefault(t => t.Id == type.Id);
            if (existing is null) project = project with { DeskTypes = [.. project.DeskTypes, type] };
            else if (System.Text.Json.JsonSerializer.Serialize(existing) != System.Text.Json.JsonSerializer.Serialize(type))
                throw new InvalidOperationException("Existing space type cannot be overwritten.");
        }

        var planIndex = FindUniqueIndex(project.Plans, planId, plan => plan.Id, "plan");
        var plan = project.Plans[planIndex];
        if (plan.DeskPlacements.Any(item => item.Id == placement.Id))
            throw new InvalidOperationException($"Desk placement ID '{placement.Id}' already exists in plan '{planId}'.");

        return ReplacePlan(project, planIndex, plan with
        {
            DeskPlacements = [.. plan.DeskPlacements, placement],
        });
    }

    public static CircleSpaceProject RemoveDesk(
        CircleSpaceProject project,
        string planId,
        string deskPlacementId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deskPlacementId);

        var planIndex = FindUniqueIndex(project.Plans, planId, plan => plan.Id, "plan");
        var plan = project.Plans[planIndex];
        var placementIndex = FindUniqueIndex(
            plan.DeskPlacements,
            deskPlacementId,
            placement => placement.Id,
            "desk placement");
        return ReplacePlan(project, planIndex, plan with
        {
            DeskPlacements = plan.DeskPlacements.Where((_, index) => index != placementIndex).ToArray(),
            SeatLabels = plan.SeatLabels.Where(item => item.DeskPlacementId != deskPlacementId).ToArray(),
        });
    }

    public static CircleSpaceProject MoveDesk(
        CircleSpaceProject project,
        string planId,
        string deskPlacementId,
        GridPosition newAnchor) =>
        TransformDesk(project, planId, deskPlacementId, newAnchor, orientation: null);

    public static CircleSpaceProject RotateDesk(
        CircleSpaceProject project,
        string planId,
        string deskPlacementId,
        QuarterTurn newOrientation) =>
        TransformDesk(project, planId, deskPlacementId, anchor: null, newOrientation);

    public static CircleSpaceProject SetDeskNumber(
        CircleSpaceProject project,
        string planId,
        string deskPlacementId,
        string? deskNumber)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deskPlacementId);

        var planIndex = FindUniqueIndex(project.Plans, planId, plan => plan.Id, "plan");
        var plan = project.Plans[planIndex];
        var placementIndex = FindUniqueIndex(plan.DeskPlacements, deskPlacementId, placement => placement.Id, "desk placement");
        var placements = plan.DeskPlacements.ToArray();
        placements[placementIndex] = placements[placementIndex] with
        {
            DeskNumber = string.IsNullOrWhiteSpace(deskNumber) ? null : deskNumber.Trim(),
        };
        return ReplacePlan(project, planIndex, plan with { DeskPlacements = placements });
    }

    private static CircleSpaceProject TransformDesk(
        CircleSpaceProject project,
        string planId,
        string deskPlacementId,
        GridPosition? anchor,
        QuarterTurn? orientation)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deskPlacementId);

        var planIndex = FindUniqueIndex(project.Plans, planId, plan => plan.Id, "plan");
        var plan = project.Plans[planIndex];
        var placementIndex = FindUniqueIndex(
            plan.DeskPlacements,
            deskPlacementId,
            placement => placement.Id,
            "desk placement");
        var placement = plan.DeskPlacements[placementIndex];
        var deskType = project.DeskTypes.SingleOrDefault(type => type.Id == placement.DeskTypeId)
            ?? throw new InvalidOperationException($"Desk type '{placement.DeskTypeId}' does not exist.");
        var replacement = placement with
        {
            Anchor = anchor ?? placement.Anchor,
            Orientation = orientation ?? placement.Orientation,
        };

        var cellMap = deskType.Footprint.ToDictionary(
            relative => placement.Anchor + relative.Rotate(placement.Orientation),
            relative => replacement.Anchor + relative.Rotate(replacement.Orientation));
        var oldDeskCells = cellMap.Keys.ToHashSet();

        var assignments = plan.Assignments.Select(assignment =>
        {
            var cellsOnDesk = assignment.OccupiedCells.Count(oldDeskCells.Contains);
            if (cellsOnDesk == 0)
                return assignment;
            if (cellsOnDesk != assignment.OccupiedCells.Count)
            {
                throw new InvalidOperationException(
                    $"Participant '{assignment.ParticipantId}' is only partially assigned to desk '{deskPlacementId}'.");
            }

            return assignment with
            {
                OccupiedCells = assignment.OccupiedCells.Select(cell => cellMap[cell]).ToHashSet(),
                ScoringPosition = cellMap[assignment.ScoringPosition],
            };
        }).ToArray();

        var placements = plan.DeskPlacements.ToArray();
        placements[placementIndex] = replacement;
        return ReplacePlan(project, planIndex, plan with
        {
            DeskPlacements = placements,
            Assignments = assignments,
        });
    }

    private static CircleSpaceProject ReplacePlan(CircleSpaceProject project, int planIndex, Plan replacement)
    {
        var plans = project.Plans.ToArray();
        plans[planIndex] = replacement;
        var editedProject = project with { Plans = plans };
        var issues = ProjectValidator.Validate(editedProject);
        if (issues.Count > 0)
            throw new ProjectValidationException(issues);
        return editedProject;
    }

    private static int FindUniqueIndex<T>(
        IReadOnlyList<T> items,
        string id,
        Func<T, string> getId,
        string itemName)
    {
        var matches = items
            .Select((item, index) => (Item: item, Index: index))
            .Where(pair => getId(pair.Item) == id)
            .Select(pair => pair.Index)
            .ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new KeyNotFoundException($"The {itemName} '{id}' does not exist."),
            _ => throw new InvalidOperationException($"The {itemName} ID '{id}' is duplicated."),
        };
    }
}
