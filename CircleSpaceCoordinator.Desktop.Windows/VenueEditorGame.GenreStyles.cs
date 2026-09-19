namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Engine.Model;

public sealed partial class VenueEditorGame
{
    private void OpenGenreStyleEditor()
    {
        if (workspace is not { } targetWorkspace) return;
        var draft = new GenreStyleDraft(targetWorkspace.Project);
        OpenStyleMappingEditor(draft.Mapping, "ジャンルコード", "ジャンルが設定されたサークルはありません。",
            styles => targetWorkspace.Execute(new SetGenreStyles(styles.Select(style =>
                new GenreStyleDefinition(style.Key, style.PrimaryColor, style.SecondaryColor, style.Pattern)).ToArray()),
                selectedPlanEdit: false), targetWorkspace.Project.GenreStyleCredits);
    }
}
