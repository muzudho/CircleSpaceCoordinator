namespace CircleSpaceCoordinator.EditorEngine;

using System.Net;
using CircleSpaceCoordinator.Engine.Contracts.V1;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Server.Kestrel.Core;

public static class EditorEngineHost
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var port = builder.Configuration.GetValue("port", 5071);
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, port, endpoint => endpoint.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc(options =>
        {
            options.MaxReceiveMessageSize = 32 * 1024 * 1024;
            options.MaxSendMessageSize = 32 * 1024 * 1024;
        });
        builder.Services.AddSingleton<ThinkingEngineProvider>();
        builder.Services.AddSingleton<EditorService>();
        builder.Services.AddSingleton<SessionRepository>();
        var app = builder.Build();
        app.MapGrpcService<EditorService>();
        return app;
    }
}
