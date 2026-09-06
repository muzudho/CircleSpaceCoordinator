namespace CircleSpaceCoordinator.Core.Model;

/// <summary>Creates the legacy combined Plan view required by existing consumers.</summary>
public static class LayoutProjection
{
    public static IReadOnlyList<Plan> ToPlans(CircleSpaceProject project) =>
        project.DeskLayouts.Count == 0 && project.CircleLayouts.Count == 0
            ? project.Plans
            : project.CircleLayouts.Select(circle =>
            {
                var desk = project.DeskLayouts.Single(item => item.Id == circle.DeskLayoutId);
                return new Plan(circle.Id, circle.Name, desk.DeskPlacements, circle.Assignments)
                {
                    Description = circle.Description,
                    TemporaryPlacements = circle.TemporaryPlacements,
                    IslandConnectors = desk.IslandConnectors,
                    DisabledIslandConnections = desk.DisabledIslandConnections,
                    FacingRegions = desk.FacingRegions,
                    SeatLabels = desk.SeatLabels,
                };
            }).ToArray();

    public static CircleSpaceProject MigrateLegacyPlans(CircleSpaceProject project)
    {
        if (project.DeskLayouts.Count != 0 || project.CircleLayouts.Count != 0)
            return project;
        var desks = project.Plans.Select(plan => new DeskLayout($"desk-{plan.Id}", plan.Name, plan.DeskPlacements)
        {
            Description = plan.Description,
            IslandConnectors = plan.IslandConnectors,
            DisabledIslandConnections = plan.DisabledIslandConnections,
            FacingRegions = plan.FacingRegions,
            SeatLabels = plan.SeatLabels,
        }).ToArray();
        var circles = project.Plans.Select(plan => new CircleLayout(plan.Id, plan.Name, $"desk-{plan.Id}", plan.Assignments)
        {
            Description = plan.Description,
            TemporaryPlacements = plan.TemporaryPlacements,
        }).ToArray();
        return project with { DeskLayouts = desks, CircleLayouts = circles };
    }

    /// <summary>
    /// Compatibility bridge while UI commands still edit the combined Plan view.
    /// It copies those edits back to the separated records before persistence.
    /// A desk edited through any child circle plan is shared by every child.
    /// </summary>
    public static CircleSpaceProject CommitLegacyPlanEdits(CircleSpaceProject project, string? editedPlanId = null)
    {
        if (project.DeskLayouts.Count == 0 || project.CircleLayouts.Count == 0)
            return MigrateLegacyPlans(project);

        var plansById = project.Plans.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var circles = project.CircleLayouts.Select(circle =>
        {
            if (!plansById.TryGetValue(circle.Id, out var plan)) return circle;
            return circle with { Name = plan.Name, Description = plan.Description, Assignments = plan.Assignments, TemporaryPlacements = plan.TemporaryPlacements };
        }).ToArray();
        var desks = project.DeskLayouts.Select(desk =>
        {
            var plan = circles.Where(circle => circle.DeskLayoutId == desk.Id)
                .OrderByDescending(circle => circle.Id == editedPlanId)
                .Select(circle => plansById.GetValueOrDefault(circle.Id))
                .FirstOrDefault(candidate => candidate is not null);
            return plan is null ? desk : desk with
            {
                DeskPlacements = plan.DeskPlacements,
                IslandConnectors = plan.IslandConnectors,
                DisabledIslandConnections = plan.DisabledIslandConnections,
                FacingRegions = plan.FacingRegions,
                SeatLabels = plan.SeatLabels,
            };
        }).ToArray();
        var committed = project with { DeskLayouts = desks, CircleLayouts = circles };
        return committed with { Plans = ToPlans(committed) };
    }
}
