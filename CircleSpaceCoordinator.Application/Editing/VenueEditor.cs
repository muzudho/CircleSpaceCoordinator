namespace CircleSpaceCoordinator.Application.Editing;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;

public static class VenueEditor
{
    public static CircleSpaceProject AddPillar(CircleSpaceProject project, GridPosition cell)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.Venue.Contains(cell) || project.Venue.BlockedCells.Contains(cell))
            return project;
        return ReplaceVenue(project, project.Venue with
        {
            BlockedCells = project.Venue.BlockedCells.Append(cell).ToHashSet(),
        });
    }

    public static CircleSpaceProject RemovePillar(CircleSpaceProject project, GridPosition cell)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.Venue.BlockedCells.Contains(cell))
            return project;
        return ReplaceVenue(project, project.Venue with
        {
            BlockedCells = project.Venue.BlockedCells.Where(item => item != cell).ToHashSet(),
        });
    }

    public static CircleSpaceProject Resize(CircleSpaceProject project, int width, int height, int offsetX = 0, int offsetY = 0)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Venue width must be positive.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Venue height must be positive.");

        if (offsetX == 0 && offsetY == 0)
            return ReplaceVenue(project, project.Venue with { Width = width, Height = height });

        var offset = new GridPosition(offsetX, offsetY);
        ParticipantAssignment ShiftAssignment(ParticipantAssignment item) => item with
        {
            OccupiedCells = item.OccupiedCells.Select(cell => cell + offset).ToHashSet(),
            ScoringPosition = item.ScoringPosition + offset,
        };
        IslandConnector ShiftConnector(IslandConnector item) => item with
        {
            FirstCell = item.FirstCell is { } first ? first + offset : null,
            SecondCell = item.SecondCell is { } second ? second + offset : null,
        };
        DisabledIslandConnection ShiftDisabled(DisabledIslandConnection item) => item with
        { FirstCell = item.FirstCell + offset, SecondCell = item.SecondCell + offset };
        FacingRegion ShiftRegion(FacingRegion item) => item with
        { FirstCorner = item.FirstCorner + offset, SecondCorner = item.SecondCorner + offset };
        DeskPlacement ShiftDesk(DeskPlacement item) => item with { Anchor = item.Anchor + offset };
        var shifted = project with
        {
            Evaluation = project.Evaluation with
            {
                WeightMaps = project.Evaluation.WeightMaps.Select(map => map with
                { Cells = map.Cells.ToDictionary(item => item.Key + offset, item => item.Value) }).ToArray(),
            },
            Plans = project.Plans.Select(plan => plan with
            {
                DeskPlacements = plan.DeskPlacements.Select(ShiftDesk).ToArray(),
                Assignments = plan.Assignments.Select(ShiftAssignment).ToArray(),
                TemporaryPlacements = plan.TemporaryPlacements.Select(ShiftAssignment).ToArray(),
                IslandConnectors = plan.IslandConnectors.Select(ShiftConnector).ToArray(),
                DisabledIslandConnections = plan.DisabledIslandConnections.Select(ShiftDisabled).ToArray(),
                FacingRegions = plan.FacingRegions.Select(ShiftRegion).ToArray(),
            }).ToArray(),
            DeskLayouts = project.DeskLayouts.Select(layout => layout with
            {
                DeskPlacements = layout.DeskPlacements.Select(ShiftDesk).ToArray(),
                IslandConnectors = layout.IslandConnectors.Select(ShiftConnector).ToArray(),
                DisabledIslandConnections = layout.DisabledIslandConnections.Select(ShiftDisabled).ToArray(),
                FacingRegions = layout.FacingRegions.Select(ShiftRegion).ToArray(),
            }).ToArray(),
            CircleLayouts = project.CircleLayouts.Select(layout => layout with
            {
                Assignments = layout.Assignments.Select(ShiftAssignment).ToArray(),
                TemporaryPlacements = layout.TemporaryPlacements.Select(ShiftAssignment).ToArray(),
            }).ToArray(),
        };
        return ReplaceVenue(shifted, project.Venue with
        {
            Width = width, Height = height,
            BlockedCells = project.Venue.BlockedCells.Select(cell => cell + offset).ToHashSet(),
            Zones = project.Venue.Zones.Select(zone => zone with { Cells = zone.Cells.Select(cell => cell + offset).ToHashSet() }).ToArray(),
        });
    }

    private static CircleSpaceProject ReplaceVenue(CircleSpaceProject project, Venue venue)
    {
        var edited = project with { Venue = venue };
        var issues = ProjectValidator.Validate(edited);
        if (issues.Count > 0)
            throw new ProjectValidationException(issues);
        return edited;
    }
}
