namespace CircleSpaceCoordinator.Application.Workspace;

using System.Text.Json;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Engine.Model;

/// <summary>Records the last content edit, in the same undo transaction as the edit.</summary>
public static class ModificationCreditsService
{
    public static CircleSpaceProject Apply(CircleSpaceProject before, CircleSpaceProject after, EditorOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.ActorHandle) || operation is ImportPortableSelection or ImportFrameLayout or RecordPortableProviders)
            return after;
        return Apply(before, after, operation.ActorHandle, operation.WorkDate, operation.ChangeLog);
    }

    public static CircleSpaceProject Apply(CircleSpaceProject before, CircleSpaceProject after, string? actorHandle, DateOnly? workDate = null,
        string? changeLog = null)
    {
        if (string.IsNullOrWhiteSpace(actorHandle)) return after;
        var handle = PersonCredits.NormalizeHandle(actorHandle);
        var date = workDate ?? DateOnly.FromDateTime(DateTime.Now);
        PersonCredits Edited(PersonCredits? previous) => (previous ?? new()).WrittenBy(handle, date, changeLog);
        var changedCircleIds = after.Plans.Where(plan =>
        {
            var previous = before.Plans.FirstOrDefault(item => item.Id == plan.Id);
            return previous is null || Changed(
                new { previous.Name, previous.Description, previous.Assignments, previous.TemporaryPlacements },
                new { plan.Name, plan.Description, plan.Assignments, plan.TemporaryPlacements });
        }).Select(item => item.Id).ToHashSet();
        var changedDeskIds = after.Plans.Where(plan =>
        {
            var previous = before.Plans.FirstOrDefault(item => item.Id == plan.Id);
            return previous is not null && Changed(
                new { previous.DeskPlacements, previous.SeatLabels, previous.IslandStarts, previous.IslandConnectors, previous.DisabledIslandConnections, previous.FacingRegions },
                new { plan.DeskPlacements, plan.SeatLabels, plan.IslandStarts, plan.IslandConnectors, plan.DisabledIslandConnections, plan.FacingRegions });
        }).Select(plan => after.CircleLayouts.FirstOrDefault(item => item.Id == plan.Id)?.DeskLayoutId).ToHashSet();
        return after with
        {
            GenreStyleCredits = Changed(before.GenreStyles, after.GenreStyles) || before.GenreStyleComment != after.GenreStyleComment
                ? Edited(before.GenreStyleCredits) : before.GenreStyleCredits,
            BlockStyleCredits = Changed(before.BlockStyles, after.BlockStyles) ? Edited(before.BlockStyleCredits) : before.BlockStyleCredits,
            CircleLayouts = after.CircleLayouts.Select(item =>
            {
                var previous = before.CircleLayouts.FirstOrDefault(old => old.Id == item.Id);
                return changedCircleIds.Contains(item.Id) || Changed(previous, item)
                    ? item with { Credits = Edited(item.Credits) } : item;
            }).ToArray(),
            DeskLayouts = after.DeskLayouts.Select(item =>
            {
                var previous = before.DeskLayouts.FirstOrDefault(old => old.Id == item.Id);
                return changedDeskIds.Contains(item.Id) || Changed(previous, item)
                    ? item with { Credits = Edited(item.Credits) } : item;
            }).ToArray(),
            Venue = Changed(before.Venue, after.Venue) ? after.Venue with { Credits = Edited(after.Venue.Credits) } : after.Venue,
        };
    }

    private static bool Changed<T>(T before, T after) =>
        !EqualityComparer<T>.Default.Equals(before, after) && JsonSerializer.Serialize(before) != JsonSerializer.Serialize(after);
}
