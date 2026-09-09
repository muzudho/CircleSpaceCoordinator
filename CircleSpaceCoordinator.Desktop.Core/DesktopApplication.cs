namespace CircleSpaceCoordinator.Desktop.Core;

using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.EditorClient;

using StationeryUI.Text;

public sealed class DesktopApplication(
    IEditorWorkspace workspace,
    ITextCompositionService textComposition)
{
    public IEditorWorkspace Workspace { get; } = workspace ?? throw new ArgumentNullException(nameof(workspace));

    public ITextCompositionService TextComposition { get; } =
        textComposition ?? throw new ArgumentNullException(nameof(textComposition));

    public static RemoteWorkspace LoadWorkspace(string jsonPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonPath);
        var fullPath = Path.GetFullPath(jsonPath);
        return EditorConnection.Current.Open(File.ReadAllText(fullPath));
    }
}
