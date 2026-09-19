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
            (styles, log, handle, date) => targetWorkspace.Execute(new SetBlockStyles(styles.Select(style =>
                new BlockStyleDefinition(style.Key, style.PrimaryColor, style.SecondaryColor, style.Pattern)).ToArray())
                { ChangeLog = log, ActorHandle = handle, WorkDate = date },
                selectedPlanEdit: false), targetWorkspace.Project.BlockStyleCredits);
    }
}
