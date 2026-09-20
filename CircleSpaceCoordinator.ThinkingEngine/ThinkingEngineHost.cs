namespace CircleSpaceCoordinator.ThinkingEngine;

using System.Net;
using System.Diagnostics;
using Microsoft.AspNetCore.Server.Kestrel.Core;

public static class ThinkingEngineHost
{
    public static WebApplication Build(string[] args, Action<string, double>? startupTiming = null)
    {
        var stage = Stopwatch.GetTimestamp();
        var builder = WebApplication.CreateBuilder(args);
        startupTiming?.Invoke("create_builder", Stopwatch.GetElapsedTime(stage).TotalMilliseconds);
        stage = Stopwatch.GetTimestamp();
        var port = builder.Configuration.GetValue("port", 5072);
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, port, endpoint => endpoint.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc(options =>
        {
            options.MaxReceiveMessageSize = 32 * 1024 * 1024;
            options.MaxSendMessageSize = 32 * 1024 * 1024;
        });
        builder.Services.AddSingleton<ThinkingService>();
        startupTiming?.Invoke("configure_services", Stopwatch.GetElapsedTime(stage).TotalMilliseconds);
        stage = Stopwatch.GetTimestamp();
        var app = builder.Build();
        startupTiming?.Invoke("build_host", Stopwatch.GetElapsedTime(stage).TotalMilliseconds);
        stage = Stopwatch.GetTimestamp();
        app.MapGrpcService<ThinkingService>();
        startupTiming?.Invoke("map_grpc", Stopwatch.GetElapsedTime(stage).TotalMilliseconds);
        return app;
    }
}
