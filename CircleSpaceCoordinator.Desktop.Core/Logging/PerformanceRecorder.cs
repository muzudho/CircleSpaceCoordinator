namespace CircleSpaceCoordinator.Desktop.Core.Logging;

using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

/// <summary>Numeric diagnostics only. The UI never performs log file I/O or waits for the writer.</summary>
public sealed class PerformanceRecorder : IDisposable
{
    private readonly Channel<object> queue = Channel.CreateBounded<object>(new BoundedChannelOptions(256)
    { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
    private readonly Dictionary<string, Measurement> measurements = [];
    private readonly object gate = new();
    private readonly Task writer;
    private readonly Timer sampler;
    private string phase = "idle";
    private string context = "startup";
    private long phaseStarted = Stopwatch.GetTimestamp();
    private long heartbeat = Stopwatch.GetTimestamp();
    private long previousSample = Stopwatch.GetTimestamp();
    private double previousCpuMs;
    private long dropped;
    private int sampling;
    private int disposed;

    public PerformanceRecorder(string path)
    {
        using var process = Process.GetCurrentProcess();
        previousCpuMs = process.TotalProcessorTime.TotalMilliseconds;
        writer = Task.Run(() => WriteAsync(path));
        sampler = new Timer(_ => CaptureSample(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    public void Heartbeat(string screen)
    {
        Volatile.Write(ref context, screen);
        Interlocked.Exchange(ref heartbeat, Stopwatch.GetTimestamp());
    }

    public Scope Measure(string name)
    {
        var previous = Volatile.Read(ref phase);
        var previousStart = Interlocked.Read(ref phaseStarted);
        var start = Stopwatch.GetTimestamp();
        Volatile.Write(ref phase, name);
        Interlocked.Exchange(ref phaseStarted, start);
        return new(this, name, start, previous, previousStart);
    }

    public void Record(string name, double milliseconds)
    {
        lock (gate)
        {
            measurements.TryGetValue(name, out var old);
            measurements[name] = new(old.Count + 1, old.TotalMs + milliseconds, Math.Max(old.MaxMs, milliseconds));
        }
    }

    public void WriteStartupEntry(object entry)
    {
        if (!queue.Writer.TryWrite(entry)) Interlocked.Increment(ref dropped);
    }

    public readonly struct Scope(PerformanceRecorder owner, string name, long start, string previous, long previousStart) : IDisposable
    {
        public void Dispose()
        {
            owner.Record(name, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            Volatile.Write(ref owner.phase, previous);
            Interlocked.Exchange(ref owner.phaseStarted, previousStart);
        }
    }

    private readonly record struct Measurement(long Count, double TotalMs, double MaxMs);

    public void CaptureSample()
    {
        if (Volatile.Read(ref disposed) != 0 || Interlocked.Exchange(ref sampling, 1) != 0) return;
        try
        {
            using var process = Process.GetCurrentProcess();
            var cpu = process.TotalProcessorTime.TotalMilliseconds;
            var elapsed = Stopwatch.GetElapsedTime(previousSample).TotalMilliseconds;
            var memory = GC.GetGCMemoryInfo();
            Dictionary<string, Measurement> timings;
            lock (gate) { timings = new(measurements); measurements.Clear(); }
            var activePhase = Volatile.Read(ref phase);
            var sample = new
            {
                timestampUtc = DateTimeOffset.UtcNow,
                debuggerAttached = Debugger.IsAttached,
                context = Volatile.Read(ref context), phase = activePhase,
                phaseMs = activePhase == "idle" ? 0 : Stopwatch.GetElapsedTime(Interlocked.Read(ref phaseStarted)).TotalMilliseconds,
                uiHeartbeatAgeMs = Stopwatch.GetElapsedTime(Interlocked.Read(ref heartbeat)).TotalMilliseconds,
                sampleIntervalMs = elapsed,
                cpuPercent = Math.Clamp((cpu - previousCpuMs) / Math.Max(1, elapsed) / Environment.ProcessorCount * 100, 0, 100),
                workingSetBytes = process.WorkingSet64, privateBytes = process.PrivateMemorySize64,
                managedBytes = GC.GetTotalMemory(false), allocatedBytesTotal = GC.GetTotalAllocatedBytes(false),
                gcHeapBytes = memory.HeapSizeBytes, gcFragmentedBytes = memory.FragmentedBytes,
                gcMemoryLoadBytes = memory.MemoryLoadBytes, gcHighMemoryThresholdBytes = memory.HighMemoryLoadThresholdBytes,
                gcAvailableMemoryBytes = memory.TotalAvailableMemoryBytes,
                gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2),
                droppedSamples = Interlocked.Read(ref dropped), timings,
            };
            previousCpuMs = cpu;
            previousSample = Stopwatch.GetTimestamp();
            if (!queue.Writer.TryWrite(sample)) Interlocked.Increment(ref dropped);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
        finally { Volatile.Write(ref sampling, 0); }
    }

    private async Task WriteAsync(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            // Keep the current and preceding 8 MiB segment. No unbounded diagnostic file.
            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            using var output = new StreamWriter(stream);
            await foreach (var entry in queue.Reader.ReadAllAsync())
            {
                if (stream.Length >= 8 * 1024 * 1024)
                {
                    await output.FlushAsync();
                    File.Copy(path, path + ".previous", overwrite: true);
                    stream.SetLength(0);
                    stream.Position = 0;
                }
                await output.WriteLineAsync(JsonSerializer.Serialize(entry));
                await output.FlushAsync();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public void Dispose()
    {
        if (Volatile.Read(ref disposed) != 0) return;
        sampler.Dispose();
        CaptureSample();
        Interlocked.Exchange(ref disposed, 1);
        queue.Writer.TryComplete();
        // A slow/full disk must not prevent the application from closing.
        try { writer.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
    }
}
