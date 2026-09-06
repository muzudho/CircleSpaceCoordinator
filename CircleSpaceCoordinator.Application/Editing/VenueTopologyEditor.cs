namespace CircleSpaceCoordinator.Application.Editing;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;

public static class VenueTopologyEditor
{
    public static CircleSpaceProject AddConnector(
        CircleSpaceProject project, string planId, string firstDeskId, string secondDeskId,
        GridPosition? firstCell = null, GridPosition? secondCell = null)
    {
        if (firstDeskId == secondDeskId)
            return project;
        var plan = project.Plans.Single(item => item.Id == planId);
        if (!plan.DeskPlacements.Any(item => item.Id == firstDeskId) || !plan.DeskPlacements.Any(item => item.Id == secondDeskId))
            return project;
        if (plan.IslandConnectors.Any(item =>
                item.FirstDeskId == firstDeskId && item.SecondDeskId == secondDeskId ||
                item.FirstDeskId == secondDeskId && item.SecondDeskId == firstDeskId))
            return project;
        var deskTypes = project.DeskTypes.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var firstDesk = plan.DeskPlacements.Single(item => item.Id == firstDeskId);
        var secondDesk = plan.DeskPlacements.Single(item => item.Id == secondDeskId);
        if (firstCell is { } first && !firstDesk.GetOccupiedCells(deskTypes[firstDesk.DeskTypeId]).Contains(first) ||
            secondCell is { } second && !secondDesk.GetOccupiedCells(deskTypes[secondDesk.DeskTypeId]).Contains(second))
            return project;
        var connector = new IslandConnector(NewId("island-link", plan.IslandConnectors.Select(item => item.Id)), firstDeskId, secondDeskId, firstCell, secondCell);
        return Replace(project, planId, plan with { IslandConnectors = [.. plan.IslandConnectors, connector] });
    }

    public static CircleSpaceProject AddFacingRegion(CircleSpaceProject project, string planId, GridPosition first, GridPosition second)
    {
        var plan = project.Plans.Single(item => item.Id == planId);
        var region = new FacingRegion(NewId("facing", plan.FacingRegions.Select(item => item.Id)), first, second);
        return Replace(project, planId, plan with { FacingRegions = [.. plan.FacingRegions, region] });
    }

    public static CircleSpaceProject ToggleAutomaticConnection(CircleSpaceProject project, string planId, GridPosition first, GridPosition second)
    {
        var plan = project.Plans.Single(item => item.Id == planId);
        var deskTypes = project.DeskTypes.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var normalized = Normalize(first, second);
        if (!VenueTopologyAnalyzer.GetAutomaticCellEdges(plan, deskTypes).Contains(normalized))
            return project;

        var disabled = plan.DisabledIslandConnections
            .Where(item => Normalize(item.FirstCell, item.SecondCell) != normalized)
            .ToArray();
        if (disabled.Length == plan.DisabledIslandConnections.Count)
            disabled = [.. disabled, new DisabledIslandConnection(normalized.FirstCell, normalized.SecondCell)];
        return Replace(project, planId, plan with { DisabledIslandConnections = disabled });
    }

    public static CircleSpaceProject RemoveAt(CircleSpaceProject project, string planId, GridPosition cell, string? deskId)
    {
        var plan = project.Plans.Single(item => item.Id == planId);
        var connectors = plan.IslandConnectors.Where(item => deskId is null || item.FirstDeskId != deskId && item.SecondDeskId != deskId).ToArray();
        var regions = plan.FacingRegions.Where(item => !Contains(item, cell)).ToArray();
        if (connectors.Length == plan.IslandConnectors.Count && regions.Length == plan.FacingRegions.Count)
            return project;
        return Replace(project, planId, plan with { IslandConnectors = connectors, FacingRegions = regions });
    }

    public static CircleSpaceProject RemoveConnector(CircleSpaceProject project, string planId, string connectorId)
    {
        var plan = project.Plans.Single(item => item.Id == planId);
        var connectors = plan.IslandConnectors.Where(item => item.Id != connectorId).ToArray();
        return connectors.Length == plan.IslandConnectors.Count
            ? project
            : Replace(project, planId, plan with { IslandConnectors = connectors });
    }

    private static bool Contains(FacingRegion region, GridPosition cell) =>
        cell.X >= Math.Min(region.FirstCorner.X, region.SecondCorner.X) && cell.X <= Math.Max(region.FirstCorner.X, region.SecondCorner.X) &&
            cell.Y >= Math.Min(region.FirstCorner.Y, region.SecondCorner.Y) && cell.Y <= Math.Max(region.FirstCorner.Y, region.SecondCorner.Y);

    private static (GridPosition FirstCell, GridPosition SecondCell) Normalize(GridPosition first, GridPosition second) =>
        first.Y < second.Y || first.Y == second.Y && first.X <= second.X ? (first, second) : (second, first);

    private static string NewId(string prefix, IEnumerable<string> existing)
    {
        var used = existing.ToHashSet(StringComparer.Ordinal);
        var number = 1;
        while (used.Contains($"{prefix}-{number:0000}")) number++;
        return $"{prefix}-{number:0000}";
    }

    private static CircleSpaceProject Replace(CircleSpaceProject project, string planId, Plan edited)
    {
        var plans = project.Plans.Select(item => item.Id == planId ? edited : item).ToArray();
        var result = project with { Plans = plans };
        var issues = ProjectValidator.Validate(result);
        if (issues.Count > 0) throw new ProjectValidationException(issues);
        return result;
    }
}
