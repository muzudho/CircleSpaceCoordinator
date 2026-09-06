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

public static class PlanCatalogService
{
    public static CircleSpaceProject CreatePlan(
        CircleSpaceProject project,
        string newPlanId,
        string newPlanName,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPlanId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPlanName);
        EnsurePlanIdIsAvailable(project, newPlanId);

        var plan = new Plan(newPlanId, newPlanName, [], [])
        {
            Description = description,
        };
        if (project.DeskLayouts.Count != 0 || project.CircleLayouts.Count != 0)
        {
            var deskId = $"desk-{newPlanId}";
            while (project.DeskLayouts.Any(desk => desk.Id == deskId)) deskId += ":";
            project = project with
            {
                DeskLayouts = [.. project.DeskLayouts, new DeskLayout(deskId, newPlanName, []) { Description = description }],
                CircleLayouts = [.. project.CircleLayouts, new CircleLayout(newPlanId, newPlanName, deskId, []) { Description = description }],
            };
        }
        return EnsureValid(project with { Plans = [.. project.Plans, plan] });
    }

    public static CircleSpaceProject RenamePlan(
        CircleSpaceProject project,
        string planId,
        string newPlanName)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPlanName);

        var planIndex = FindPlanIndex(project, planId);
        var plans = project.Plans.ToArray();
        plans[planIndex] = plans[planIndex] with { Name = newPlanName };
        return EnsureValid(project with { Plans = plans });
    }

    public static CircleSpaceProject RemovePlan(CircleSpaceProject project, string planId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        if (project.Plans.Count == 1)
            throw new InvalidOperationException("The last plan cannot be removed.");

        var planIndex = FindPlanIndex(project, planId);
        return EnsureValid(project with
        {
            Plans = project.Plans.Where((_, index) => index != planIndex).ToArray(),
            CircleLayouts = project.CircleLayouts.Where(circle => circle.Id != planId).ToArray(),
        });
    }

    public static CircleSpaceProject DuplicatePlan(
        CircleSpaceProject project,
        string sourcePlanId,
        string newPlanId,
        string newPlanName)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePlanId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPlanId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPlanName);

        EnsurePlanIdIsAvailable(project, newPlanId);

        var source = project.Plans.SingleOrDefault(plan => plan.Id == sourcePlanId)
            ?? throw new KeyNotFoundException($"Plan '{sourcePlanId}' does not exist.");
        var duplicate = source with
        {
            Id = newPlanId,
            Name = newPlanName,
            TemporaryPlacements = source.TemporaryPlacements.Select(item => item with { OccupiedCells = item.OccupiedCells.ToHashSet() }).ToArray(),
            DeskPlacements = source.DeskPlacements.Select(placement => placement with { }).ToArray(),
            Assignments = source.Assignments.Select(assignment => assignment with
            {
                OccupiedCells = assignment.OccupiedCells.ToHashSet(),
            }).ToArray(),
            IslandConnectors = source.IslandConnectors.Select(item => item with { }).ToArray(),
            DisabledIslandConnections = source.DisabledIslandConnections.Select(item => item with { }).ToArray(),
            FacingRegions = source.FacingRegions.Select(item => item with { }).ToArray(),
            SeatLabels = source.SeatLabels.Select(item => item with { }).ToArray(),
        };
        return AppendCirclePlan(project, sourcePlanId, duplicate);
    }

    public static CircleSpaceProject AddOptimizedPlan(
        CircleSpaceProject project,
        Plan optimizedPlan,
        string newPlanId,
        string newPlanName)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(optimizedPlan);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPlanId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPlanName);
        EnsurePlanIdIsAvailable(project, newPlanId);
        var copy = optimizedPlan with
        {
            Id = newPlanId,
            Name = newPlanName.Trim(),
            DeskPlacements = optimizedPlan.DeskPlacements.Select(item => item with { }).ToArray(),
            Assignments = optimizedPlan.Assignments.Select(item => item with { OccupiedCells = item.OccupiedCells.ToHashSet() }).ToArray(),
            IslandConnectors = optimizedPlan.IslandConnectors.Select(item => item with { }).ToArray(),
            DisabledIslandConnections = optimizedPlan.DisabledIslandConnections.Select(item => item with { }).ToArray(),
            FacingRegions = optimizedPlan.FacingRegions.Select(item => item with { }).ToArray(),
            SeatLabels = optimizedPlan.SeatLabels.Select(item => item with { }).ToArray(),
        };
        return AppendCirclePlan(project, optimizedPlan.Id, copy);
    }

    private static CircleSpaceProject AppendCirclePlan(CircleSpaceProject project, string sourcePlanId, Plan copy)
    {
        // Legacy projects still use Plans alone. In a separated project every
        // selectable plan must also have a circle layout and its desk binding.
        if (project.DeskLayouts.Count != 0 || project.CircleLayouts.Count != 0)
        {
            var source = project.CircleLayouts.SingleOrDefault(circle => circle.Id == sourcePlanId)
                ?? throw new KeyNotFoundException($"Circle layout '{sourcePlanId}' does not exist.");
            if (project.CircleLayouts.Any(circle => circle.Id == copy.Id))
                throw new InvalidOperationException($"Circle layout ID '{copy.Id}' already exists.");
            project = project with
            {
                CircleLayouts = [.. project.CircleLayouts, new CircleLayout(copy.Id, copy.Name, source.DeskLayoutId, copy.Assignments)
                {
                    Description = copy.Description,
                    TemporaryPlacements = copy.TemporaryPlacements,
                }],
            };
        }
        return EnsureValid(project with { Plans = [.. project.Plans, copy] });
    }

    public static CircleSpaceProject CopyDeskLayout(
        CircleSpaceProject project,
        string sourcePlanId,
        string destinationPlanId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePlanId);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPlanId);
        if (string.Equals(sourcePlanId, destinationPlanId, StringComparison.Ordinal))
            throw new ArgumentException("The copy source and destination must differ.", nameof(destinationPlanId));

        var sourceIndex = FindPlanIndex(project, sourcePlanId);
        var destinationIndex = FindPlanIndex(project, destinationPlanId);
        var source = project.Plans[sourceIndex];
        var plans = project.Plans.ToArray();
        plans[destinationIndex] = plans[destinationIndex] with
        {
            DeskPlacements = source.DeskPlacements.Select(item => item with { }).ToArray(),
            SeatLabels = source.SeatLabels.Select(item => item with { }).ToArray(),
            IslandConnectors = source.IslandConnectors.Select(item => item with { }).ToArray(),
            DisabledIslandConnections = source.DisabledIslandConnections.Select(item => item with { }).ToArray(),
            FacingRegions = source.FacingRegions.Select(item => item with { }).ToArray(),
        };
        return EnsureValid(project with { Plans = plans });
    }

    public static IReadOnlyList<RankedPlan> RankPlans(CircleSpaceProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var plansById = project.Plans.ToDictionary(plan => plan.Id);
        var audienceResults = GeneralAttendeeEvaluator.Evaluate(project).ToDictionary(item => item.PlanId);
        return ProjectEvaluator.Evaluate(project)
            .Select((result, originalIndex) => (Result: result, Audience: audienceResults[result.PlanId], OriginalIndex: originalIndex))
            .OrderByDescending(item => item.Audience.GeneralAttendeeScore)
            .ThenByDescending(item => item.Audience.CircleParticipantScore)
            .ThenBy(item => item.OriginalIndex)
            .Select((item, sortedIndex) =>
            {
                var plan = plansById[item.Result.PlanId];
                return new RankedPlan(
                    sortedIndex + 1,
                    plan.Id,
                    plan.Name,
                    item.Result.TotalScore,
                    item.Audience.GeneralAttendeeScore,
                    item.Audience.CircleParticipantScore,
                    item.Audience.CombinedSpaceRequirementsSatisfied,
                    item.Result.Features);
            })
            .ToArray();
    }

    private static int FindPlanIndex(CircleSpaceProject project, string planId)
    {
        var index = project.Plans.ToList().FindIndex(plan => plan.Id == planId);
        return index >= 0
            ? index
            : throw new KeyNotFoundException($"Plan '{planId}' does not exist.");
    }

    private static void EnsurePlanIdIsAvailable(CircleSpaceProject project, string planId)
    {
        if (project.Plans.Any(plan => plan.Id == planId))
            throw new InvalidOperationException($"Plan ID '{planId}' already exists.");
    }

    private static CircleSpaceProject EnsureValid(CircleSpaceProject project)
    {
        var issues = ProjectValidator.Validate(project);
        if (issues.Count > 0)
            throw new ProjectValidationException(issues);
        return project;
    }
}
