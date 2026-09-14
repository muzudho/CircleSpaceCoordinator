namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core.Logging;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var projectPath = args.Length > 0 ? args[0] : null;
        var logPath = Path.Combine(
            AppContext.BaseDirectory,
            "logs",
            $"ui-operations-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.jsonl");
        using var operationLogger = new JsonLinesOperationLogger(logPath);
        try
        {
            using var game = new VenueEditorGame(null, operationLogger, projectPath, startEngines: true);
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
