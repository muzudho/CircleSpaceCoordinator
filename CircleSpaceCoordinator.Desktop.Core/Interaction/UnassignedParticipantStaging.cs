namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;

/// <summary>Deterministic display positions for circles that have never been placed or parked.</summary>
public static class UnassignedParticipantStaging
{
    public static IReadOnlyList<ParticipantAssignment> Build(CircleSpaceProject project, Plan plan)
    {
        var positioned = plan.Assignments.Concat(plan.TemporaryPlacements).ToArray();
        var positionedIds = positioned.Select(item => item.ParticipantId).ToHashSet(StringComparer.Ordinal);
        var types = project.DeskTypes.ToDictionary(type => type.Id);
        var occupied = plan.DeskPlacements.SelectMany(desk => desk.GetOccupiedCells(types[desk.DeskTypeId]))
            .Concat(project.Venue.BlockedCells).Concat(positioned.SelectMany(item => item.OccupiedCells)).ToHashSet();
        var result = new List<ParticipantAssignment>();
        foreach (var participant in project.Participants.Where(item => !positionedIds.Contains(item.Id)))
        {
            var count = participant.RequiredCellCount;
            GridPosition[]? cells = null;
            // Try both orientations so narrow aisles can hold multi-cell circles too.
            for (var y = 0; y < project.Venue.Height && cells is null; y++)
            for (var x = 0; x < project.Venue.Width && cells is null; x++)
            foreach (var vertical in new[] { false, true })
            {
                var candidate = Enumerable.Range(0, count).Select(i => new GridPosition(x + (vertical ? 0 : i), y + (vertical ? i : 0))).ToArray();
                if (candidate.All(cell => project.Venue.CanPlaceAt(cell) && !occupied.Contains(cell)))
                {
                    cells = candidate;
                    break;
                }
            }
            // One empty row separates the overflow queue from the venue.
            var queueWidth = Math.Max(project.Venue.Width, count);
            for (var y = project.Venue.Height + 1; cells is null; y++)
            for (var x = 0; x <= queueWidth - count && cells is null; x++)
            {
                var candidate = Enumerable.Range(0, count).Select(i => new GridPosition(x + i, y)).ToArray();
                if (candidate.All(cell => !occupied.Contains(cell))) cells = candidate;
            }
            occupied.UnionWith(cells);
            result.Add(new(participant.Id, cells.ToHashSet(), cells[0]));
        }
        return result;
    }
}
