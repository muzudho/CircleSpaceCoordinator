namespace CircleSpaceCoordinator.ThinkingEngine;

using System.Diagnostics;
using System.Text.Json;

/// <summary>Startup-only, non-overlapping stages. Written after readiness, outside the measured interval.</summary>
internal sealed class ThinkingStartupTiming
{
    private readonly List<(string Name, double Ms)> stages = [];
    public void Record(string name, double milliseconds) => stages.Add((name, milliseconds));

    public void Write(bool success, double total)
    {
        var all = stages.Append(("other_managed_startup", Math.Max(0, total - stages.Sum(stage => stage.Ms))));
        var parent = Environment.GetEnvironmentVariable("CSC_STARTUP_PARENT_PID") ?? "standalone";
        var path = Path.Combine(Environment.GetEnvironmentVariable("CSC_STARTUP_LOG_DIRECTORY")
            ?? Path.Combine(AppContext.BaseDirectory, "logs"),
            $"thinking-startup-{DateTime.Now:yyyyMMdd-HHmmss}-{parent}-{Environment.ProcessId}.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var summary = new
            {
                kind = "thinking_startup_summary", timestampUtc = DateTimeOffset.UtcNow,
                processId = Environment.ProcessId, parentProcessId = parent, success,
                debuggerAttached = Debugger.IsAttached, totalMs = total,
                stages = all.Select(stage => new { name = stage.Item1, milliseconds = stage.Item2,
                    percent = total > 0 ? stage.Item2 / total * 100 : 0 }).ToArray(),
            };
            File.WriteAllText(path, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
