namespace CircleSpaceCoordinator.Core.Evaluation;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;

public sealed record GenreEvaluationResult(
    string GenreId,
    int AssignedCircleCount,
    int ContinuousGroupCount,
    double Score);

public sealed record AudienceEvaluationResult(
    string PlanId,
    double GeneralAttendeeScore,
    double CircleParticipantScore,
    bool CombinedSpaceRequirementsSatisfied,
    IReadOnlyList<GenreEvaluationResult> Genres);

/// <summary>
/// Genre-continuity evaluation based on automatically detected desk runs, explicit island
/// connectors, and explicit facing regions. One additional group halves the genre score;
/// each group merge across a facing aisle applies the provisional 0.7 coefficient.
/// </summary>
public static class GeneralAttendeeEvaluator
{
    public static IReadOnlyList<AudienceEvaluationResult> Evaluate(CircleSpaceProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var circleResults = ProjectEvaluator.Evaluate(project).ToDictionary(result => result.PlanId);
        var participantsByCircleId = project.Participants.ToDictionary(item => item.CircleId, StringComparer.Ordinal);

        return project.Plans.Select(plan =>
        {
            var topology = VenueTopologyAnalyzer.Build(project, plan);
            var assignments = plan.Assignments.ToDictionary(item => item.ParticipantId);
            var genreMembers = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var participant in project.Participants)
                AddGenreMember(participant.GenreId, participant.Id);

            var combinedPartners = BuildCombinedPartners(project, plan, participantsByCircleId);

            var genres = genreMembers.Select(pair =>
            {
                var assigned = pair.Value.Where(assignments.ContainsKey).ToArray();
                var connectivityMembers = assigned
                    .SelectMany(id => combinedPartners.TryGetValue(id, out var partners)
                        ? partners.Append(id)
                        : [id])
                    .Where(assignments.ContainsKey)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                var (groups, facingMerges) = CountGroups(connectivityMembers, assigned, assignments, topology);
                var score = assigned.Length == 0 ? 0d : assigned.Length *
                    Math.Pow(0.5d, Math.Max(0, groups - 1)) * Math.Pow(0.7d, facingMerges);
                return new GenreEvaluationResult(pair.Key, assigned.Length, groups, score);
            }).OrderBy(item => item.GenreId, StringComparer.Ordinal).ToArray();
            var combinedSpaceRequirementsSatisfied = CombinedSpaceRequirementsAreSatisfied(
                project, plan, participantsByCircleId);

            return new AudienceEvaluationResult(
                plan.Id,
                combinedSpaceRequirementsSatisfied ? genres.Sum(item => item.Score) : 0d,
                combinedSpaceRequirementsSatisfied ? circleResults[plan.Id].TotalScore : 0d,
                combinedSpaceRequirementsSatisfied,
                genres);

            void AddGenreMember(string? genreId, string participantId)
            {
                if (string.IsNullOrWhiteSpace(genreId))
                    return;
                if (!genreMembers.TryGetValue(genreId, out var members))
                    genreMembers.Add(genreId, members = new HashSet<string>(StringComparer.Ordinal));
                members.Add(participantId);
            }
        }).ToArray();
    }

    public static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildCombinedPartners(
        CircleSpaceProject project,
        Plan plan,
        IReadOnlyDictionary<string, Participant>? participantsByCircleId = null)
    {
        participantsByCircleId ??= project.Participants.ToDictionary(item => item.CircleId, StringComparer.Ordinal);
        var partners = project.Participants.ToDictionary(
            item => item.Id, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var participant in project.Participants)
            if (participant.CombinedWithCircleId is { } partnerCircleId &&
                participantsByCircleId.TryGetValue(partnerCircleId, out var partner))
            {
                partners[participant.Id].Add(partner.Id);
                partners[partner.Id].Add(participant.Id);
            }
        foreach (var group in plan.Assignments.Where(item => item.CombinedSpaceId is not null)
                     .GroupBy(item => item.CombinedSpaceId!, StringComparer.Ordinal))
        foreach (var first in group)
        foreach (var second in group.Where(item => item.ParticipantId != first.ParticipantId))
            partners[first.ParticipantId].Add(second.ParticipantId);
        return partners.ToDictionary(pair => pair.Key, pair => (IReadOnlySet<string>)pair.Value, StringComparer.Ordinal);
    }

    private static bool CombinedSpaceRequirementsAreSatisfied(
        CircleSpaceProject project,
        Plan plan,
        IReadOnlyDictionary<string, Participant> participantsByCircleId)
    {
        var assignments = plan.Assignments.ToDictionary(item => item.ParticipantId);
        var deskTypes = project.DeskTypes.ToDictionary(item => item.Id);
        var deskCellsById = plan.DeskPlacements.ToDictionary(
            desk => desk.Id,
            desk => desk.GetOccupiedCells(deskTypes[desk.DeskTypeId]));
        var checkedPairs = new HashSet<string>(StringComparer.Ordinal);

        foreach (var participant in project.Participants)
        {
            if (participant.CombinedWithCircleId is not { } partnerCircleId ||
                !participantsByCircleId.TryGetValue(partnerCircleId, out var partner))
                continue;
            var pairKey = string.Join("|", new[] { participant.Id, partner.Id }.Order(StringComparer.Ordinal));
            if (!checkedPairs.Add(pairKey))
                continue;
            if (!assignments.TryGetValue(participant.Id, out var first) ||
                !assignments.TryGetValue(partner.Id, out var second) ||
                FindContainingDesk(first) is not { } firstDeskId ||
                FindContainingDesk(second) is not { } secondDeskId ||
                !string.Equals(firstDeskId, secondDeskId, StringComparison.Ordinal))
                return false;
        }
        return true;

        string? FindContainingDesk(ParticipantAssignment assignment) => deskCellsById
            .FirstOrDefault(pair => pair.Value.IsSupersetOf(assignment.OccupiedCells))
            .Key;
    }

    private static (int Groups, int FacingMerges) CountGroups(
        IReadOnlyCollection<string> participantIds,
        IReadOnlyCollection<string> countedParticipantIds,
        IReadOnlyDictionary<string, ParticipantAssignment> assignments,
        VenueTopologyGraph topology)
    {
        var cellOwner = assignments.Values.SelectMany(assignment =>
                assignment.OccupiedCells.Select(cell => (Cell: cell, assignment.ParticipantId)))
            .ToDictionary(item => item.Cell, item => item.ParticipantId);
        var participantSet = participantIds.ToHashSet(StringComparer.Ordinal);
        var participantNeighbors = participantIds.ToDictionary(id => id, _ => new HashSet<string>(), StringComparer.Ordinal);
        var unvisitedCells = topology.Neighbors.Keys
            // Empty desk cells are walkable for continuity. A cell occupied by a
            // different genre remains a boundary, so it cannot be skipped over.
            .Where(cell => !cellOwner.TryGetValue(cell, out var owner) || participantSet.Contains(owner))
            .ToHashSet();
        while (unvisitedCells.Count > 0)
        {
            var firstCell = unvisitedCells.First();
            var connectedParticipants = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<GridPosition>();
            unvisitedCells.Remove(firstCell);
            queue.Enqueue(firstCell);
            while (queue.TryDequeue(out var current))
            {
                if (cellOwner.TryGetValue(current, out var owner) && participantSet.Contains(owner))
                    connectedParticipants.Add(owner);
                foreach (var next in topology.Neighbors[current])
                    if (unvisitedCells.Remove(next))
                        queue.Enqueue(next);
            }
            foreach (var first in connectedParticipants)
            foreach (var second in connectedParticipants.Where(second => second != first))
                participantNeighbors[first].Add(second);
        }

        var pending = participantSet.ToHashSet(StringComparer.Ordinal);
        var components = new List<HashSet<string>>();
        while (pending.Count > 0)
        {
            var first = pending.First();
            pending.Remove(first);
            var component = new HashSet<string>(StringComparer.Ordinal) { first };
            var queue = new Queue<string>();
            queue.Enqueue(first);
            while (queue.TryDequeue(out var current))
            {
                foreach (var neighbor in participantNeighbors[current])
                    if (pending.Remove(neighbor))
                    {
                        component.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
            }
            components.Add(component);
        }

        var countedSet = countedParticipantIds.ToHashSet(StringComparer.Ordinal);
        components = components.Where(component => component.Overlaps(countedSet)).ToList();
        if (components.Count <= 1)
            return (components.Count, 0);
        var componentByParticipant = components.SelectMany((component, index) =>
            component.Select(participantId => (participantId, index)))
            .ToDictionary(item => item.participantId, item => item.index, StringComparer.Ordinal);
        var links = Enumerable.Range(0, components.Count).ToDictionary(index => index, _ => new HashSet<int>());
        foreach (var (firstCell, secondCell) in topology.FacingCellPairs)
            if (cellOwner.TryGetValue(firstCell, out var firstParticipant) && participantSet.Contains(firstParticipant) &&
                cellOwner.TryGetValue(secondCell, out var secondParticipant) && participantSet.Contains(secondParticipant))
            {
                var first = componentByParticipant[firstParticipant];
                var second = componentByParticipant[secondParticipant];
                if (first == second) continue;
                links[first].Add(second);
                links[second].Add(first);
            }

        var unvisited = links.Keys.ToHashSet();
        var finalGroups = 0;
        while (unvisited.Count > 0)
        {
            finalGroups++;
            var queue = new Queue<int>();
            var first = unvisited.First();
            unvisited.Remove(first);
            queue.Enqueue(first);
            while (queue.TryDequeue(out var current))
                foreach (var neighbor in links[current])
                    if (unvisited.Remove(neighbor)) queue.Enqueue(neighbor);
        }
        return (finalGroups, components.Count - finalGroups);
    }
}
