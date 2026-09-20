namespace CircleSpaceCoordinator.Desktop.Core.Persistence;

using CircleSpaceCoordinator.Core.Model;

public sealed record EventProjectMetadata(bool Exists, bool Confidential, string? Error);

/// <summary>Process-local metadata. Listing cached entries never accesses project files.</summary>
public sealed class EventProjectMetadataCache(Func<string, CircleSpaceProject> load)
{
    private readonly Dictionary<string, EventProjectMetadata> entries = new(StringComparer.OrdinalIgnoreCase);
    public EventProjectMetadata? Peek(string path) => entries.GetValueOrDefault(Path.GetFullPath(path));
    public void Invalidate(string path) => entries.Remove(Path.GetFullPath(path));
    public EventProjectMetadata Inspect(string path)
    {
        path = Path.GetFullPath(path);
        if (entries.TryGetValue(path, out var cached) && cached.Exists && cached.Error is null) return cached;
        EventProjectMetadata result;
        try
        {
            var project = load(path);
            result = new(true, project.IsConfidential || project.DeskLayouts.Any(layout => layout.IsConfidential), null);
        }
        catch (Exception ex)
        {
            result = new(ex is not (FileNotFoundException or DirectoryNotFoundException), false, ex.Message);
        }
        return entries[path] = result;
    }
}
