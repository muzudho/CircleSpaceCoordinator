namespace CircleSpaceCoordinator.Application.Editing;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;

public static class DeskLayoutService
{
    public static CircleSpaceProject FillAvailableCells(
        CircleSpaceProject project,
        string planId,
        string deskTypeId,
        string idPrefix = "auto-desk")
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deskTypeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idPrefix);

        var planIndex = project.Plans.ToList().FindIndex(plan => plan.Id == planId);
        if (planIndex < 0)
            throw new KeyNotFoundException($"Plan '{planId}' does not exist.");
        var deskType = project.DeskTypes.SingleOrDefault(type => type.Id == deskTypeId)
            ?? throw new KeyNotFoundException($"Desk type '{deskTypeId}' does not exist.");
        var plan = project.Plans[planIndex];
        var placements = plan.DeskPlacements.ToList();
        var occupied = placements.SelectMany(placement =>
        {
            var type = project.DeskTypes.Single(item => item.Id == placement.DeskTypeId);
            return placement.GetOccupiedCells(type);
        }).ToHashSet();
        var usedIds = placements.Select(placement => placement.Id).ToHashSet(StringComparer.Ordinal);
        var nextId = 1;

        for (var y = 0; y < project.Venue.Height; y++)
        {
            for (var x = 0; x < project.Venue.Width; x++)
            {
                var anchor = new GridPosition(x, y);
                var candidate = new DeskPlacement("candidate", deskType.Id, anchor, QuarterTurn.North);
                var cells = candidate.GetOccupiedCells(deskType);
                if (cells.Any(cell => !project.Venue.CanPlaceAt(cell) || occupied.Contains(cell)))
                    continue;

                string id;
                do
                {
                    id = $"{idPrefix}-{nextId++:0000}";
                }
                while (!usedIds.Add(id));

                placements.Add(candidate with { Id = id });
                occupied.UnionWith(cells);
            }
        }

        var plans = project.Plans.ToArray();
        plans[planIndex] = plan with { DeskPlacements = placements };
        var edited = project with { Plans = plans };
        var issues = ProjectValidator.Validate(edited);
        if (issues.Count > 0)
            throw new ProjectValidationException(issues);
        return edited;
    }
}
