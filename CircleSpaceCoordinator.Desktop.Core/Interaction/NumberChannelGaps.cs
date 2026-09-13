namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;

public sealed record NumberChannelGaps(int Blocks, int Frames, int Cells)
{
    public int this[int channel] => channel switch
    {
        0 => Blocks, 1 => Frames, 2 => Cells,
        _ => throw new ArgumentOutOfRangeException(nameof(channel)),
    };

    public static NumberChannelGaps Find(CircleSpaceProject project, Plan plan)
    {
        var types = project.DeskTypes.ToDictionary(type => type.Id, StringComparer.Ordinal);
        var labels = plan.SeatLabels.ToDictionary(label => (label.DeskPlacementId, label.RelativeCell));
        var blocks = 0;
        var frames = 0;
        var cells = 0;
        foreach (var desk in plan.DeskPlacements)
        {
            if (string.IsNullOrWhiteSpace(desk.DeskNumber)) frames++;
            var type = types[desk.DeskTypeId];
            // Labels are keyed by unrotated frame-relative coordinates.
            var seats = type.Space is { } space
                ? space.Cells.Where(cell => cell.Area > 0).Select(cell => new GridPosition(cell.X, cell.Y))
                : type.Footprint;
            foreach (var seat in seats)
            {
                var label = labels.GetValueOrDefault((desk.Id, seat));
                if (string.IsNullOrWhiteSpace(label?.BlockName)) blocks++;
                if (string.IsNullOrWhiteSpace(label?.SeatName)) cells++;
            }
        }
        return new(blocks, frames, cells);
    }
}
