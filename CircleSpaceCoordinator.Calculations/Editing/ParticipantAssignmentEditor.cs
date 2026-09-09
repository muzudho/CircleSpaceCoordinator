namespace CircleSpaceCoordinator.Application.Editing;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;

public static class ParticipantAssignmentEditor
{
    public static CircleSpaceProject SwapCellRegions(
        CircleSpaceProject project,
        string planId,
        GridPosition sourceTopLeft,
        GridPosition destinationTopLeft,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        var source = Cells(sourceTopLeft, width, height).ToHashSet();
        var destination = Cells(destinationTopLeft, width, height).ToHashSet();
        if (source.Overlaps(destination))
            throw Invalid("rangeSwap.overlap", "The source and destination ranges overlap.");
        if (!source.Concat(destination).All(project.Venue.CanPlaceAt))
            throw Invalid("rangeSwap.destination.invalid", "The destination range must consist of venue desk cells.");

        var planIndex = FindPlanIndex(project, planId);
        var plan = project.Plans[planIndex];
        foreach (var assignment in plan.Assignments)
        {
            if (assignment.OccupiedCells.Overlaps(source) && !source.IsSupersetOf(assignment.OccupiedCells))
                throw Invalid("rangeSwap.source.protrudes", "A circle extends beyond the selected source range.");
            if (assignment.OccupiedCells.Overlaps(destination) && !destination.IsSupersetOf(assignment.OccupiedCells))
                throw Invalid("rangeSwap.destination.protrudes", "A circle extends beyond the destination range.");
        }

        var delta = new GridPosition(destinationTopLeft.X - sourceTopLeft.X, destinationTopLeft.Y - sourceTopLeft.Y);
        var assignments = plan.Assignments.Select(assignment =>
        {
            var shift = source.IsSupersetOf(assignment.OccupiedCells) ? delta :
                destination.IsSupersetOf(assignment.OccupiedCells) ? new GridPosition(-delta.X, -delta.Y) : default;
            return shift == default ? assignment : assignment with
            {
                OccupiedCells = assignment.OccupiedCells.Select(cell => cell + shift).ToHashSet(),
                ScoringPosition = assignment.ScoringPosition + shift,
            };
        }).ToArray();
        var deskTypes = project.DeskTypes.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var deskCells = plan.DeskPlacements.SelectMany(placement =>
            placement.GetOccupiedCells(deskTypes[placement.DeskTypeId])).ToHashSet();
        if (assignments.Any(assignment => !assignment.OccupiedCells.All(deskCells.Contains)))
            throw Invalid("rangeSwap.destination.noDesk", "The swap would place a circle on a non-desk cell.");
        return ReplacePlan(project, planIndex, plan with { Assignments = assignments });

        static IEnumerable<GridPosition> Cells(GridPosition topLeft, int width, int height) =>
            Enumerable.Range(0, height).SelectMany(y => Enumerable.Range(0, width)
                .Select(x => new GridPosition(topLeft.X + x, topLeft.Y + y)));

        static ProjectValidationException Invalid(string code, string message) => new([
            new ValidationIssue(code, "plans.assignments", message),
        ]);
    }

    public static CircleSpaceProject Assign(
        CircleSpaceProject project,
        string planId,
        string participantId,
        IReadOnlySet<GridPosition> occupiedCells,
        GridPosition scoringPosition,
        string? combinedSpaceId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        ArgumentException.ThrowIfNullOrWhiteSpace(participantId);
        ArgumentNullException.ThrowIfNull(occupiedCells);

        var planIndex = FindPlanIndex(project, planId);
        var plan = project.Plans[planIndex];
        if (plan.Assignments.Any(assignment => assignment.ParticipantId == participantId))
            throw new InvalidOperationException($"Participant '{participantId}' is already assigned in plan '{planId}'.");

        var assignment = new ParticipantAssignment(
            participantId,
            occupiedCells.ToHashSet(),
            scoringPosition)
        {
            CombinedSpaceId = combinedSpaceId,
        };
        return ReplacePlan(project, planIndex, plan with
        {
            Assignments = [.. plan.Assignments, assignment],
        });
    }

    public static CircleSpaceProject Reassign(
        CircleSpaceProject project,
        string planId,
        string participantId,
        IReadOnlySet<GridPosition> occupiedCells,
        GridPosition scoringPosition,
        string? combinedSpaceId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(participantId);
        ArgumentNullException.ThrowIfNull(occupiedCells);

        var planIndex = FindPlanIndex(project, planId);
        var plan = project.Plans[planIndex];
        var assignmentIndex = FindAssignmentIndex(plan, participantId);
        var assignments = plan.Assignments.ToArray();
        assignments[assignmentIndex] = new ParticipantAssignment(
            participantId,
            occupiedCells.ToHashSet(),
            scoringPosition)
        {
            CombinedSpaceId = combinedSpaceId,
        };
        return ReplacePlan(project, planIndex, plan with { Assignments = assignments });
    }

    public static CircleSpaceProject Unassign(
        CircleSpaceProject project,
        string planId,
        string participantId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(participantId);

        var planIndex = FindPlanIndex(project, planId);
        var plan = project.Plans[planIndex];
        var assignmentIndex = FindAssignmentIndex(plan, participantId);
        var assignments = plan.Assignments.Where((_, index) => index != assignmentIndex).ToArray();
        return ReplacePlan(project, planIndex, plan with { Assignments = assignments });
    }

    public static CircleSpaceProject Swap(
        CircleSpaceProject project,
        string planId,
        string firstParticipantId,
        string secondParticipantId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstParticipantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secondParticipantId);
        if (firstParticipantId == secondParticipantId)
            return project;

        var planIndex = FindPlanIndex(project, planId);
        var plan = project.Plans[planIndex];
        var firstIndex = FindAssignmentIndex(plan, firstParticipantId);
        var secondIndex = FindAssignmentIndex(plan, secondParticipantId);
        var first = plan.Assignments[firstIndex];
        var second = plan.Assignments[secondIndex];
        var assignments = plan.Assignments.ToArray();
        assignments[firstIndex] = first with
        {
            OccupiedCells = second.OccupiedCells.ToHashSet(),
            ScoringPosition = second.ScoringPosition,
        };
        assignments[secondIndex] = second with
        {
            OccupiedCells = first.OccupiedCells.ToHashSet(),
            ScoringPosition = first.ScoringPosition,
        };
        return ReplacePlan(project, planIndex, plan with { Assignments = assignments });
    }

    public static CircleSpaceProject SwapGroups(
        CircleSpaceProject project,
        string planId,
        IReadOnlyCollection<string> firstParticipantIds,
        IReadOnlyCollection<string> secondParticipantIds)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(firstParticipantIds);
        ArgumentNullException.ThrowIfNull(secondParticipantIds);
        if (firstParticipantIds.Count == 0 || secondParticipantIds.Count == 0)
            return project;
        if (firstParticipantIds.Intersect(secondParticipantIds, StringComparer.Ordinal).Any())
            return project;

        var planIndex = FindPlanIndex(project, planId);
        var plan = project.Plans[planIndex];
        var firstIds = firstParticipantIds.ToHashSet(StringComparer.Ordinal);
        var secondIds = secondParticipantIds.ToHashSet(StringComparer.Ordinal);
        var first = plan.Assignments.Where(item => firstIds.Contains(item.ParticipantId)).ToArray();
        var second = plan.Assignments.Where(item => secondIds.Contains(item.ParticipantId)).ToArray();
        if (first.Length != firstIds.Count || second.Length != secondIds.Count)
            throw new KeyNotFoundException("Every participant in a group must be assigned before the groups can be swapped.");

        var firstCells = OrderCells(first.SelectMany(item => item.OccupiedCells));
        var secondCells = OrderCells(second.SelectMany(item => item.OccupiedCells));
        if (firstCells.Length != secondCells.Length)
            throw new InvalidOperationException("Participant groups must occupy the same number of cells before they can be swapped.");

        var replacements = Allocate(first, secondCells)
            .Concat(Allocate(second, firstCells))
            .ToDictionary(item => item.ParticipantId, StringComparer.Ordinal);
        var assignments = plan.Assignments
            .Select(item => replacements.GetValueOrDefault(item.ParticipantId) ?? item)
            .ToArray();
        return ReplacePlan(project, planIndex, plan with { Assignments = assignments });

        static IEnumerable<ParticipantAssignment> Allocate(
            IEnumerable<ParticipantAssignment> group,
            IReadOnlyList<GridPosition> destinationCells)
        {
            var offset = 0;
            foreach (var assignment in group.OrderBy(item => item.ScoringPosition.Y).ThenBy(item => item.ScoringPosition.X))
            {
                var cells = destinationCells.Skip(offset).Take(assignment.OccupiedCells.Count).ToHashSet();
                offset += cells.Count;
                yield return assignment with
                {
                    OccupiedCells = cells,
                    ScoringPosition = cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).First(),
                };
            }
        }

        static GridPosition[] OrderCells(IEnumerable<GridPosition> cells) =>
            cells.Distinct().OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
    }

    private static int FindPlanIndex(CircleSpaceProject project, string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        var index = project.Plans.ToList().FindIndex(plan => plan.Id == planId);
        return index >= 0
            ? index
            : throw new KeyNotFoundException($"Plan '{planId}' does not exist.");
    }

    private static int FindAssignmentIndex(Plan plan, string participantId)
    {
        var index = plan.Assignments.ToList().FindIndex(assignment => assignment.ParticipantId == participantId);
        return index >= 0
            ? index
            : throw new KeyNotFoundException($"Participant '{participantId}' is not assigned in plan '{plan.Id}'.");
    }

    private static CircleSpaceProject ReplacePlan(CircleSpaceProject project, int planIndex, Plan plan)
    {
        var plans = project.Plans.ToArray();
        plans[planIndex] = plan with
        {
            TemporaryPlacements = plan.TemporaryPlacements
                .Where(item => plan.Assignments.All(assignment => assignment.ParticipantId != item.ParticipantId)).ToArray(),
        };
        var editedProject = project with { Plans = plans };
        var issues = ProjectValidator.Validate(editedProject);
        if (issues.Count > 0)
            throw new ProjectValidationException(issues);
        return editedProject;
    }
}
