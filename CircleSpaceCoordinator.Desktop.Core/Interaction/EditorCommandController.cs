namespace CircleSpaceCoordinator.Desktop.Core.Interaction;


using CircleSpaceCoordinator.Application.Plans;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;
using CircleSpaceCoordinator.OptimizationEngine;

public sealed record EditorCommandResult(
    bool Applied,
    IReadOnlyList<ValidationIssue> Issues,
    QuarterTurn? AffectedOrientation = null)
{
    public static EditorCommandResult Success { get; } = new(true, []);
    public static EditorCommandResult NoTarget { get; } = new(false, []);
}

public sealed class EditorCommandController(IEditorWorkspace workspace)
{
    public void CyclePlan(int direction)
    {
        if (direction == 0)
            return;

        var plans = workspace.RankPlans();
        var currentIndex = plans.ToList().FindIndex(plan => plan.PlanId == workspace.SelectedPlanId);
        var nextIndex = (currentIndex + Math.Sign(direction) + plans.Count) % plans.Count;
        workspace.SelectPlan(plans[nextIndex].PlanId);
    }

    public EditorCommandResult DuplicateSelectedPlan() => DuplicateSelectedPlan($"{workspace.SelectedPlan.Name}2");

    public EditorCommandResult DuplicateSelectedPlan(string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        var source = workspace.SelectedPlan;
        var usedIds = workspace.Project.Plans.Select(plan => plan.Id).ToHashSet(StringComparer.Ordinal);
        var revision = 1;
        string newId;
        do
        {
            newId = $"{source.Id}-revision-{revision++}";
        }
        while (usedIds.Contains(newId));
        var result = Apply( new PlanCatalogServiceDuplicatePlan(
            source.Id,
            newId,
            newName.Trim()));
        if (result.Applied)
            workspace.SelectPlan(newId);
        return result;
    }

    public EditorCommandResult RenameSelectedPlan(string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        return Apply( new PlanCatalogServiceRenamePlan(
            workspace.SelectedPlanId,
            newName.Trim()));
    }

    public EditorCommandResult CopyDeskLayout(string sourcePlanId, string destinationPlanId) => Apply(
        new PlanCatalogServiceCopyDeskLayout( sourcePlanId, destinationPlanId));

    public EditorCommandResult AddOptimizedPlan(CirclePlacementOptimizationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var source = workspace.SelectedPlan;
        var usedIds = workspace.Project.Plans.Select(plan => plan.Id).ToHashSet(StringComparer.Ordinal);
        var revision = 1;
        string id;
        do
        {
            id = $"{source.Id}-optimized-{revision++}";
        }
        while (usedIds.Contains(id));
        var name = $"{source.Name} 自動最適化";
        var resultCommand = Apply( new PlanCatalogServiceAddOptimizedPlan( result.BestPlan, id, name));
        if (resultCommand.Applied)
            workspace.SelectPlan(id);
        return resultCommand;
    }

    public bool Undo()
    {
        if (!workspace.CanUndo)
            return false;
        workspace.Undo();
        return true;
    }

    public bool Redo()
    {
        if (!workspace.CanRedo)
            return false;
        workspace.Redo();
        return true;
    }

    public EditorCommandResult RotateDeskAt(GridPosition cell, bool clockwise)
    {
        var desk = workspace.GetSelectedPlanSnapshot().Desks
            .LastOrDefault(item => item.OccupiedCells.Contains(cell));
        if (desk is null)
            return EditorCommandResult.NoTarget;

        var orientation = clockwise
            ? (QuarterTurn)(((int)desk.Orientation + 1) % 4)
            : (QuarterTurn)(((int)desk.Orientation + 3) % 4);
        try
        {
            workspace.Execute(
                new PlanDeskEditorRotateDesk( workspace.SelectedPlanId, desk.Id, orientation));
            return new EditorCommandResult(true, [], orientation);
        }
        catch (ProjectValidationException exception)
        {
            return new EditorCommandResult(false, exception.Issues);
        }
    }

    public EditorCommandResult AddDeskAt(GridPosition anchor, QuarterTurn orientation = QuarterTurn.North)
    {
        var deskType = workspace.Project.DeskTypes.FirstOrDefault();
        if (deskType is null)
            return EditorCommandResult.NoTarget;
        var usedIds = workspace.SelectedPlan.DeskPlacements.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var number = 1;
        string id;
        do
        {
            id = $"desk-{number++:0000}";
        }
        while (usedIds.Contains(id));

        var result = Apply( new PlanDeskEditorAddDesk(
            workspace.SelectedPlanId,
            new DeskPlacement(id, deskType.Id, anchor, orientation)));
        return result.Applied ? result with { AffectedOrientation = orientation } : result;
    }

    public EditorCommandResult RemoveDeskAt(GridPosition cell)
    {
        var desk = workspace.GetSelectedPlanSnapshot().Desks.LastOrDefault(item => item.OccupiedCells.Contains(cell));
        return desk is null
            ? EditorCommandResult.NoTarget
            : Apply( new PlanDeskEditorRemoveDesk( workspace.SelectedPlanId, desk.Id));
    }

    public EditorCommandResult SetDeskNumber(string deskPlacementId, string? deskNumber) => Apply(
        new PlanDeskEditorSetDeskNumber( workspace.SelectedPlanId, deskPlacementId, deskNumber));

    public EditorCommandResult FillDesks()
    {
        var deskType = workspace.Project.DeskTypes.FirstOrDefault();
        return deskType is null
            ? EditorCommandResult.NoTarget
            : Apply( new DeskLayoutServiceFillAvailableCells(
                workspace.SelectedPlanId,
                deskType.Id));
    }

    public EditorCommandResult ResizeVenue(int widthDelta, int heightDelta) => Apply(
        new VenueEditorResize(
            workspace.Project.Venue.Width + widthDelta,
            workspace.Project.Venue.Height + heightDelta));

    public EditorCommandResult AddPillarAt(GridPosition cell) => Apply(
        new VenueEditorAddPillar( cell));

    public EditorCommandResult RemovePillarAt(GridPosition cell) => Apply(
        new VenueEditorRemovePillar( cell));

    public EditorCommandResult SetSeatLabel(
        string deskPlacementId,
        GridPosition relativeCell,
        string blockName,
        string seatName) => Apply(
        new DeskSeatLabelEditorSetLabel( workspace.SelectedPlanId, deskPlacementId, relativeCell, blockName, seatName));

    public EditorCommandResult RemoveSeatLabel(string deskPlacementId, GridPosition relativeCell) => Apply(
        new DeskSeatLabelEditorRemoveLabel( workspace.SelectedPlanId, deskPlacementId, relativeCell));

    public EditorCommandResult ReplaceSeatLabels(IReadOnlyList<DeskSeatLabel> labels) => Apply(
        new DeskSeatLabelEditorReplaceLabels( workspace.SelectedPlanId, labels));

    public EditorCommandResult AddIslandConnector(
        string firstDeskId, string secondDeskId, GridPosition firstCell, GridPosition secondCell) => Apply(
        new VenueTopologyEditorAddConnector( workspace.SelectedPlanId, firstDeskId, secondDeskId, firstCell, secondCell));

    public EditorCommandResult AddFacingRegion(GridPosition first, GridPosition second) => Apply(
        new VenueTopologyEditorAddFacingRegion( workspace.SelectedPlanId, first, second));

    public EditorCommandResult ToggleAutomaticIslandConnection(GridPosition first, GridPosition second) => Apply(
        new VenueTopologyEditorToggleAutomaticConnection( workspace.SelectedPlanId, first, second));

    public EditorCommandResult RemoveIslandConnector(string connectorId) => Apply(
        new VenueTopologyEditorRemoveConnector( workspace.SelectedPlanId, connectorId));

    public EditorCommandResult RemoveTopologyAt(GridPosition cell)
    {
        var deskId = workspace.GetSelectedPlanSnapshot().Desks
            .LastOrDefault(item => item.OccupiedCells.Contains(cell))?.Id;
        return Apply( new VenueTopologyEditorRemoveAt( workspace.SelectedPlanId, cell, deskId));
    }

    private EditorCommandResult Apply(EditorOperation edit)
    {
        try
        {
            workspace.Execute(edit);
            return EditorCommandResult.Success;
        }
        catch (ProjectValidationException exception)
        {
            return new EditorCommandResult(false, exception.Issues);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return new EditorCommandResult(false,
            [
                new ValidationIssue("venue.size", "venue", exception.Message),
            ]);
        }
    }
}
