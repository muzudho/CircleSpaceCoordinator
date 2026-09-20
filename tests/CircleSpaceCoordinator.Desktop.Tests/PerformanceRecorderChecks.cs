namespace CircleSpaceCoordinator.Desktop.Tests;

using System.Text.Json;
using CircleSpaceCoordinator.Desktop.Core.Logging;

internal static partial class Program
{
    private static void StartupRecording()
    {
        var directory = Path.Combine(Path.GetTempPath(), "csc-startup-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "startup.jsonl");
        try
        {
            using (var recorder = new PerformanceRecorder(path))
            {
                var startup = new StartupTiming(recorder);
                startup.SpinnerFrameSubmitted();
                var stage = System.Diagnostics.Stopwatch.GetTimestamp();
                startup.Record("thinking_process_ready", System.Diagnostics.Stopwatch.GetElapsedTime(stage).TotalMilliseconds);
                startup.Complete(true);
                startup.Complete(true);
            }
            var summaries = File.ReadAllLines(path).Where(line => line.Contains("\"kind\":\"startup_summary\"", StringComparison.Ordinal)).ToArray();
            AssertEqual(1, summaries.Length);
            using var doc = JsonDocument.Parse(summaries.Single());
            var root = doc.RootElement;
            AssertEqual(true, root.GetProperty("success").GetBoolean());
            var stages = root.GetProperty("stages").EnumerateArray().ToArray();
            AssertEqual(true, Math.Abs(stages.Sum(item => item.GetProperty("percent").GetDouble()) - 100) < .001);
            AssertEqual(true, Math.Abs(stages.Sum(item => item.GetProperty("milliseconds").GetDouble()) - root.GetProperty("totalMs").GetDouble()) < .001);
            AssertEqual(true, root.GetProperty("firstSpinnerFrameMs").GetDouble() <= root.GetProperty("totalMs").GetDouble());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    private static void PerformanceRecording()
    {
        var directory = Path.Combine(Path.GetTempPath(), "csc-performance-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "performance.jsonl");
        try
        {
            using (var recorder = new PerformanceRecorder(path))
            {
                recorder.Heartbeat("genre_text");
                recorder.Record("draw", 10);
                recorder.Record("draw", 30);
                using (recorder.Measure("update"))
                {
                    using (recorder.Measure("genre_save")) recorder.CaptureSample();
                    // A nested scope must restore the parent operation for the stall monitor.
                    recorder.CaptureSample();
                }
                recorder.CaptureSample();
            }
            var entries = File.ReadAllLines(path).Select(line => JsonDocument.Parse(line)).ToArray();
            try
            {
                var duringSave = entries.Select(doc => doc.RootElement).First(entry => entry.GetProperty("phase").GetString() == "genre_save");
                AssertEqual("genre_text", duringSave.GetProperty("context").GetString()!);
                AssertEqual(true, duringSave.GetProperty("workingSetBytes").GetInt64() > 0);
                AssertEqual(true, duringSave.GetProperty("managedBytes").GetInt64() > 0);
                AssertEqual(true, duringSave.GetProperty("uiHeartbeatAgeMs").GetDouble() >= 0);
                var draw = duringSave.GetProperty("timings").GetProperty("draw");
                AssertEqual(2L, draw.GetProperty("Count").GetInt64());
                AssertEqual(40d, draw.GetProperty("TotalMs").GetDouble());
                AssertEqual(30d, draw.GetProperty("MaxMs").GetDouble());
                AssertEqual(true, entries.Any(doc => doc.RootElement.GetProperty("phase").GetString() == "update"));
                AssertEqual(true, entries.Any(doc => doc.RootElement.GetProperty("phase").GetString() == "idle"));
            }
            finally { foreach (var entry in entries) entry.Dispose(); }

            // An unusable destination must not break editing or throw on shutdown.
            using var unavailable = new PerformanceRecorder(Path.Combine(path, "invalid.jsonl"));
            unavailable.Record("draw", 1);
            unavailable.CaptureSample();
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}
