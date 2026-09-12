namespace CircleSpaceCoordinator.Application.Editing;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;

public static class TemporaryPlacementEditor
{
    public static CircleSpaceProject Park(CircleSpaceProject project, string planId, string participantId,
        IReadOnlySet<GridPosition> cells, GridPosition position)
    {
        var plan = project.Plans.Single(item => item.Id == planId);
        var existing = All(plan).SingleOrDefault(item => item.ParticipantId == participantId);
        var placed = new ParticipantAssignment(participantId, cells.ToHashSet(), position)
        {
            CombinedSpaceId = existing?.CombinedSpaceId,
        };
        return Replace(project, plan, All(plan).Where(item => item.ParticipantId != participantId).Append(placed));
    }

    public static CircleSpaceProject Swap(CircleSpaceProject project, string planId, string firstId, string secondId)
    {
        var plan = project.Plans.Single(item => item.Id == planId);
        var all = All(plan).ToArray();
        var first = all.Single(item => item.ParticipantId == firstId);
        var second = all.Single(item => item.ParticipantId == secondId);
        if (first.OccupiedCells.Count != second.OccupiedCells.Count)
            throw Invalid("temporary.swap.capacity", "仮置きとの入れ替えは同じセル数同士で行ってください。範囲選択なら複数のセルをまとめて交換できます。");
        return Replace(project, plan, all.Select(item => item.ParticipantId == firstId
            ? item with { OccupiedCells = second.OccupiedCells.ToHashSet(), ScoringPosition = second.ScoringPosition }
            : item.ParticipantId == secondId
                ? item with { OccupiedCells = first.OccupiedCells.ToHashSet(), ScoringPosition = first.ScoringPosition }
                : item));
    }

    public static CircleSpaceProject SwapRegions(CircleSpaceProject project, string planId,
        GridPosition source, GridPosition destination, int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        var from = Cells(source).ToHashSet();
        var to = Cells(destination).ToHashSet();
        if (from.Overlaps(to)) throw Invalid("rangeSwap.overlap", "交換する範囲が重なっています。");
        var plan = project.Plans.Single(item => item.Id == planId);
        var delta = new GridPosition(destination.X - source.X, destination.Y - source.Y);
        return Replace(project, plan, All(plan).Select(item =>
        {
            if ((item.OccupiedCells.Overlaps(from) && !from.IsSupersetOf(item.OccupiedCells)) ||
                (item.OccupiedCells.Overlaps(to) && !to.IsSupersetOf(item.OccupiedCells)))
                throw Invalid("rangeSwap.source.protrudes", "サークル全体が範囲に収まるように選択してください。");
            var shift = from.IsSupersetOf(item.OccupiedCells) ? delta :
                to.IsSupersetOf(item.OccupiedCells) ? new GridPosition(-delta.X, -delta.Y) : default;
            return item with
            {
                OccupiedCells = item.OccupiedCells.Select(cell => cell + shift).ToHashSet(),
                ScoringPosition = item.ScoringPosition + shift,
            };
        }));

        IEnumerable<GridPosition> Cells(GridPosition start) => Enumerable.Range(0, height)
            .SelectMany(y => Enumerable.Range(0, width).Select(x => new GridPosition(start.X + x, start.Y + y)));
    }

    private static IEnumerable<ParticipantAssignment> All(Plan plan) => plan.Assignments.Concat(plan.TemporaryPlacements);

    private static CircleSpaceProject Replace(CircleSpaceProject project, Plan plan, IEnumerable<ParticipantAssignment> placements)
    {
        var all = placements.ToArray();
        var types = project.DeskTypes.ToDictionary(item => item.Id);
        var desks = plan.DeskPlacements.Select(item => item.GetOccupiedCells(types[item.DeskTypeId])).ToArray();
        var deskCells = desks.SelectMany(cells => cells).ToHashSet();
        var assigned = new List<ParticipantAssignment>();
        var temporary = new List<ParticipantAssignment>();
        foreach (var item in all)
        {
            if (!item.OccupiedCells.Any(deskCells.Contains)) temporary.Add(item);
            else if (desks.Any(cells => cells.IsSupersetOf(item.OccupiedCells))) assigned.Add(item);
            else throw Invalid("temporary.destination.partialDesk", "机と通路にまたがらない位置に置いてください。");
        }
        var edited = plan with
        {
            Assignments = assigned.ToArray(),
            TemporaryPlacements = temporary.ToArray(),
        };
        var result = project with { Plans = project.Plans.Select(item => item.Id == plan.Id ? edited : item).ToArray() };
        var issues = ProjectValidator.Validate(result);
        if (issues.Count != 0) throw new ProjectValidationException(issues);
        return result;
    }

    private static ProjectValidationException Invalid(string code, string message) => new([new ValidationIssue(code, "temporaryPlacements", message)]);
}
