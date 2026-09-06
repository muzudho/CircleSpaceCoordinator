namespace CircleSpaceCoordinator.StationeryUI.Text;

public interface ITextCompositionService
{
    bool IsComposing { get; }

    string CompositionText { get; }

    void StartTextInput();

    void StopTextInput();
}
