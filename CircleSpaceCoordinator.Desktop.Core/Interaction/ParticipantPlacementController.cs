namespace CircleSpaceCoordinator.Desktop.Core.Interaction;


using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Validation;

public sealed class ParticipantPlacementController(IEditorWorkspace workspace)
{
    private string? selectedParticipantId;

    public string? SelectedParticipantId
    {
        get
        {
            ReconcileSelection();
            return selectedParticipantId;
        }
    }

    public string? SelectedParticipantName
    {
        get
        {
            var selectedId = SelectedParticipantId;
            return selectedId is null
                ? null
                : workspace.Project.Participants.Single(participant => participant.Id == selectedId).DisplayName;
        }
    }

    public bool SelectParticipant(string participantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(participantId);
        if (workspace.GetSelectedPlanSnapshot().UnassignedParticipants.All(item => item.ParticipantId != participantId))
            return false;
        selectedParticipantId = participantId;
        return true;
    }

    public void CycleUnassigned(int direction)
    {
        var unassigned = workspace.GetSelectedPlanSnapshot().UnassignedParticipants;
        if (unassigned.Count == 0)
        {
            selectedParticipantId = null;
            return;
        }

        var currentIndex = selectedParticipantId is null
            ? -1
            : unassigned.ToList().FindIndex(participant => participant.ParticipantId == selectedParticipantId);
        var step = direction == 0 ? 0 : Math.Sign(direction);
        var nextIndex = step == 0
            ? Math.Max(currentIndex, 0)
            : (currentIndex + step + unassigned.Count) % unassigned.Count;
        selectedParticipantId = unassigned[nextIndex].ParticipantId;
    }

    public EditorCommandResult AssignSelectedAt(GridPosition scoringPosition)
    {
        var participantId = SelectedParticipantId;
        if (participantId is null)
            return EditorCommandResult.NoTarget;
        return PlaceParticipantAt(participantId, scoringPosition);
    }

    public EditorCommandResult PlaceParticipantAt(string participantId, GridPosition scoringPosition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(participantId);
        if (!workspace.HasSelectedCircleLayout)
            return EditorCommandResult.NoTarget;

        var snapshot = workspace.GetSelectedPlanSnapshot();
        var desk = snapshot.Desks.LastOrDefault(item => item.OccupiedCells.Contains(scoringPosition));
        if (desk is null)
            return EditorCommandResult.NoTarget;

        var participant = workspace.Project.Participants.Single(item => item.Id == participantId);
        var cells = desk.OccupiedCells
            .OrderByDescending(cell => cell == scoringPosition)
            .ThenBy(cell => cell.Y)
            .ThenBy(cell => cell.X)
            .Take(participant.RequiredCellCount)
            .ToHashSet();
        if (cells.Count != participant.RequiredCellCount)
        {
            return new EditorCommandResult(false,
            [
                new ValidationIssue(
                    "assignment.desk.capacity",
                    $"plans[{workspace.SelectedPlanId}].assignments",
                    "The selected desk does not have enough cells for the participant."),
            ]);
        }

        try
        {
            var existing = workspace.SelectedPlan.Assignments
                .SingleOrDefault(item => item.ParticipantId == participantId);
            var temporary = workspace.SelectedPlan.TemporaryPlacements.SingleOrDefault(item => item.ParticipantId == participantId);
            workspace.Execute( existing is null
                ? new ParticipantAssignmentEditorAssign( workspace.SelectedPlanId, participantId, cells, scoringPosition, temporary?.CombinedSpaceId)
                : new ParticipantAssignmentEditorReassign(
                    workspace.SelectedPlanId,
                    participantId,
                    cells,
                    scoringPosition,
                    existing.CombinedSpaceId));
            ReconcileSelection();
            return EditorCommandResult.Success;
        }
        catch (ProjectValidationException exception)
        {
            return new EditorCommandResult(false, exception.Issues);
        }
    }

    public EditorCommandResult UnassignAt(GridPosition cell)
    {
        var assignment = workspace.GetSelectedPlanSnapshot().Assignments
            .LastOrDefault(item => item.OccupiedCells.Contains(cell));
        if (assignment is null)
            return EditorCommandResult.NoTarget;

        workspace.Execute(
            new ParticipantAssignmentEditorUnassign( workspace.SelectedPlanId, assignment.ParticipantId));
        ReconcileSelection();
        return EditorCommandResult.Success;
    }

    public EditorCommandResult SwapParticipants(string firstParticipantId, string secondParticipantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstParticipantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secondParticipantId);
        if (firstParticipantId == secondParticipantId)
            return EditorCommandResult.NoTarget;
        try
        {
            if (workspace.SelectedPlan.TemporaryPlacements.Any(item => item.ParticipantId == firstParticipantId || item.ParticipantId == secondParticipantId))
            {
                workspace.Execute( new TemporaryPlacementEditorSwap( workspace.SelectedPlanId, firstParticipantId, secondParticipantId));
                ReconcileSelection();
                return EditorCommandResult.Success;
            }
            var snapshot = workspace.GetSelectedPlanSnapshot();
            var firstGroup = GetCombinedGroup(firstParticipantId);
            var firstAssignment = snapshot.Assignments.Single(item => item.ParticipantId == firstParticipantId);
            if (firstGroup.Length == 1 && firstAssignment.OccupiedCells.Count == 1)
            {
                workspace.Execute( new ParticipantAssignmentEditorSwap( workspace.SelectedPlanId, firstParticipantId, secondParticipantId));
                ReconcileSelection();
                return EditorCommandResult.Success;
            }
            var targetAssignment = snapshot.Assignments.Single(item => item.ParticipantId == secondParticipantId);
            var targetDesk = snapshot.Desks.SingleOrDefault(desk => desk.OccupiedCells.Contains(targetAssignment.ScoringPosition));
            var secondGroup = targetDesk is null
                ? [secondParticipantId]
                : snapshot.Assignments
                    .Where(assignment => assignment.OccupiedCells.Any(targetDesk.OccupiedCells.Contains))
                    .Select(assignment => assignment.ParticipantId)
                    .ToArray();

            if (firstGroup.Intersect(secondGroup, StringComparer.Ordinal).Any())
            {
                // Dropping one member of an approved combined circle onto its
                // partner reverses their two cells without moving the pair.
                workspace.Execute( new ParticipantAssignmentEditorSwap( workspace.SelectedPlanId, firstParticipantId, secondParticipantId));
                ReconcileSelection();
                return EditorCommandResult.Success;
            }

            var swapsWholeDesk = firstGroup.Length > 1 || firstAssignment.OccupiedCells.Count > 1;
            workspace.Execute( swapsWholeDesk
                ? new ParticipantAssignmentEditorSwapGroups( workspace.SelectedPlanId, firstGroup, secondGroup)
                : new ParticipantAssignmentEditorSwap( workspace.SelectedPlanId, firstParticipantId, secondParticipantId));
            ReconcileSelection();
            return EditorCommandResult.Success;
        }
        catch (Exception exception) when (exception is ProjectValidationException or InvalidOperationException)
        {
            return exception is ProjectValidationException validation
                ? new EditorCommandResult(false, validation.Issues)
                : new EditorCommandResult(false,
                [
                    new ValidationIssue(
                        "assignment.groupSwap.capacity",
                        $"plans[{workspace.SelectedPlanId}].assignments",
                        exception.Message),
                ]);
        }
    }

    public EditorCommandResult SwapCellRegions(
        GridPosition sourceTopLeft,
        GridPosition destinationTopLeft,
        int width,
        int height)
    {
        try
        {
            workspace.Execute( new TemporaryPlacementEditorSwapRegions( workspace.SelectedPlanId, sourceTopLeft, destinationTopLeft, width, height));
            ReconcileSelection();
            return EditorCommandResult.Success;
        }
        catch (ProjectValidationException exception)
        {
            return new EditorCommandResult(false, exception.Issues);
        }
    }

    public EditorCommandResult ParkParticipantAt(string participantId, IReadOnlyList<GridPosition> displayCells,
        GridPosition originalPosition, GridPosition destination)
    {
        if (!workspace.HasSelectedCircleLayout) return EditorCommandResult.NoTarget;
        var delta = new GridPosition(destination.X - originalPosition.X, destination.Y - originalPosition.Y);
        try
        {
            workspace.Execute( new TemporaryPlacementEditorPark( workspace.SelectedPlanId, participantId,
                displayCells.Select(cell => cell + delta).ToHashSet(), destination));
            ReconcileSelection();
            return EditorCommandResult.Success;
        }
        catch (ProjectValidationException exception)
        {
            return new EditorCommandResult(false, exception.Issues);
        }
    }

    private string[] GetCombinedGroup(string participantId)
    {
        var assignments = workspace.SelectedPlan.Assignments;
        var assignment = assignments.Single(item => item.ParticipantId == participantId);
        if (assignment.CombinedSpaceId is { } combinedSpaceId)
            return assignments.Where(item => item.CombinedSpaceId == combinedSpaceId)
                .Select(item => item.ParticipantId).ToArray();

        var participant = workspace.Project.Participants.Single(item => item.Id == participantId);
        if (participant.CombinedWithCircleId is not { } partnerCircleId)
            return [participantId];
        var partner = workspace.Project.Participants.SingleOrDefault(item => item.CircleId == partnerCircleId);
        return partner is not null && assignments.Any(item => item.ParticipantId == partner.Id)
            ? [participantId, partner.Id]
            : [participantId];
    }

    private void ReconcileSelection()
    {
        var unassigned = workspace.GetSelectedPlanSnapshot().UnassignedParticipants;
        if (selectedParticipantId is null || unassigned.All(item => item.ParticipantId != selectedParticipantId))
            selectedParticipantId = unassigned.FirstOrDefault()?.ParticipantId;
    }
}
