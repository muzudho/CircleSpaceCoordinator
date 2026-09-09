namespace CircleSpaceCoordinator.Application.Workspace;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Application.Participants;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Application.Editing;
using CircleSpaceCoordinator.Application.Layouts;
using CircleSpaceCoordinator.Application.Plans;

public static class EditorOperationHandler
{
    public static CircleSpaceProject Apply(CircleSpaceProject project, EditorOperation operation) => operation switch
    {
        DeskLayoutServiceFillAvailableCells op => DeskLayoutService.FillAvailableCells(project, op.planId, op.deskTypeId, op.idPrefix),
        DeskSeatLabelEditorReplaceLabels op => DeskSeatLabelEditor.ReplaceLabels(project, op.planId, op.labels),
        DeskSeatLabelEditorSetLabel op => DeskSeatLabelEditor.SetLabel(project, op.planId, op.deskPlacementId, op.relativeCell, op.blockName, op.seatName),
        DeskSeatLabelEditorRemoveLabel op => DeskSeatLabelEditor.RemoveLabel(project, op.planId, op.deskPlacementId, op.relativeCell),
        ParticipantAssignmentEditorSwapCellRegions op => ParticipantAssignmentEditor.SwapCellRegions(project, op.planId, op.sourceTopLeft, op.destinationTopLeft, op.width, op.height),
        ParticipantAssignmentEditorAssign op => ParticipantAssignmentEditor.Assign(project, op.planId, op.participantId, op.occupiedCells, op.scoringPosition, op.combinedSpaceId),
        ParticipantAssignmentEditorReassign op => ParticipantAssignmentEditor.Reassign(project, op.planId, op.participantId, op.occupiedCells, op.scoringPosition, op.combinedSpaceId),
        ParticipantAssignmentEditorUnassign op => ParticipantAssignmentEditor.Unassign(project, op.planId, op.participantId),
        ParticipantAssignmentEditorSwap op => ParticipantAssignmentEditor.Swap(project, op.planId, op.firstParticipantId, op.secondParticipantId),
        ParticipantAssignmentEditorSwapGroups op => ParticipantAssignmentEditor.SwapGroups(project, op.planId, op.firstParticipantIds, op.secondParticipantIds),
        PlanDeskEditorAddDesk op => PlanDeskEditor.AddDesk(project, op.planId, op.placement),
        PlanDeskEditorRemoveDesk op => PlanDeskEditor.RemoveDesk(project, op.planId, op.deskPlacementId),
        PlanDeskEditorMoveDesk op => PlanDeskEditor.MoveDesk(project, op.planId, op.deskPlacementId, op.newAnchor),
        PlanDeskEditorRotateDesk op => PlanDeskEditor.RotateDesk(project, op.planId, op.deskPlacementId, op.newOrientation),
        PlanDeskEditorSetDeskNumber op => PlanDeskEditor.SetDeskNumber(project, op.planId, op.deskPlacementId, op.deskNumber),
        TemporaryPlacementEditorPark op => TemporaryPlacementEditor.Park(project, op.planId, op.participantId, op.cells, op.position),
        TemporaryPlacementEditorSwap op => TemporaryPlacementEditor.Swap(project, op.planId, op.firstId, op.secondId),
        TemporaryPlacementEditorSwapRegions op => TemporaryPlacementEditor.SwapRegions(project, op.planId, op.source, op.destination, op.width, op.height),
        VenueEditorAddPillar op => VenueEditor.AddPillar(project, op.cell),
        VenueEditorRemovePillar op => VenueEditor.RemovePillar(project, op.cell),
        VenueEditorResize op => VenueEditor.Resize(project, op.width, op.height),
        VenueTopologyEditorAddConnector op => VenueTopologyEditor.AddConnector(project, op.planId, op.firstDeskId, op.secondDeskId, op.firstCell, op.secondCell),
        VenueTopologyEditorAddFacingRegion op => VenueTopologyEditor.AddFacingRegion(project, op.planId, op.first, op.second),
        VenueTopologyEditorToggleAutomaticConnection op => VenueTopologyEditor.ToggleAutomaticConnection(project, op.planId, op.first, op.second),
        VenueTopologyEditorRemoveAt op => VenueTopologyEditor.RemoveAt(project, op.planId, op.cell, op.deskId),
        VenueTopologyEditorRemoveConnector op => VenueTopologyEditor.RemoveConnector(project, op.planId, op.connectorId),
        LayoutCatalogServiceCreateDeskLayout op => LayoutCatalogService.CreateDeskLayout(project, op.id, op.name, op.description),
        LayoutCatalogServiceCreateCircleLayout op => LayoutCatalogService.CreateCircleLayout(project, op.id, op.name, op.deskLayoutId, op.description),
        LayoutCatalogServiceRemoveCircleLayout op => LayoutCatalogService.RemoveCircleLayout(project, op.circleLayoutId),
        LayoutCatalogServiceRemoveDeskLayout op => LayoutCatalogService.RemoveDeskLayout(project, op.deskLayoutId),
        LayoutCatalogServiceReassignCircleLayout op => LayoutCatalogService.ReassignCircleLayout(project, op.circleLayoutId, op.deskLayoutId),
        LayoutCatalogServiceRenameDeskLayout op => LayoutCatalogService.RenameDeskLayout(project, op.deskLayoutId, op.name),
        LayoutCatalogServiceRenameCircleLayout op => LayoutCatalogService.RenameCircleLayout(project, op.circleLayoutId, op.name),
        ParticipantCatalogServiceReplaceParticipants op => ParticipantCatalogService.ReplaceParticipants(project, op.rows),
        PlanCatalogServiceCreatePlan op => PlanCatalogService.CreatePlan(project, op.newPlanId, op.newPlanName, op.description),
        PlanCatalogServiceRenamePlan op => PlanCatalogService.RenamePlan(project, op.planId, op.newPlanName),
        PlanCatalogServiceRemovePlan op => PlanCatalogService.RemovePlan(project, op.planId),
        PlanCatalogServiceDuplicatePlan op => PlanCatalogService.DuplicatePlan(project, op.sourcePlanId, op.newPlanId, op.newPlanName),
        PlanCatalogServiceAddOptimizedPlan op => PlanCatalogService.AddOptimizedPlan(project, op.optimizedPlan, op.newPlanId, op.newPlanName),
        PlanCatalogServiceCopyDeskLayout op => PlanCatalogService.CopyDeskLayout(project, op.sourcePlanId, op.destinationPlanId),
        SetGenreStyles op => project with { GenreStyles = op.styles },
        _ => throw new ArgumentException("Unsupported editor operation."),
    };
}
