namespace CircleSpaceCoordinator.Desktop.Core.Logging;

using System.Diagnostics;

/// <summary>Disjoint startup stages, measured across the background worker and UI handoff.</summary>
public sealed class StartupTiming(PerformanceRecorder recorder)
{
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly List<(string Name, double Milliseconds)> stages = [];
    private double? firstSpinnerFrameMs;
    private bool complete;
    private readonly object gate = new();

    public void Record(string name, double milliseconds)
    {
        lock (gate)
        {
            if (complete) return;
            stages.Add((name, milliseconds));
            recorder.WriteStartupEntry(new { kind = "startup_stage", timestampUtc = DateTimeOffset.UtcNow,
                name, milliseconds, elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds });
        }
    }

    public void SpinnerFrameSubmitted()
    {
        lock (gate) firstSpinnerFrameMs ??= Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    public void Complete(bool success)
    {
        lock (gate)
        {
            if (complete) return;
            complete = true;
            var total = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var all = stages.Append(("scheduling_and_other", Math.Max(0, total - stages.Sum(stage => stage.Milliseconds))));
            recorder.WriteStartupEntry(new { kind = "startup_summary", timestampUtc = DateTimeOffset.UtcNow,
                success, totalMs = total, firstSpinnerFrameMs,
                stages = all.Select(stage => new { name = stage.Item1, milliseconds = stage.Item2,
                    percent = total > 0 ? stage.Item2 / total * 100 : 0 }).ToArray() });
        }
    }
}
