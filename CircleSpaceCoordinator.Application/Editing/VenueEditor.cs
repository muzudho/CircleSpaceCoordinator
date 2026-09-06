namespace CircleSpaceCoordinator.Application.Editing;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;

public static class VenueEditor
{
    public static CircleSpaceProject AddPillar(CircleSpaceProject project, GridPosition cell)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.Venue.Contains(cell) || project.Venue.BlockedCells.Contains(cell))
            return project;
        return ReplaceVenue(project, project.Venue with
        {
            BlockedCells = project.Venue.BlockedCells.Append(cell).ToHashSet(),
        });
    }

    public static CircleSpaceProject RemovePillar(CircleSpaceProject project, GridPosition cell)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.Venue.BlockedCells.Contains(cell))
            return project;
        return ReplaceVenue(project, project.Venue with
        {
            BlockedCells = project.Venue.BlockedCells.Where(item => item != cell).ToHashSet(),
        });
    }

    public static CircleSpaceProject Resize(CircleSpaceProject project, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Venue width must be positive.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Venue height must be positive.");

        return ReplaceVenue(project, project.Venue with { Width = width, Height = height });
    }

    private static CircleSpaceProject ReplaceVenue(CircleSpaceProject project, Venue venue)
    {
        var edited = project with { Venue = venue };
        var issues = ProjectValidator.Validate(edited);
        if (issues.Count > 0)
            throw new ProjectValidationException(issues);
        return edited;
    }
}
