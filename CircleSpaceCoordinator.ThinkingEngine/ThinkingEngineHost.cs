namespace CircleSpaceCoordinator.ThinkingEngine;

using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;

public static class ThinkingEngineHost
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var port = builder.Configuration.GetValue("port", 5072);
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, port, endpoint => endpoint.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc(options =>
        {
            options.MaxReceiveMessageSize = 32 * 1024 * 1024;
            options.MaxSendMessageSize = 32 * 1024 * 1024;
        });
        var app = builder.Build();
        app.MapGrpcService<ThinkingService>();
        return app;
    }
}
