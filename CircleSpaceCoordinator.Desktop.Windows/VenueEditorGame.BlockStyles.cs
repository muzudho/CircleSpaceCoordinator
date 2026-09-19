namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Engine.Model;

public sealed partial class VenueEditorGame
{
    private void OpenBlockStyleEditor()
    {
        if (workspace is not { } targetWorkspace) return;
        var draft = new BlockStyleDraft(targetWorkspace.Project);
        OpenStyleMappingEditor(draft.Mapping, "ブロック番号", "ブロック番号がありません。セルにブロック番号を入力してください。",
            styles => targetWorkspace.Execute(new SetBlockStyles(styles.Select(style =>
                new BlockStyleDefinition(style.Key, style.PrimaryColor, style.SecondaryColor, style.Pattern)).ToArray()),
                selectedPlanEdit: false), targetWorkspace.Project.BlockStyleCredits);
    }
}
