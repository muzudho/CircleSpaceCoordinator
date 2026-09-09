using System.Globalization;
using System.Text;
using CircleSpaceCoordinator.Engine.Contracts.V1;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;

if (args.Length < 2)
{
    Usage();
    return 2;
}

try
{
    using var channel = GrpcChannel.ForAddress(args[0], new GrpcChannelOptions
    {
        MaxReceiveMessageSize = 32 * 1024 * 1024,
        MaxSendMessageSize = 32 * 1024 * 1024,
    });
    var client = new Editor.EditorClient(channel);
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
    var call = new CallOptions(deadline: DateTime.UtcNow.AddMinutes(11), cancellationToken: cancellation.Token);
    WorkspaceState result;
    switch (args[1])
    {
        case "open" when args.Length == 3:
            result = await client.OpenAsync(new OpenRequest { ProjectJson = await File.ReadAllTextAsync(args[2]) }, call);
            break;
        case "get" when args.Length == 3:
            result = await client.GetAsync(new WorkspaceRequest { WorkspaceId = args[2] }, call);
            break;
        case "rename" when args.Length == 6:
            result = await client.EditAsync(new EditRequest
            {
                WorkspaceId = args[2], ExpectedRevision = Revision(args[3]),
                RenamePlan = new RenamePlan { PlanId = args[4], Name = args[5] },
            }, call);
            break;
        case "undo" or "redo" when args.Length == 4:
            var edit = new EditRequest { WorkspaceId = args[2], ExpectedRevision = Revision(args[3]) };
            if (args[1] == "undo") edit.Undo = new Empty(); else edit.Redo = new Empty();
            result = await client.EditAsync(edit, call);
            break;
        case "optimize" when args.Length == 9:
            result = await client.OptimizeAsync(new EditorOptimizeRequest
            {
                WorkspaceId = args[2], ExpectedRevision = Revision(args[3]), SourcePlanId = args[4],
                NewPlanId = args[5], NewPlanName = args[6],
                Options = new OptimizationOptions
                {
                    TimeLimitMs = int.Parse(args[7], CultureInfo.InvariantCulture),
                    MaximumIterations = int.Parse(args[8], CultureInfo.InvariantCulture),
                },
            }, call);
            break;
        case "export" when args.Length == 4:
            result = await client.GetAsync(new WorkspaceRequest { WorkspaceId = args[2] }, call);
            await using (var stream = new FileStream(args[3], FileMode.CreateNew, FileAccess.Write))
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                await writer.WriteAsync(result.ProjectJson);
            break;
        case "close" when args.Length == 3:
            await client.CloseAsync(new WorkspaceRequest { WorkspaceId = args[2] }, call);
            Console.WriteLine("{}");
            return 0;
        default:
            Usage();
            return 2;
    }
    Console.WriteLine(JsonFormatter.Default.Format(result));
    return 0;
}
catch (RpcException exception)
{
    Console.Error.WriteLine($"{exception.StatusCode}: {exception.Status.Detail}");
    return 1;
}
catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException
    or IOException or UnauthorizedAccessException or OperationCanceledException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

static long Revision(string value) => long.Parse(value, CultureInfo.InvariantCulture);
static void Usage()
{
    Console.Error.WriteLine("Usage: EditorCli <address> <command> ...");
    Console.Error.WriteLine("  open <input.json> | get <workspace> | export <workspace> <new-output.json>");
    Console.Error.WriteLine("  rename <workspace> <revision> <plan-id> <name>");
    Console.Error.WriteLine("  undo|redo <workspace> <revision> | close <workspace>");
    Console.Error.WriteLine("  optimize <workspace> <revision> <source-plan> <new-plan> <name> <time-ms> <iterations>");
}
