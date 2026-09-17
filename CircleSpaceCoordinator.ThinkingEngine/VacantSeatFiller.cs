namespace CircleSpaceCoordinator.ThinkingEngine;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Validation;

/// <summary>Deterministic first placement, preserving existing assignments and combined groups.</summary>
public static class VacantSeatFiller
{
    public static CircleSpaceProject Fill(CircleSpaceProject project, string planId, CancellationToken cancellationToken = default)
    {
        var plan = project.Plans.Single(p => p.Id == planId);
        var participants = project.Participants.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var assignments = plan.Assignments.ToDictionary(a => a.ParticipantId, StringComparer.Ordinal);
        var temporary = plan.TemporaryPlacements.ToDictionary(a => a.ParticipantId, StringComparer.Ordinal);
        var types = project.DeskTypes.ToDictionary(t => t.Id, StringComparer.Ordinal);
        var desks = plan.DeskPlacements.Select(d => d.GetSeatCells(types[d.DeskTypeId]))
            .Where(cells => cells.Count > 0).OrderBy(cells => cells.Min(c => c.Y))
            .ThenBy(cells => cells.Min(c => c.X)).ToArray();
        // Include manually combined stones parked in the aisle as well as imported partners.
        var partners = GeneralAttendeeEvaluator.BuildCombinedPartners(project,
            plan with { Assignments = [.. plan.Assignments, .. plan.TemporaryPlacements] });
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var groups = new List<string[]>();
        foreach (var participant in project.Participants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!seen.Add(participant.Id)) continue;
            var group = new List<string>();
            var pending = new Queue<string>();
            pending.Enqueue(participant.Id);
            while (pending.TryDequeue(out var id))
            {
                group.Add(id);
                foreach (var partner in partners[id]) if (seen.Add(partner)) pending.Enqueue(partner);
            }
            groups.Add(group.ToArray());
        }
        // Place constrained partners first, then large groups before singles.
        foreach (var group in groups.Where(g => g.Any(id => !assignments.ContainsKey(id)))
                     .OrderByDescending(g => g.Any(assignments.ContainsKey))
                     .ThenByDescending(g => g.Sum(id => (long)participants[id].RequiredCellCount)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var missing = group.Where(id => !assignments.ContainsKey(id)).ToArray();
            var needed = missing.Sum(id => (long)participants[id].RequiredCellCount);
            var fixedCells = group.Where(assignments.ContainsKey).SelectMany(id => assignments[id].OccupiedCells).ToHashSet();
            var occupied = assignments.Values.SelectMany(a => a.OccupiedCells)
                .Concat(temporary.Values.Where(a => !missing.Contains(a.ParticipantId)).SelectMany(a => a.OccupiedCells)).ToHashSet();
            // Best fit avoids consuming a large empty frame when a small one is enough.
            GridPosition[]? destination = null;
            foreach (var desk in desks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!desk.IsSupersetOf(fixedCells)) continue;
                var free = desk.Where(c => !occupied.Contains(c) && project.Venue.CanPlaceAt(c)).ToArray();
                if (free.LongLength < needed || destination is not null && destination.Length <= free.Length) continue;
                destination = free;
            }
            if (destination is null) continue;
            var ordered = destination.OrderBy(c => c.Y).ThenBy(c => c.X).ToArray();
            var offset = 0;
            foreach (var id in missing)
            {
                var cells = ordered.Skip(offset).Take(participants[id].RequiredCellCount).ToHashSet();
                offset += cells.Count;
                assignments.Add(id, new ParticipantAssignment(id, cells, ordered[offset - cells.Count])
                { CombinedSpaceId = temporary.GetValueOrDefault(id)?.CombinedSpaceId });
                temporary.Remove(id);
            }
        }
        if (assignments.Count == plan.Assignments.Count) return project;
        var filled = plan with { Assignments = assignments.Values.ToArray(), TemporaryPlacements = temporary.Values.ToArray() };
        var result = LayoutProjection.CommitLegacyPlanEdits(project with
        { Plans = project.Plans.Select(p => p.Id == planId ? filled : p).ToArray() }, planId);
        var issues = ProjectValidator.Validate(result);
        if (issues.Count > 0) throw new ProjectValidationException(issues);
        return result;
    }
}
