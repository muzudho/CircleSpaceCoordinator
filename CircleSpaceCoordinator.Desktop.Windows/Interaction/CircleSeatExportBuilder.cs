namespace CircleSpaceCoordinator.Desktop.Windows.Interaction;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Infrastructure.Tabular;

public static class CircleSeatExportBuilder
{
    public static IReadOnlyList<DeskPlacement> FindMissingDeskNumbers(Plan plan) =>
        plan.DeskPlacements.Where(desk => string.IsNullOrWhiteSpace(desk.DeskNumber)).ToArray();

    public static IReadOnlyList<CircleSeatExportRow> Build(CircleSpaceProject project, Plan plan)
    {
        if (plan.TemporaryPlacements.Count != 0)
            throw new InvalidOperationException($"仮置きのサークルが {plan.TemporaryPlacements.Count} 件あります。机へ戻してから書き出してください。");
        var missing = FindMissingDeskNumbers(plan);
        if (missing.Count != 0)
            throw new InvalidOperationException($"机番号が未設定の机が {missing.Count} 台あります。［机配置］モードで全ての机に机番号を設定してから書き出してください。\n{string.Join("、", missing.Select(desk => desk.Id))}");

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
            .Where(assignment => participants.ContainsKey(assignment.ParticipantId) && labelsByCell.ContainsKey(assignment.ScoringPosition))
            .Select(assignment =>
            {
                var label = labelsByCell[assignment.ScoringPosition];
                var participant = participants[assignment.ParticipantId];
                var seatName = participant.RequiredCellCount == 2
                    ? desksByCell[assignment.ScoringPosition].DeskNumber!
                    : label.SeatName;
                return new CircleSeatExportRow(participant.CircleId, label.BlockName, seatName);
            })
            .ToArray();
    }
}
