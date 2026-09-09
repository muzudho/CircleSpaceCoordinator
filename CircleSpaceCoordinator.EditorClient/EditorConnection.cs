namespace CircleSpaceCoordinator.EditorClient;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;
using CircleSpaceCoordinator.Engine.Contracts.V1;
using CircleSpaceCoordinator.Engine.Model;
using Grpc.Core;
using Grpc.Net.Client;

public sealed class EditorConnection : IDisposable
{
    public static EditorConnection Current { get; set; } = null!;
    private readonly GrpcChannel channel;
    public Editor.EditorClient Client { get; }
    public EditorConnection(string address)
    {
        channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
        { MaxReceiveMessageSize = 32 * 1024 * 1024, MaxSendMessageSize = 32 * 1024 * 1024 });
        Client = new Editor.EditorClient(channel);
    }
    public RemoteWorkspace Open(string json) => new(this, Invoke(() => Client.Open(new OpenRequest { ProjectJson = json }, Deadline())));
    public CircleSpaceProject Decode(string json) => WireJson.Read<CircleSpaceProject>(Invoke(() => Client.ConvertDocument(
        new DocumentRequest { ProjectJson = json }, Deadline())).ModelJson);
    public string Encode(CircleSpaceProject project) => Invoke(() => Client.ConvertDocument(
        new DocumentRequest { ModelJson = WireJson.Write(project) }, Deadline())).ProjectJson;
    public static CallOptions Deadline() => new(deadline: DateTime.UtcNow.AddSeconds(15));
    internal static T Invoke<T>(Func<T> action)
    {
        try { return action(); }
        catch (RpcException exception)
        {
            var issues = exception.Trailers.GetValue("validation-json");
            if (issues is not null) throw new ProjectValidationException(WireJson.Read<ValidationIssue[]>(issues));
            throw new InvalidOperationException($"エディターエンジン: {exception.StatusCode}: {exception.Status.Detail}", exception);
        }
    }
    public void Dispose() => channel.Dispose();
}
