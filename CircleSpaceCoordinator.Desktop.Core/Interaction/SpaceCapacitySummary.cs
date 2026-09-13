namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Core.Model;

public sealed record SpaceCapacitySummary(long Requested, IReadOnlyDictionary<string, long> Layouts)
{
    public static SpaceCapacitySummary Calculate(CircleSpaceProject project)
    {
        var types = project.DeskTypes.ToDictionary(type => type.Id, StringComparer.Ordinal);
        long Count(IReadOnlyList<DeskPlacement> desks) => desks
            .SelectMany(desk => desk.GetSeatCells(types[desk.DeskTypeId]))
            .Where(project.Venue.CanPlaceAt).Distinct().LongCount();
        var layouts = project.DeskLayouts.Count > 0
            ? project.DeskLayouts.ToDictionary(layout => layout.Id, layout => Count(layout.DeskPlacements), StringComparer.Ordinal)
            : project.Plans.ToDictionary(plan => plan.Id, plan => Count(plan.DeskPlacements), StringComparer.Ordinal);
        return new(project.Participants.Sum(participant => (long)participant.RequiredCellCount), layouts);
    }
}
