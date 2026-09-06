namespace CircleSpaceCoordinator.Desktop.Core.Logging;

using System.Text.Json;

public sealed record UiOperationLogEntry(
    DateTimeOffset TimestampUtc,
    string Action,
    int? ScreenX = null,
    int? ScreenY = null,
    int? GridX = null,
    int? GridY = null,
    bool? Success = null,
    string? Detail = null);

public interface IOperationLogger
{
    void Log(UiOperationLogEntry entry);
}

public static class UiOperationLogFormatter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Serialize(UiOperationLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        // Free-form details can contain names, circle IDs, file paths or
        // exception messages. Do not persist them, regardless of project flags.
        return JsonSerializer.Serialize(entry with { Detail = null }, Options);
    }
}

public sealed class JsonLinesOperationLogger : IOperationLogger, IDisposable
{
    private readonly StreamWriter writer;
    private readonly object sync = new();
    private bool disposed;

    public JsonLinesOperationLogger(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("The operation log path has no parent directory."));
        writer = new StreamWriter(fullPath, append: true) { AutoFlush = true };
    }

    public void Log(UiOperationLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (sync)
        {
            if (disposed)
                return;
            try
            {
                writer.WriteLine(UiOperationLogFormatter.Serialize(entry));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
                return;
            disposed = true;
            writer.Dispose();
        }
    }
}

public sealed class NullOperationLogger : IOperationLogger
{
    public static NullOperationLogger Instance { get; } = new();

    private NullOperationLogger()
    {
    }

    public void Log(UiOperationLogEntry entry)
    {
    }
}
