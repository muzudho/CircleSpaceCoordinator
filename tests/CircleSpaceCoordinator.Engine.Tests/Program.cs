using CircleSpaceCoordinator.EditorEngine;
using CircleSpaceCoordinator.ThinkingEngine;
using CircleSpaceCoordinator.Engine.Contracts.V1;
using CircleSpaceCoordinator.Infrastructure.Json;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Net;

await using var thinkingHost = ThinkingEngineHost.Build(["--port", "0", "--Logging:LogLevel:Default", "Warning"]);
await thinkingHost.StartAsync();
await using var editorHost = EditorEngineHost.Build(["--port", "0", "--thinking-address", Address(thinkingHost),
    "--Logging:LogLevel:Default", "Warning"]);
await editorHost.StartAsync();
try
{
    using var channel = GrpcChannel.ForAddress(Address(editorHost));
    var client = new Editor.EditorClient(channel);
    var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "sample.json"));
    await Expect(StatusCode.InvalidArgument, async () => await client.OpenAsync(new OpenRequest { ProjectJson = "{" }));
    var opened = await client.OpenAsync(new OpenRequest { ProjectJson = json });
    var id = opened.WorkspaceId;
    var original = ProjectJsonSerializer.Load(opened.ProjectJson);
    var plan = original.Plans.Last(); // Also exercises a plan that need not be selected.
    Check(opened.Revision == 1 && !opened.CanUndo, "initial state");
    await Expect(StatusCode.InvalidArgument, async () => await client.EditAsync(new EditRequest
    {
        WorkspaceId = id, ExpectedRevision = 1, RenamePlan = new RenamePlan { PlanId = plan.Id, Name = " " },
    }));
    var renamed = await client.EditAsync(new EditRequest
    {
        WorkspaceId = id, ExpectedRevision = 1, RenamePlan = new RenamePlan { PlanId = plan.Id, Name = "ヘッドレス編集" },
    });
    Check(renamed.Revision == 2 && renamed.CanUndo, "rename revision");
    Check(ProjectJsonSerializer.Load(renamed.ProjectJson).Plans.Single(p => p.Id == plan.Id).Name == "ヘッドレス編集", "rename persists");
    await Expect(StatusCode.Aborted, async () => await client.EditAsync(new EditRequest
        { WorkspaceId = id, ExpectedRevision = 1, Undo = new Empty() }));
    var undone = await client.EditAsync(new EditRequest { WorkspaceId = id, ExpectedRevision = 2, Undo = new Empty() });
    Check(undone.ProjectJson == opened.ProjectJson && undone.CanRedo, "undo restores exact project");
    var redone = await client.EditAsync(new EditRequest { WorkspaceId = id, ExpectedRevision = 3, Redo = new Empty() });
    Check(redone.ProjectJson == renamed.ProjectJson && redone.Revision == 4, "redo restores rename");
    var isolated = await client.OpenAsync(new OpenRequest { ProjectJson = json });
    Check(isolated.ProjectJson == opened.ProjectJson, "workspace isolation");
    var optimized = await client.OptimizeAsync(new EditorOptimizeRequest
    {
        WorkspaceId = id, ExpectedRevision = 4, SourcePlanId = plan.Id, NewPlanId = "headless-result", NewPlanName = "探索結果",
        Options = new OptimizationOptions { MaximumIterations = 10, TimeLimitMs = 1000, RandomSeed = 42 },
    }, deadline: DateTime.UtcNow.AddSeconds(15));
    var optimizedProject = ProjectJsonSerializer.Load(optimized.ProjectJson);
    Check(optimized.Revision == 5 && optimizedProject.Plans.Count == original.Plans.Count + 1, "two-hop gRPC optimization");
    Check(optimizedProject.Plans.Any(p => p.Id == "headless-result"), "result appended");
    var undoOptimization = await client.EditAsync(new EditRequest { WorkspaceId = id, ExpectedRevision = 5, Undo = new Empty() });
    Check(undoOptimization.ProjectJson == redone.ProjectJson, "optimization is undoable");
    await client.CloseAsync(new WorkspaceRequest { WorkspaceId = id });
    await Expect(StatusCode.NotFound, async () => await client.GetAsync(new WorkspaceRequest { WorkspaceId = id }));

    using var thinkingChannel = GrpcChannel.ForAddress(Address(thinkingHost));
    var thinking = new Thinking.ThinkingClient(thinkingChannel);
    await Expect(StatusCode.InvalidArgument, async () => await thinking.OptimizeAsync(new ThinkingRequest
        { ProjectJson = json, PlanId = plan.Id, Options = new OptimizationOptions() }));
    await thinkingHost.StopAsync();
    await Expect(StatusCode.Unavailable, async () => await client.OptimizeAsync(new EditorOptimizeRequest
    {
        WorkspaceId = isolated.WorkspaceId, ExpectedRevision = 1, SourcePlanId = plan.Id,
        NewPlanId = "unavailable", NewPlanName = "Unavailable",
        Options = new OptimizationOptions { MaximumIterations = 1, TimeLimitMs = 100 },
    }, deadline: DateTime.UtcNow.AddSeconds(5)));
    var unchanged = await client.GetAsync(new WorkspaceRequest { WorkspaceId = isolated.WorkspaceId });
    Check(unchanged.Revision == 1 && unchanged.ProjectJson == isolated.ProjectJson, "failed thinking leaves edits unchanged");
    await CheckConcurrentThinking(json, plan.Id);
    Console.WriteLine("PASS: headless gRPC editing, history, isolation, revisions, optimization, cancellation and failure handling.");
}
finally
{
    await editorHost.StopAsync();
    await thinkingHost.StopAsync();
}

static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>()
    .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
static void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"FAIL: {name}");
}
static async Task Expect(StatusCode code, Func<Task> action)
{
    try { await action(); }
    catch (RpcException exception) when (exception.StatusCode == code) { return; }
    throw new Exception($"Expected gRPC {code}.");
}

static async Task CheckConcurrentThinking(string json, string planId)
{
    var builder = WebApplication.CreateBuilder(["--Logging:LogLevel:Default", "Warning"]);
    builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0,
        endpoint => endpoint.Protocols = HttpProtocols.Http2));
    builder.Services.AddGrpc();
    var controlled = new ControlledThinking();
    builder.Services.AddSingleton(controlled);
    await using var thinkingHost = builder.Build();
    thinkingHost.MapGrpcService<ControlledThinking>();
    await thinkingHost.StartAsync();
    await using var editorHost = EditorEngineHost.Build(["--port", "0", "--thinking-address", Address(thinkingHost),
        "--Logging:LogLevel:Default", "Warning"]);
    await editorHost.StartAsync();
    try
    {
        using var channel = GrpcChannel.ForAddress(Address(editorHost));
        var client = new Editor.EditorClient(channel);
        var state = await client.OpenAsync(new OpenRequest { ProjectJson = json });
        var request = new EditorOptimizeRequest
        {
            WorkspaceId = state.WorkspaceId, ExpectedRevision = 1, SourcePlanId = planId,
            NewPlanId = "late-result", NewPlanName = "Late result",
            Options = new OptimizationOptions { MaximumIterations = 1, TimeLimitMs = 100 },
        };
        using var pending = client.OptimizeAsync(request, deadline: DateTime.UtcNow.AddSeconds(15));
        await controlled.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var edited = await client.EditAsync(new EditRequest
        {
            WorkspaceId = state.WorkspaceId, ExpectedRevision = 1,
            RenamePlan = new RenamePlan { PlanId = planId, Name = "Edited while thinking" },
        });
        controlled.Release.TrySetResult();
        await Expect(StatusCode.Aborted, async () => await pending);
        var after = await client.GetAsync(new WorkspaceRequest { WorkspaceId = state.WorkspaceId });
        Check(after.ProjectJson == edited.ProjectJson && after.Revision == 2, "late result cannot overwrite edits");

        controlled.Reset();
        request.ExpectedRevision = 2;
        using var cancellation = new CancellationTokenSource();
        using var cancelled = client.OptimizeAsync(request, deadline: DateTime.UtcNow.AddSeconds(15), cancellationToken: cancellation.Token);
        await controlled.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Expect(StatusCode.Cancelled, async () => await cancelled);
        await controlled.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        after = await client.GetAsync(new WorkspaceRequest { WorkspaceId = state.WorkspaceId });
        Check(after.ProjectJson == edited.ProjectJson && after.Revision == 2, "cancellation propagates without mutation");
    }
    finally
    {
        controlled.Release.TrySetResult();
        await editorHost.StopAsync();
        await thinkingHost.StopAsync();
    }
}

public sealed class ControlledThinking : Thinking.ThinkingBase
{
    public TaskCompletionSource Started { get; private set; } = NewSignal();
    public TaskCompletionSource Release { get; private set; } = NewSignal();
    public TaskCompletionSource Cancelled { get; private set; } = NewSignal();
    public void Reset() { Started = NewSignal(); Release = NewSignal(); Cancelled = NewSignal(); }
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override async Task<ThinkingResult> Optimize(ThinkingRequest request, ServerCallContext context)
    {
        Started.TrySetResult();
        try { await Release.Task.WaitAsync(context.CancellationToken); }
        catch (OperationCanceledException) { Cancelled.TrySetResult(); throw; }
        return new ThinkingResult { ProjectJson = request.ProjectJson };
    }
}
