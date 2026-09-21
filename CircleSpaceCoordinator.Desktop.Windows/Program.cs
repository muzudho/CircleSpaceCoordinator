namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Logging;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // StationeryDeveloperWindow starts this executable in inspector mode.
        // Handle it before treating the first argument as an event project or starting engines.
        if (args.Length > 0 && args[0] == "--stationery-inspector")
        {
            if (args.Length != 2 || !args[1].StartsWith("StationeryUI.Inspector.", StringComparison.Ordinal))
            {
                Environment.ExitCode = 1;
                return;
            }
            try
            {
                using var inspector = new StationeryInspectorGame(args[1]);
                inspector.Run();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                Environment.ExitCode = 1;
            }
            return;
        }
        var projectPath = args.Length > 0 ? args[0] : null;
        var logPath = Path.Combine(
            AppContext.BaseDirectory,
            "logs",
            $"ui-operations-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.jsonl");
        using var operationLogger = new JsonLinesOperationLogger(logPath);
        using var performance = new PerformanceRecorder(Path.Combine(AppContext.BaseDirectory, "logs",
            $"performance-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.jsonl"));
        try
        {
            using var game = new VenueEditorGame(null, operationLogger, projectPath, startEngines: true, performance: performance);
            game.Run();
        }
        catch (Exception exception)
        {
            // A graphics initialization failure cannot be drawn in the failed window.
            operationLogger.Log(new UiOperationLogEntry(DateTimeOffset.UtcNow,
                "application_failure_" + exception.GetType().Name, Success: false));
            Console.Error.WriteLine(exception);
            Environment.ExitCode = 1;
        }
    }

}
