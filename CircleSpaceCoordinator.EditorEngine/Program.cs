using CircleSpaceCoordinator.EditorEngine;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

CircleSpaceCoordinator.Engine.Contracts.ParentProcessLifetime.Watch(args);
await using var app = EditorEngineHost.Build(args);
await app.StartAsync();
if (app.Configuration["ready-file"] is { } readyFile)
{
    var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    await File.WriteAllTextAsync(readyFile + ".tmp", address);
    File.Move(readyFile + ".tmp", readyFile, overwrite: true);
}
await app.WaitForShutdownAsync();
