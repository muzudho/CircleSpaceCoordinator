namespace CircleSpaceCoordinator.EditorEngine;

using CircleSpaceCoordinator.Engine.Model;
using Grpc.Core;

public sealed record SavedSession(long Revision, WorkspaceCheckpoint Workspace);

public sealed class SessionRepository(IConfiguration configuration)
{
    private readonly string? directory = configuration["state-directory"];
    public bool Enabled => directory is not null;
    public SavedSession? Load(string id)
    {
        if (directory is null) return null;
        var path = GetPath(id);
        return File.Exists(path) ? WireJson.Read<SavedSession>(File.ReadAllText(path)) : null;
    }
    public void Save(string id, SavedSession session, long persistedRevision)
    {
        if (directory is null || session.Revision == persistedRevision) return;
        Directory.CreateDirectory(directory);
        var path = GetPath(id);
        using var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var previous = Load(id);
        if ((previous?.Revision ?? 0) != persistedRevision)
            throw new RpcException(new Status(StatusCode.Aborted, "The persisted session changed in another server."));
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, WireJson.Write(session));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void Delete(string id)
    {
        if (directory is null) return;
        var path = GetPath(id);
        if (!File.Exists(path)) return;
        using var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        File.Delete(path);
    }
    private string GetPath(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new RpcException(new Status(StatusCode.NotFound, "Unknown workspace."));
        return Path.Combine(directory!, id + ".json");
    }
}
