namespace CircleSpaceCoordinator.OptimizationEngine;

using System.Diagnostics;
using CircleSpaceCoordinator.Application.Editing;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Model;

/// <summary>
/// An anytime simulated-annealing optimizer. It never mutates the source project;
/// the best valid plan found so far is returned when cancelled.
/// </summary>
public sealed class CirclePlacementOptimizationEngine
{
    public CirclePlacementOptimizationResult Optimize(
        CircleSpaceProject source,
        string planId,
        CirclePlacementOptimizationOptions? options = null,
        IProgress<CirclePlacementOptimizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        options ??= new CirclePlacementOptimizationOptions();
        if (options.MaximumIterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(options.MaximumIterations));
        if (options.TimeLimit <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options.TimeLimit));

        var sourcePlan = source.Plans.Single(plan => plan.Id == planId);
        var currentProject = source;
        var currentScore = Evaluate(currentProject, planId);
        var bestPlan = sourcePlan;
        var bestScore = currentScore;
        var random = options.RandomSeed is { } seed ? new Random(seed) : Random.Shared;
        var movableGroups = BuildSwapGroups(source, sourcePlan);
        var accepted = 0;
        var iteration = 0;
        var stopped = false;
        var temperature = Math.Max(0.25d, Math.Max(Math.Abs(currentScore.GeneralAttendeeScore), Math.Abs(currentScore.CircleParticipantScore)) * 0.05d);
        var stopwatch = Stopwatch.StartNew();

        for (; iteration < options.MaximumIterations; iteration++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                stopped = true;
                break;
            }
            if (stopwatch.Elapsed >= options.TimeLimit)
                break;
            if (movableGroups.Length < 2)
                break;

            var firstIndex = random.Next(movableGroups.Length);
            var secondIndex = random.Next(movableGroups.Length);
            if (firstIndex == secondIndex)
                continue;
            var firstGroup = movableGroups[firstIndex];
            var secondGroup = movableGroups[secondIndex];
            var currentPlan = currentProject.Plans.Single(plan => plan.Id == planId);
            var firstCellCount = currentPlan.Assignments.Where(assignment => firstGroup.Contains(assignment.ParticipantId)).Sum(assignment => assignment.OccupiedCells.Count);
            var secondCellCount = currentPlan.Assignments.Where(assignment => secondGroup.Contains(assignment.ParticipantId)).Sum(assignment => assignment.OccupiedCells.Count);
            if (firstCellCount != secondCellCount)
                continue;

            CircleSpaceProject candidate;
            try
            {
                candidate = firstGroup.Length == 1 && secondGroup.Length == 1
                    ? ParticipantAssignmentEditor.Swap(currentProject, planId, firstGroup[0], secondGroup[0])
                    : ParticipantAssignmentEditor.SwapGroups(currentProject, planId, firstGroup, secondGroup);
            }
            catch (InvalidOperationException)
            {
                continue;
            }
            var candidateScore = Evaluate(candidate, planId);
            var delta = candidateScore.GeneralAttendeeScore - currentScore.GeneralAttendeeScore;
            if (delta == 0d)
                delta = candidateScore.CircleParticipantScore - currentScore.CircleParticipantScore;
            var cooling = Math.Max(0d, 1d - stopwatch.Elapsed.TotalMilliseconds / options.TimeLimit.TotalMilliseconds);
            var candidateTemperature = temperature * cooling;
            var accept = delta >= 0d || candidateTemperature > 0d && random.NextDouble() < Math.Exp(delta / candidateTemperature);
            if (accept)
            {
                currentProject = candidate;
                currentScore = candidateScore;
                accepted++;
            }
            if (candidateScore.CompareTo(bestScore) > 0)
            {
                bestScore = candidateScore;
                bestPlan = candidate.Plans.Single(plan => plan.Id == planId);
            }
            if ((iteration + 1) % options.ProgressInterval == 0)
                progress?.Report(new CirclePlacementOptimizationProgress(iteration + 1, accepted, currentScore, bestScore, stopwatch.Elapsed));
        }

        progress?.Report(new CirclePlacementOptimizationProgress(iteration, accepted, currentScore, bestScore, stopwatch.Elapsed));
        return new CirclePlacementOptimizationResult(bestPlan, Evaluate(source, planId), bestScore, iteration, accepted, stopped);
    }

    private static CirclePlacementOptimizationScore Evaluate(CircleSpaceProject project, string planId)
    {
        var result = GeneralAttendeeEvaluator.Evaluate(project).Single(item => item.PlanId == planId);
        return new CirclePlacementOptimizationScore(result.GeneralAttendeeScore, result.CircleParticipantScore);
    }

    private static string[][] BuildSwapGroups(CircleSpaceProject project, Plan plan)
    {
        var assignedIds = plan.Assignments.Select(item => item.ParticipantId).ToHashSet(StringComparer.Ordinal);
        var parent = assignedIds.ToDictionary(id => id, id => id, StringComparer.Ordinal);
        string Find(string id)
        {
            var root = parent[id];
            while (root != parent[root]) root = parent[root];
            while (id != root)
            {
                var next = parent[id];
                parent[id] = root;
                id = next;
            }
            return root;
        }
        void Union(string first, string second)
        {
            var firstRoot = Find(first);
            var secondRoot = Find(second);
            if (firstRoot != secondRoot) parent[secondRoot] = firstRoot;
        }

        foreach (var group in plan.Assignments.Where(item => item.CombinedSpaceId is not null)
                     .GroupBy(item => item.CombinedSpaceId!, StringComparer.Ordinal))
        {
            var members = group.Select(item => item.ParticipantId).ToArray();
            foreach (var member in members.Skip(1)) Union(members[0], member);
        }
        var participantsByCircleId = project.Participants.ToDictionary(item => item.CircleId, StringComparer.Ordinal);
        foreach (var participant in project.Participants)
            if (assignedIds.Contains(participant.Id) && participant.CombinedWithCircleId is { } partnerCircleId &&
                participantsByCircleId.TryGetValue(partnerCircleId, out var partner) && assignedIds.Contains(partner.Id))
                Union(participant.Id, partner.Id);

        return assignedIds.GroupBy(Find, StringComparer.Ordinal)
            .Select(group => group.Order(StringComparer.Ordinal).ToArray())
            .ToArray();
    }
}
