namespace CircleSpaceCoordinator.Engine.Contracts;

using System.Diagnostics;

/// <summary>Watch the owning process before host initialization, including failures during startup.</summary>
public static class ParentProcessLifetime
{
    public static void Watch(string[] args)
    {
        var index = Array.IndexOf(args, "--parent-pid");
        if (index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var parentId))
            _ = WatchAsync(parentId);
    }

    private static async Task WatchAsync(int parentId)
    {
        try
        {
            using var parent = Process.GetProcessById(parentId);
            await parent.WaitForExitAsync();
        }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
        finally { Environment.Exit(0); }
    }
}
