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
    private static CircleSpaceProject ApplyBlockStyles(CircleSpaceProject project, SetBlockStyles operation)
    {
        operation.Table?.Validate();
        foreach (var row in operation.styles) GenreStyleDefinition.NormalizeKnowledgeComment(row.KnowledgeComment);
        return project with { BlockStyles = operation.styles, BlockStyleTable = operation.Table ?? project.BlockStyleTable,
            IsConfidential = project.IsConfidential || operation.MarkConfidential };
    }

    public static CircleSpaceProject Apply(CircleSpaceProject project, EditorOperation operation) =>
        ModificationCreditsService.Apply(project, ApplyCore(project, operation), operation);

    private static CircleSpaceProject ApplyCore(CircleSpaceProject project, EditorOperation operation) => operation switch
    {
        RecordPortableProviders op => PortableCreditsService.Record(project, op.Package, op.Materials, op.FallbackDefinitions),
        UpdateChannelKnowledge op => ChannelKnowledgeService.Update(project, op.Knowledge, op.Handle, op.WorkDate),
        CaptureChannelKnowledge op => ChannelKnowledgeService.Capture(project, op.featureId, op.id, op.description, op.purpose, op.rule, op.confidential, op.Handle, op.Overwrite, op.WorkDate),
        BindChannelKnowledge op => ChannelKnowledgeService.Bind(project, op.id, op.featureId, op.name, op.column),
        ImportPortableSelection op => PortableSelectionService.Apply(project, op.package, op.selection),
        ImportFrameLayout op => FrameLayoutImportService.Add(project, op.incoming, op.id, op.name),
        SetFrameLayoutDefinitions op => SetDefinitions(project, op),
        SetFrameLayoutConfidential op => project.DeskLayouts.Any(layout => layout.Id == op.layoutId)
            ? project with { DeskLayouts = project.DeskLayouts.Select(layout => layout.Id == op.layoutId ? layout with { IsConfidential = true } : layout).ToArray() }
            : throw new InvalidOperationException("フレーム配置が存在しません。"),
        SetVenueName op => !string.IsNullOrWhiteSpace(op.name)
            ? project with { Venue = project.Venue with { Name = op.name.Trim() } }
            : throw new InvalidOperationException("会場名を入力してください。"),
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
        PlanDeskEditorAddDesk op => PlanDeskEditor.AddDesk(project, op.planId, op.placement, op.type),
        PlanDeskEditorRemoveDesk op => PlanDeskEditor.RemoveDesk(project, op.planId, op.deskPlacementId),
        PlanDeskEditorMoveDesk op => PlanDeskEditor.MoveDesk(project, op.planId, op.deskPlacementId, op.newAnchor),
        PlanDeskEditorRotateDesk op => PlanDeskEditor.RotateDesk(project, op.planId, op.deskPlacementId, op.newOrientation),
        PlanDeskEditorSetDeskNumber op => PlanDeskEditor.SetDeskNumber(project, op.planId, op.deskPlacementId, op.deskNumber),
        NumberFramesFromIslands op => PlanDeskEditor.NumberFromIslands(project, op.planId, op.firstNumber, op.frameIds),
        PlanDeskEditorSetDeskNumbers op => PlanDeskEditor.SetDeskNumbers(project, op.planId, op.deskPlacementIds, op.deskNumber),
        TemporaryPlacementEditorPark op => TemporaryPlacementEditor.Park(project, op.planId, op.participantId, op.cells, op.position),
        TemporaryPlacementEditorSwap op => TemporaryPlacementEditor.Swap(project, op.planId, op.firstId, op.secondId),
        TemporaryPlacementEditorSwapRegions op => TemporaryPlacementEditor.SwapRegions(project, op.planId, op.source, op.destination, op.width, op.height),
        VenueEditorAddPillar op => VenueEditor.AddPillar(project, op.cell),
        VenueEditorRemovePillar op => VenueEditor.RemovePillar(project, op.cell),
        VenueEditorResize op => VenueEditor.Resize(project, op.width, op.height, op.offsetX, op.offsetY),
        SetIslandStart op => VenueTopologyEditor.SetStart(project, op.planId, op.cell, op.remove),
        VenueTopologyEditorAddConnector op => VenueTopologyEditor.AddConnector(project, op.planId, op.firstDeskId, op.secondDeskId, op.firstCell, op.secondCell),
        VenueTopologyEditorAddFacingRegion op => VenueTopologyEditor.AddFacingRegion(project, op.planId, op.first, op.second),
        VenueTopologyEditorToggleAutomaticConnection op => VenueTopologyEditor.ToggleAutomaticConnection(project, op.planId, op.first, op.second),
        VenueTopologyEditorRemoveAt op => VenueTopologyEditor.RemoveAt(project, op.planId, op.cell, op.deskId),
        VenueTopologyEditorRemoveConnector op => VenueTopologyEditor.RemoveConnector(project, op.planId, op.connectorId),
        LayoutCatalogServiceCreateDeskLayout op => LayoutCatalogService.CreateDeskLayout(project, op.id, op.name, op.description),
        LayoutCatalogServiceDuplicateDeskLayout op => LayoutCatalogService.DuplicateDeskLayout(project, op.sourceId, op.id, op.name),
        LayoutCatalogServiceCreateCircleLayout op => LayoutCatalogService.CreateCircleLayout(project, op.id, op.name, op.deskLayoutId, op.description),
        LayoutCatalogServiceRemoveCircleLayout op => LayoutCatalogService.RemoveCircleLayout(project, op.circleLayoutId),
        LayoutCatalogServiceRemoveDeskLayout op => project.DeskLayouts.Count > 1
            ? LayoutCatalogService.RemoveDeskLayout(project, op.deskLayoutId)
            : throw new InvalidOperationException("最後のフレーム配置は削除できません。"),
        LayoutCatalogServiceReassignCircleLayout op => LayoutCatalogService.ReassignCircleLayout(project, op.circleLayoutId, op.deskLayoutId),
        LayoutCatalogServiceRenameDeskLayout op => LayoutCatalogService.RenameDeskLayout(project, op.deskLayoutId, op.name),
        SetDeskLayoutDescription op => LayoutCatalogService.SetDeskLayoutDescription(project, op.deskLayoutId, op.description),
        LayoutCatalogServiceMoveDeskLayout op => LayoutCatalogService.MoveDeskLayout(project, op.deskLayoutId, op.direction),
        LayoutCatalogServiceRenameCircleLayout op => LayoutCatalogService.RenameCircleLayout(project, op.circleLayoutId, op.name),
        ParticipantCatalogServiceReplaceParticipants op => ParticipantCatalogService.ReplaceParticipants(project, op.rows, op.source),
        PlanCatalogServiceCreatePlan op => PlanCatalogService.CreatePlan(project, op.newPlanId, op.newPlanName, op.description),
        PlanCatalogServiceRenamePlan op => PlanCatalogService.RenamePlan(project, op.planId, op.newPlanName),
        PlanCatalogServiceRemovePlan op => PlanCatalogService.RemovePlan(project, op.planId),
        PlanCatalogServiceDuplicatePlan op => PlanCatalogService.DuplicatePlan(project, op.sourcePlanId, op.newPlanId, op.newPlanName),
        PlanCatalogServiceAddOptimizedPlan op => PlanCatalogService.AddOptimizedPlan(project, op.optimizedPlan, op.newPlanId, op.newPlanName),
        PlanCatalogServiceCopyDeskLayout op => PlanCatalogService.CopyDeskLayout(project, op.sourcePlanId, op.destinationPlanId),
        SetGenreStyles op => project with { GenreStyles = op.styles,
            IsConfidential = project.IsConfidential || op.MarkConfidential,
            GenreCodeTableName = op.GenreCodeTableName is null ? project.GenreCodeTableName : GenreStyleDefinition.NormalizeTableName(op.GenreCodeTableName),
            GenreCodeOrder = op.GenreCodeOrder ?? project.GenreCodeOrder,
            GenreCodeOrderComment = op.UpdateGenreCodeOrder ? GenreStyleDefinition.NormalizeKnowledgeComment(op.GenreCodeOrderComment) : project.GenreCodeOrderComment,
            GenreStyleComment = op.UpdateOverallComment ? GenreStyleDefinition.NormalizeKnowledgeComment(op.OverallComment) : project.GenreStyleComment },
        SetBlockStyles op => ApplyBlockStyles(project, op),
        SwapNumberAddresses op => AddressSwapEditor.Swap(project, op.planId, op.channel, op.source, op.destination, op.width, op.height, op.sourceFrameIds),
        SetExportPlan op => op.planId is null || project.Plans.Any(plan => plan.Id == op.planId)
            ? project with { ExportPlanId = op.planId }
            : throw new InvalidOperationException("選択した配置案は存在しません。"),
        UpsertChannel op => ChannelEditor.Upsert(project, op.id, op.name, op.sourceColumn, op.CommentForChannel, op.CommentForWeight, op.OverallWeight, op.Handle, op.WorkDate),
        RemoveChannel op => ChannelEditor.Remove(project, op.id),
        SetChannelWeights op => ChannelEditor.SetWeights(project, op.planId, op.channelId, op.cells, op.weight),
        _ => throw new ArgumentException("Unsupported editor operation."),
    };

    private static CircleSpaceProject SetDefinitions(CircleSpaceProject project, SetFrameLayoutDefinitions operation)
    {
        operation.definitions.Validate(requireRepresentativeCell: false);
        if (!project.DeskLayouts.Any(layout => layout.Id == operation.layoutId))
            throw new InvalidOperationException("フレーム配置が存在しません。");
        return project with { DeskLayouts = project.DeskLayouts.Select(layout => layout.Id == operation.layoutId
            ? layout with { Definitions = operation.definitions } : layout).ToArray() };
    }
}
