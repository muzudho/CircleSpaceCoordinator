namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Infrastructure.Tabular;

public static class CircleSeatExportBuilder
{
    public const string UndefinedSpaceNumber = "#UNDEFINED_SPACE_NUMBER";
    public static Plan? GetExportPlan(CircleSpaceProject project) =>
        project.ExportPlanId is { } id ? project.Plans.FirstOrDefault(plan => plan.Id == id) : null;

    public static IReadOnlyList<CircleSeatExportRow> BuildDecided(CircleSpaceProject project) =>
        Build(project, GetExportPlan(project) ?? throw new InvalidOperationException("配置案が未決定です。［配置決定案を選択する］で選択してください。"));

    public static IReadOnlyList<DeskPlacement> FindMissingDeskNumbers(Plan plan) =>
        plan.DeskPlacements.Where(desk => string.IsNullOrWhiteSpace(desk.DeskNumber)).ToArray();

    public static IReadOnlyList<CircleSeatExportRow> Build(CircleSpaceProject project, Plan plan)
    {
        if (plan.TemporaryPlacements.Count != 0)
            throw new InvalidOperationException($"仮置きのサークルが {plan.TemporaryPlacements.Count} 件あります。フレームへ戻してから書き出してください。");
        var missing = FindMissingDeskNumbers(plan);
        if (missing.Count != 0)
            throw new InvalidOperationException($"フレーム番号が未設定のフレームが {missing.Count} 個あります。［フレーム配置］モードで全てのフレームに番号を設定してから書き出してください。\n{string.Join("、", missing.Select(desk => desk.Id))}");

        var deskTypes = project.DeskTypes.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var placements = plan.DeskPlacements.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var labelsByCell = plan.SeatLabels
            .Where(label => placements.ContainsKey(label.DeskPlacementId) && deskTypes.ContainsKey(placements[label.DeskPlacementId].DeskTypeId))
            .ToDictionary(
                label => placements[label.DeskPlacementId].Anchor + label.RelativeCell.Rotate(placements[label.DeskPlacementId].Orientation),
                label => label);
        var desksByCell = plan.DeskPlacements
            .Where(placement => deskTypes.ContainsKey(placement.DeskTypeId))
            .SelectMany(placement => deskTypes[placement.DeskTypeId].Footprint.Select(relative =>
                (Cell: placement.Anchor + relative.Rotate(placement.Orientation), Placement: placement)))
            .ToDictionary(item => item.Cell, item => item.Placement);
        var participants = project.Participants.ToDictionary(item => item.Id, StringComparer.Ordinal);
        return plan.Assignments
            .Where(assignment => participants.ContainsKey(assignment.ParticipantId))
            .Select(assignment =>
            {
                labelsByCell.TryGetValue(assignment.ScoringPosition, out var label);
                var participant = participants[assignment.ParticipantId];
                desksByCell.TryGetValue(assignment.ScoringPosition, out var desk);
                var fullFrame = desk is not null && assignment.OccupiedCells.SetEquals(desk.GetSeatCells(deskTypes[desk.DeskTypeId]));
                var seatName = fullFrame ? desk!.DeskNumber!
                    : assignment.OccupiedCells.Count == 1 && !string.IsNullOrWhiteSpace(label?.SeatName) ? label.SeatName : UndefinedSpaceNumber;
                return new CircleSeatExportRow(participant.CircleId, string.IsNullOrWhiteSpace(label?.BlockName) ? "#MISSING_BLOCK_NUMBER" : label.BlockName, seatName);
            })
            .ToArray();
    }
}
