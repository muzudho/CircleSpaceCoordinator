namespace CircleSpaceCoordinator.Desktop.Tests;

using System.Text.Json;
using CircleSpaceCoordinator.Desktop.Core.Logging;

internal static partial class Program
{
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
