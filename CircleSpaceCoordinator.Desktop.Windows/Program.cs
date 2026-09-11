namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Desktop.Core;
using CircleSpaceCoordinator.Desktop.Core.Logging;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.EditorClient;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        using var runtime = StartEngines();
        if (runtime is null) return;
        EditorConnection.Current = runtime.Connection;
        var settings = new ApplicationSettingsService(Path.Combine(AppContext.BaseDirectory, "application-settings.json"));
        var projectPath = SelectProjectPath(args, settings);
        if (projectPath is null)
            return;
        using var workspace = DesktopApplication.LoadWorkspace(projectPath);
        settings.RememberProject(projectPath);
        var logPath = Path.Combine(
            AppContext.BaseDirectory,
            "logs",
            $"ui-operations-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.jsonl");
        using var operationLogger = new JsonLinesOperationLogger(logPath);
        using var game = new VenueEditorGame(workspace, operationLogger, projectPath, settings);
        game.Run();
    }

    private static EngineRuntime? StartEngines()
    {
        try { return EngineRuntime.StartAsync(AppContext.BaseDirectory).GetAwaiter().GetResult(); }
        catch (Exception exception)
        {
            System.Windows.Forms.MessageBox.Show(exception.Message, "エンジンの起動に失敗しました",
                System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            return null;
        }
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
