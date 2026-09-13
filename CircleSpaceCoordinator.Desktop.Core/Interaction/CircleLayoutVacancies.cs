namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;

/// <summary>Counts physical seat cells, not area IDs or circles. A parked circle does not fill a seat.</summary>
public static class CircleLayoutVacancies
{
    public static IReadOnlySet<GridPosition> Find(CircleSpaceProject project, Plan plan)
    {
        var types = project.DeskTypes.ToDictionary(type => type.Id, StringComparer.Ordinal);
        var seats = plan.DeskPlacements.SelectMany(desk => desk.GetSeatCells(types[desk.DeskTypeId]))
            .Where(project.Venue.CanPlaceAt).ToHashSet();
        seats.ExceptWith(plan.Assignments.SelectMany(assignment => assignment.OccupiedCells));
        return seats;
    }
}
