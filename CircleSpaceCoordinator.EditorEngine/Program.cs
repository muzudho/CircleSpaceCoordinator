using CircleSpaceCoordinator.EditorEngine;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

await using var app = EditorEngineHost.Build(args);
await app.StartAsync();
if (app.Configuration["ready-file"] is { } readyFile)
{
    var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    await File.WriteAllTextAsync(readyFile + ".tmp", address);
    File.Move(readyFile + ".tmp", readyFile, overwrite: true);
}
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
