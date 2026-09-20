using CircleSpaceCoordinator.ThinkingEngine;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

var entered = Stopwatch.GetTimestamp();
await RunAsync(args, entered);

static async Task RunAsync(string[] args, long entered)
{
    var timing = new ThinkingStartupTiming();
    WebApplication? app = null;
    try
    {
        app = ThinkingEngineHost.Build(args, timing.Record);
        var stage = Stopwatch.GetTimestamp();
        await app.StartAsync();
        timing.Record("start_server", Stopwatch.GetElapsedTime(stage).TotalMilliseconds);
        stage = Stopwatch.GetTimestamp();
        if (app.Configuration["ready-file"] is { } readyFile)
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            await File.WriteAllTextAsync(readyFile + ".tmp", address);
            File.Move(readyFile + ".tmp", readyFile, overwrite: true);
        }
        timing.Record("publish_ready", Stopwatch.GetElapsedTime(stage).TotalMilliseconds);
        timing.Write(true, Stopwatch.GetElapsedTime(entered).TotalMilliseconds);
    }
    catch
    {
        timing.Write(false, Stopwatch.GetElapsedTime(entered).TotalMilliseconds);
        if (app is not null) await app.DisposeAsync();
        throw;
    }
    await using var lifetime = app;
    if (int.TryParse(app.Configuration["parent-pid"], out var parentId))
    {
        _ = Task.Run(async () =>
        {
            try { using var parent = Process.GetProcessById(parentId); await parent.WaitForExitAsync(); }
            catch (ArgumentException) { }
            finally { app.Lifetime.StopApplication(); }
        });
    }
    await app.WaitForShutdownAsync();
}
