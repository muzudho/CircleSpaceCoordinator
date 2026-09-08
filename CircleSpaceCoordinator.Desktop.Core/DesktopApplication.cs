namespace CircleSpaceCoordinator.Desktop.Core;

using CircleSpaceCoordinator.Application.Workspace;
using CircleSpaceCoordinator.Infrastructure.Json;
using StationeryUI.Text;

public sealed class DesktopApplication(
    ProjectWorkspace workspace,
    ITextCompositionService textComposition)
{
    public ProjectWorkspace Workspace { get; } = workspace ?? throw new ArgumentNullException(nameof(workspace));

    public ITextCompositionService TextComposition { get; } =
        textComposition ?? throw new ArgumentNullException(nameof(textComposition));

    public static ProjectWorkspace LoadWorkspace(string jsonPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonPath);
        var fullPath = Path.GetFullPath(jsonPath);
        var project = ProjectJsonSerializer.Load(File.ReadAllText(fullPath));
        return new ProjectWorkspace(project);
    }
}
