namespace CircleSpaceCoordinator.EditorEngine;

using System.Diagnostics;
using CircleSpaceCoordinator.Engine.Contracts.V1;
using Grpc.Core;
using Grpc.Net.Client;

/// <summary>Own a local thinking process only for the duration of an optimization.</summary>
public sealed class ThinkingEngineProvider(IConfiguration configuration, IHostApplicationLifetime lifetime)
{
    private readonly SemaphoreSlim slot = new(1, 1);
    public bool StartsLocalProcess => configuration["thinking-directory"] is not null;

    public async Task<Lease> AcquireAsync(CancellationToken cancellationToken)
    {
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.ApplicationStopping);
        startup.CancelAfter(TimeSpan.FromSeconds(30));
        var token = startup.Token;
        if (StartsLocalProcess && !await slot.WaitAsync(0, token))
            throw new RpcException(new Status(StatusCode.ResourceExhausted, "Thinking engine is busy."));
        var lease = new Lease(StartsLocalProcess ? slot : null);
        try
        {
            var address = configuration["thinking-address"];
            if (configuration["thinking-directory"] is { } directory)
            {
                lease.ReadyDirectory = Path.Combine(Path.GetTempPath(), "csc-thinking-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(lease.ReadyDirectory);
                var ready = Path.Combine(lease.ReadyDirectory, "ready.txt");
                var executable = Path.Combine(directory, "CircleSpaceCoordinator.ThinkingEngine" + (OperatingSystem.IsWindows() ? ".exe" : ""));
                var info = new ProcessStartInfo
                {
                    FileName = File.Exists(executable) ? executable : "dotnet", WorkingDirectory = directory,
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                };
                if (!File.Exists(executable)) info.ArgumentList.Add(Path.Combine(directory, "CircleSpaceCoordinator.ThinkingEngine.dll"));
                foreach (var arg in new[] { "--port", "0", "--ready-file", ready, "--parent-pid", Environment.ProcessId.ToString() })
                    info.ArgumentList.Add(arg);
                if (configuration["thinking-log-directory"] is { } logs) info.Environment["CSC_STARTUP_LOG_DIRECTORY"] = logs;
                info.Environment["CSC_STARTUP_PARENT_PID"] = Environment.ProcessId.ToString();
                token.ThrowIfCancellationRequested();
                lease.Process = Process.Start(info) ?? throw new InvalidOperationException("Cannot start thinking engine.");
                lease.Process.OutputDataReceived += (_, _) => { };
                lease.Process.ErrorDataReceived += (_, _) => { };
                lease.Process.BeginOutputReadLine();
                lease.Process.BeginErrorReadLine();
                while (!File.Exists(ready))
                {
                    token.ThrowIfCancellationRequested();
                    if (lease.Process.HasExited) throw new InvalidOperationException("Thinking engine exited before becoming ready.");
                    await Task.Delay(25, token);
                }
                address = (await File.ReadAllTextAsync(ready, token)).Trim();
            }
            lease.Channel = GrpcChannel.ForAddress(address ?? "http://127.0.0.1:5072",
                new GrpcChannelOptions { MaxReceiveMessageSize = 32 * 1024 * 1024, MaxSendMessageSize = 32 * 1024 * 1024 });
            lease.Client = new Thinking.ThinkingClient(lease.Channel);
            if (StartsLocalProcess)
            {
                var description = await lease.Client.DescribeAsync(new Empty(), cancellationToken: token);
                if (description.ApiMajor != 1) throw new InvalidOperationException("Unsupported thinking API version.");
            }
            lease.Shutdown = lifetime.ApplicationStopping.Register(lease.StopProcess);
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    public sealed class Lease(SemaphoreSlim? slot) : IDisposable
    {
        internal Process? Process;
        internal string? ReadyDirectory;
        internal GrpcChannel? Channel;
        internal CancellationTokenRegistration Shutdown;
        private readonly object gate = new();
        private bool disposed;
        public Thinking.ThinkingClient Client { get; internal set; } = null!;
        internal void StopProcess()
        {
            lock (gate)
            {
                try { if (Process is { HasExited: false }) { Process.Kill(entireProcessTree: true); Process.WaitForExit(5000); } }
                catch (InvalidOperationException) { }
            }
        }
        public void Dispose()
        {
            Shutdown.Dispose();
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                Channel?.Dispose();
                StopProcess();
                Process?.Dispose();
                Process = null;
                try { if (ReadyDirectory is not null && Directory.Exists(ReadyDirectory)) Directory.Delete(ReadyDirectory, recursive: true); }
                catch (IOException) { }
                slot?.Release();
            }
        }
    }
}
