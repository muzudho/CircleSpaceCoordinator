namespace CircleSpaceCoordinator.Desktop;

using CircleSpaceCoordinator.Desktop.Logging;
using CircleSpaceCoordinator.Desktop.Persistence;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        var settings = new ApplicationSettingsService(Path.Combine(AppContext.BaseDirectory, "application-settings.json"));
        var projectPath = SelectProjectPath(args, settings);
        if (projectPath is null)
            return;
        var workspace = DesktopApplication.LoadWorkspace(projectPath);
        settings.RememberProject(projectPath);
        var logPath = Path.Combine(
            AppContext.BaseDirectory,
            "logs",
            $"ui-operations-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.jsonl");
        using var operationLogger = new JsonLinesOperationLogger(logPath);
        using var game = new VenueEditorGame(workspace, operationLogger, projectPath, settings);
        game.Run();
    }

    private static string? SelectProjectPath(string[] args, ApplicationSettingsService settings)
    {
        if (args.Length > 0)
        {
            var path = Path.GetFullPath(args[0]);
            new EventProjectCatalogService(settings).Register(path);
            return path;
        }

        using var selector = new EventProjectSelectorForm(settings);
        return selector.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? selector.SelectedProjectPath
            : null;
    }
}
