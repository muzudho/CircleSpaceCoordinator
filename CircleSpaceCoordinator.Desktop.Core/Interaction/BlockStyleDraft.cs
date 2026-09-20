namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Core.Model;

/// <summary>Collects distinct block numbers across the event for the shared mapping editor.</summary>
public sealed class BlockStyleDraft
{
    public StyleMappingDraft Mapping { get; }

    public BlockStyleDraft(CircleSpaceProject project)
    {
        Mapping = ShadingTable.CreateDraft(project, blocks: true);
    }
}
