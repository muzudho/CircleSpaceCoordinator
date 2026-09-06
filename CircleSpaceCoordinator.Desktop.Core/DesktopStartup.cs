namespace CircleSpaceCoordinator.Desktop.Core;

public static class DesktopStartup
{
    public const string FictionalExampleFileName = "circle-space-project-v1.example.json";

    public static string ResolveProjectPath(IReadOnlyList<string> args, string baseDirectory, string? lastProjectPath = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        if (args.Count > 0)
            return Path.GetFullPath(args[0]);
        if (!string.IsNullOrWhiteSpace(lastProjectPath) && File.Exists(lastProjectPath))
            return Path.GetFullPath(lastProjectPath);
        return Path.Combine(Path.GetFullPath(baseDirectory), "examples", FictionalExampleFileName);
    }
}
