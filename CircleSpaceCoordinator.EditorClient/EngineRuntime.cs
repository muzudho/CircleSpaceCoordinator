namespace CircleSpaceCoordinator.EditorClient;

using System.Diagnostics;
using System.Collections.Concurrent;
using CircleSpaceCoordinator.Engine.Contracts.V1;
using Grpc.Net.Client;

// Owns only the child processes it starts. External CLI servers are not stopped.
public sealed class EngineRuntime : IDisposable
{
    private readonly List<Process> processes = [];
    private readonly string readyDirectory = Path.Combine(Path.GetTempPath(), $"circle-space-{Guid.NewGuid():N}");
    public EditorConnection Connection { get; private set; } = null!;

    public static async Task<EngineRuntime> StartAsync(string baseDirectory, string? stateDirectory = null)
    {
        var runtime = new EngineRuntime();
        Directory.CreateDirectory(runtime.readyDirectory);
        try
        {
            var thinkingAddress = await runtime.StartEngine(baseDirectory, "thinking", "CircleSpaceCoordinator.ThinkingEngine", []);
            using (var channel = GrpcChannel.ForAddress(thinkingAddress))
            {
                var info = await new Thinking.ThinkingClient(channel).DescribeAsync(new Empty(), EditorConnection.Deadline());
                if (info.ApiMajor != 1) throw new InvalidOperationException("Unsupported thinking API version.");
            }
            stateDirectory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CircleSpaceCoordinator", "EngineSessions");
            var address = await runtime.StartEngine(baseDirectory, "editor", "CircleSpaceCoordinator.EditorEngine",
                ["--thinking-address", thinkingAddress, "--state-directory", stateDirectory]);
            runtime.Connection = new EditorConnection(address);
            var editor = await runtime.Connection.Client.DescribeAsync(new Empty(), EditorConnection.Deadline());
            if (editor.ApiMajor != 1) throw new InvalidOperationException("Unsupported editor API version.");
            return runtime;
        }
        catch { runtime.Dispose(); throw; }
    }

    private async Task<string> StartEngine(string baseDirectory, string folder, string name, string[] args)
    {
        var directory = Path.Combine(baseDirectory, "engines", folder);
        var executable = Path.Combine(directory, name + (OperatingSystem.IsWindows() ? ".exe" : ""));
        var info = new ProcessStartInfo
        {
            FileName = File.Exists(executable) ? executable : "dotnet",
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (!File.Exists(executable)) info.ArgumentList.Add(Path.Combine(directory, name + ".dll"));
        var ready = Path.Combine(readyDirectory, folder + ".txt");
        foreach (var argument in new[] { "--port", "0", "--ready-file", ready, "--parent-pid", Environment.ProcessId.ToString() }.Concat(args))
            info.ArgumentList.Add(argument);
        var output = new ConcurrentQueue<string>();
        var process = new Process { StartInfo = info };
        void Capture(object sender, DataReceivedEventArgs eventArgs)
        {
            if (eventArgs.Data is not { } line) return;
            output.Enqueue(line);
            while (output.Count > 32) output.TryDequeue(out _);
        }
        process.OutputDataReceived += Capture;
        process.ErrorDataReceived += Capture;
        if (!process.Start()) throw new InvalidOperationException($"Could not start {name}.");
        processes.Add(process);
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!File.Exists(ready))
        {
            if (process.HasExited) throw new InvalidOperationException($"{name} exited with code {process.ExitCode}.\n{string.Join(Environment.NewLine, output)}");
            if (DateTime.UtcNow >= deadline) throw new TimeoutException($"{name} did not become ready.\n{string.Join(Environment.NewLine, output)}");
            await Task.Delay(100);
        }
        var address = await File.ReadAllTextAsync(ready);
        return address.Trim();
    }
    public void Dispose()
    {
        Connection?.Dispose();
        foreach (var process in processes.AsEnumerable().Reverse())
        {
            try { if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(5000); } }
            catch (InvalidOperationException) { }
            finally { process.Dispose(); }
        }
        if (Directory.Exists(readyDirectory))
        {
            foreach (var file in Directory.GetFiles(readyDirectory)) File.Delete(file);
            Directory.Delete(readyDirectory);
        }
    }
}
