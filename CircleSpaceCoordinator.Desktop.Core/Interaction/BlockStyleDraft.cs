namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Core.Model;

/// <summary>Collects distinct block numbers across the event for the shared mapping editor.</summary>
public sealed class BlockStyleDraft
{
    public StyleMappingDraft Mapping { get; }

    public BlockStyleDraft(CircleSpaceProject project)
    {
        var labels = project.DeskLayouts.Count > 0
            ? project.DeskLayouts.SelectMany(layout => layout.SeatLabels)
            : project.Plans.SelectMany(plan => plan.SeatLabels);
        Mapping = new StyleMappingDraft(labels.Select(label => label.BlockName),
            project.BlockStyles.Select(style => new StyleMappingEntry(style.BlockNumber,
                style.PrimaryColor, style.SecondaryColor, style.Pattern)));
    }
}
