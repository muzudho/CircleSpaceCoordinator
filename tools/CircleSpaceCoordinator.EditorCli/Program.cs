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
    var call = new CallOptions(deadline: DateTime.UtcNow.AddMinutes(121), cancellationToken: cancellation.Token);
    WorkspaceState result;
    switch (args[1])
    {
        case "describe" when args.Length == 2:
            Console.WriteLine(JsonFormatter.Default.Format(await client.DescribeAsync(new Empty(), call)));
            return 0;
        case "execute" when args.Length is 5 or 6:
            if (args.Length == 6 && args[5] != "--project") { Usage(); return 2; }
            result = await client.ExecuteAsync(new OperationRequest
            {
                WorkspaceId = args[2], ExpectedRevision = Revision(args[3]),
                OperationJson = await File.ReadAllTextAsync(args[4]), SelectedPlanEdit = args.Length == 5,
            }, call);
            break;
        case "select-plan" or "select-desk" when args.Length == 5:
            var selection = new SelectionRequest { WorkspaceId = args[2], ExpectedRevision = Revision(args[3]) };
            if (args[1] == "select-plan") selection.PlanId = args[4]; else selection.DeskLayoutId = args[4];
            result = await client.SelectAsync(selection, call);
            break;
        case "load" when args.Length == 5:
            result = await client.LoadAsync(new LoadRequest { WorkspaceId = args[2], ExpectedRevision = Revision(args[3]),
                ProjectJson = await File.ReadAllTextAsync(args[4]) }, call);
            break;
        case "watch" when args.Length == 3:
            using (var stream = client.WatchJob(new JobHandle { JobId = args[2] }, call))
            {
                await foreach (var item in stream.ResponseStream.ReadAllAsync(cancellation.Token))
                {
                    Console.WriteLine(JsonFormatter.Default.Format(item));
                    if (item.Completed && item.ErrorCode.Length > 0) return 1;
                }
            }
            return 0;
        case "stop" or "release-job" when args.Length == 3:
            var handle = new JobHandle { JobId = args[2] };
            if (args[1] == "stop") await client.StopJobAsync(handle, call); else await client.ReleaseJobAsync(handle, call);
            Console.WriteLine("{}");
            return 0;
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
        case "optimize" or "start" when args.Length == 9:
            var optimization = new EditorOptimizeRequest
            {
                WorkspaceId = args[2], ExpectedRevision = Revision(args[3]), SourcePlanId = args[4],
                NewPlanId = args[5], NewPlanName = args[6],
                Options = new OptimizationOptions
                {
                    TimeLimitMs = int.Parse(args[7], CultureInfo.InvariantCulture),
                    MaximumIterations = int.Parse(args[8], CultureInfo.InvariantCulture),
                },
            };
            if (args[1] == "start")
            {
                Console.WriteLine(JsonFormatter.Default.Format(await client.StartJobAsync(optimization, call)));
                return 0;
            }
            result = await client.OptimizeAsync(optimization, call);
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
    Console.Error.WriteLine("  start <workspace> <revision> <source-plan> <new-plan> <name> <time-ms> <iterations>");
    Console.Error.WriteLine("  watch|stop|release-job <job-id> | describe");
    Console.Error.WriteLine("  execute <workspace> <revision> <operation.json> [--project]");
    Console.Error.WriteLine("  select-plan|select-desk <workspace> <revision> <layout-id>");
    Console.Error.WriteLine("  load <workspace> <revision> <input.json>");
}
