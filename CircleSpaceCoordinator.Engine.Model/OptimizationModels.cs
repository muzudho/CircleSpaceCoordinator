namespace CircleSpaceCoordinator.OptimizationEngine;

using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Model;

public sealed record CirclePlacementOptimizationOptions(int MaximumIterations = int.MaxValue, int? RandomSeed = null)
{
    public int ProgressInterval { get; init; } = 100;
    public TimeSpan TimeLimit { get; init; } = TimeSpan.FromMinutes(10);
}

/// <summary>Uses the same lexicographic priority as the plan ranking: general attendees first, circle participants second.</summary>
public sealed record CirclePlacementOptimizationScore(double GeneralAttendeeScore, double CircleParticipantScore)
    : IComparable<CirclePlacementOptimizationScore>
{
    public int CompareTo(CirclePlacementOptimizationScore? other)
    {
        if (other is null) return 1;
        var general = GeneralAttendeeScore.CompareTo(other.GeneralAttendeeScore);
        return general != 0 ? general : CircleParticipantScore.CompareTo(other.CircleParticipantScore);
    }
}

public sealed record CirclePlacementOptimizationProgress(
    int Iteration,
    int AcceptedMoveCount,
    CirclePlacementOptimizationScore CurrentScore,
    CirclePlacementOptimizationScore BestScore,
    TimeSpan Elapsed);

public sealed record CirclePlacementOptimizationResult(
    Plan BestPlan,
    CirclePlacementOptimizationScore InitialScore,
    CirclePlacementOptimizationScore BestScore,
    int IterationCount,
    int AcceptedMoveCount,
    bool WasStopped);

