namespace CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Application.Participants;
using System.Text.Json.Serialization;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "operation")]
[JsonDerivedType(typeof(ImportFrameLayout), "ImportFrameLayout")]
[JsonDerivedType(typeof(ImportPortableSelection), "ImportPortableSelection")]
[JsonDerivedType(typeof(CaptureChannelKnowledge), "CaptureChannelKnowledge")]
[JsonDerivedType(typeof(BindChannelKnowledge), "BindChannelKnowledge")]
[JsonDerivedType(typeof(SetFrameLayoutDefinitions), "SetFrameLayoutDefinitions")]
[JsonDerivedType(typeof(SetFrameLayoutConfidential), "SetFrameLayoutConfidential")]
[JsonDerivedType(typeof(SetVenueName), "SetVenueName")]
[JsonDerivedType(typeof(SetExportPlan), "SetExportPlan")]
[JsonDerivedType(typeof(DeskLayoutServiceFillAvailableCells), "DeskLayoutService.FillAvailableCells")]
[JsonDerivedType(typeof(DeskSeatLabelEditorReplaceLabels), "DeskSeatLabelEditor.ReplaceLabels")]
[JsonDerivedType(typeof(DeskSeatLabelEditorSetLabel), "DeskSeatLabelEditor.SetLabel")]
[JsonDerivedType(typeof(DeskSeatLabelEditorRemoveLabel), "DeskSeatLabelEditor.RemoveLabel")]
[JsonDerivedType(typeof(ParticipantAssignmentEditorSwapCellRegions), "ParticipantAssignmentEditor.SwapCellRegions")]
[JsonDerivedType(typeof(ParticipantAssignmentEditorAssign), "ParticipantAssignmentEditor.Assign")]
[JsonDerivedType(typeof(ParticipantAssignmentEditorReassign), "ParticipantAssignmentEditor.Reassign")]
[JsonDerivedType(typeof(ParticipantAssignmentEditorUnassign), "ParticipantAssignmentEditor.Unassign")]
[JsonDerivedType(typeof(ParticipantAssignmentEditorSwap), "ParticipantAssignmentEditor.Swap")]
[JsonDerivedType(typeof(ParticipantAssignmentEditorSwapGroups), "ParticipantAssignmentEditor.SwapGroups")]
[JsonDerivedType(typeof(PlanDeskEditorAddDesk), "PlanDeskEditor.AddDesk")]
[JsonDerivedType(typeof(PlanDeskEditorRemoveDesk), "PlanDeskEditor.RemoveDesk")]
[JsonDerivedType(typeof(PlanDeskEditorMoveDesk), "PlanDeskEditor.MoveDesk")]
[JsonDerivedType(typeof(PlanDeskEditorRotateDesk), "PlanDeskEditor.RotateDesk")]
[JsonDerivedType(typeof(PlanDeskEditorSetDeskNumber), "PlanDeskEditor.SetDeskNumber")]
[JsonDerivedType(typeof(PlanDeskEditorSetDeskNumbers), "PlanDeskEditor.SetDeskNumbers")]
[JsonDerivedType(typeof(TemporaryPlacementEditorPark), "TemporaryPlacementEditor.Park")]
[JsonDerivedType(typeof(TemporaryPlacementEditorSwap), "TemporaryPlacementEditor.Swap")]
[JsonDerivedType(typeof(TemporaryPlacementEditorSwapRegions), "TemporaryPlacementEditor.SwapRegions")]
[JsonDerivedType(typeof(VenueEditorAddPillar), "VenueEditor.AddPillar")]
[JsonDerivedType(typeof(VenueEditorRemovePillar), "VenueEditor.RemovePillar")]
[JsonDerivedType(typeof(VenueEditorResize), "VenueEditor.Resize")]
[JsonDerivedType(typeof(VenueTopologyEditorAddConnector), "VenueTopologyEditor.AddConnector")]
[JsonDerivedType(typeof(VenueTopologyEditorAddFacingRegion), "VenueTopologyEditor.AddFacingRegion")]
[JsonDerivedType(typeof(VenueTopologyEditorToggleAutomaticConnection), "VenueTopologyEditor.ToggleAutomaticConnection")]
[JsonDerivedType(typeof(VenueTopologyEditorRemoveAt), "VenueTopologyEditor.RemoveAt")]
[JsonDerivedType(typeof(VenueTopologyEditorRemoveConnector), "VenueTopologyEditor.RemoveConnector")]
[JsonDerivedType(typeof(LayoutCatalogServiceCreateDeskLayout), "LayoutCatalogService.CreateDeskLayout")]
[JsonDerivedType(typeof(LayoutCatalogServiceDuplicateDeskLayout), "LayoutCatalogService.DuplicateDeskLayout")]
[JsonDerivedType(typeof(LayoutCatalogServiceCreateCircleLayout), "LayoutCatalogService.CreateCircleLayout")]
[JsonDerivedType(typeof(LayoutCatalogServiceRemoveCircleLayout), "LayoutCatalogService.RemoveCircleLayout")]
[JsonDerivedType(typeof(LayoutCatalogServiceRemoveDeskLayout), "LayoutCatalogService.RemoveDeskLayout")]
[JsonDerivedType(typeof(LayoutCatalogServiceReassignCircleLayout), "LayoutCatalogService.ReassignCircleLayout")]
[JsonDerivedType(typeof(LayoutCatalogServiceRenameDeskLayout), "LayoutCatalogService.RenameDeskLayout")]
[JsonDerivedType(typeof(SetDeskLayoutDescription), "SetDeskLayoutDescription")]
[JsonDerivedType(typeof(LayoutCatalogServiceMoveDeskLayout), "LayoutCatalogService.MoveDeskLayout")]
[JsonDerivedType(typeof(LayoutCatalogServiceRenameCircleLayout), "LayoutCatalogService.RenameCircleLayout")]
[JsonDerivedType(typeof(ParticipantCatalogServiceReplaceParticipants), "ParticipantCatalogService.ReplaceParticipants")]
[JsonDerivedType(typeof(PlanCatalogServiceCreatePlan), "PlanCatalogService.CreatePlan")]
[JsonDerivedType(typeof(PlanCatalogServiceRenamePlan), "PlanCatalogService.RenamePlan")]
[JsonDerivedType(typeof(PlanCatalogServiceRemovePlan), "PlanCatalogService.RemovePlan")]
[JsonDerivedType(typeof(PlanCatalogServiceDuplicatePlan), "PlanCatalogService.DuplicatePlan")]
[JsonDerivedType(typeof(PlanCatalogServiceAddOptimizedPlan), "PlanCatalogService.AddOptimizedPlan")]
[JsonDerivedType(typeof(PlanCatalogServiceCopyDeskLayout), "PlanCatalogService.CopyDeskLayout")]
[JsonDerivedType(typeof(SetGenreStyles), "SetGenreStyles")]
[JsonDerivedType(typeof(SetBlockStyles), "SetBlockStyles")]
[JsonDerivedType(typeof(SwapNumberAddresses), "SwapNumberAddresses")]
[JsonDerivedType(typeof(UpsertChannel), "UpsertChannel")]
[JsonDerivedType(typeof(RemoveChannel), "RemoveChannel")]
[JsonDerivedType(typeof(SetChannelWeights), "SetChannelWeights")]
[JsonDerivedType(typeof(SetIslandStart), "SetIslandStart")]
[JsonDerivedType(typeof(NumberFramesFromIslands), "NumberFramesFromIslands")]
[JsonDerivedType(typeof(RecordPortableProviders), "RecordPortableProviders")]
[JsonDerivedType(typeof(UpdateChannelKnowledge), "UpdateChannelKnowledge")]
public abstract record EditorOperation
{
    public string? ActorHandle { get; init; }
    public DateOnly? WorkDate { get; init; }
    public string? ChangeLog { get; init; }
}
public sealed record UpdateChannelKnowledge(CircleSpaceCoordinator.Core.Evaluation.ChannelKnowledge Knowledge, string Handle) : EditorOperation;
public sealed record RecordPortableProviders(PortablePackage Package, IReadOnlyList<PortableMaterialSelection> Materials,
    SpaceDefinitionCatalog FallbackDefinitions) : EditorOperation;
public sealed record CaptureChannelKnowledge(string featureId, string id, string description, string purpose,
    CircleSpaceCoordinator.Core.Evaluation.ChannelInputRule rule, bool confidential) : EditorOperation
{
    public string Handle { get; init; } = "";
    public bool Overwrite { get; init; }
}
public sealed record BindChannelKnowledge(string id, string featureId, string name, string column) : EditorOperation;
public sealed record ImportPortableSelection(PortablePackage package, IReadOnlyList<PortableImportItem> selection) : EditorOperation;
public sealed record ImportFrameLayout(CircleSpaceProject incoming, string id, string name) : EditorOperation;
public sealed record SetFrameLayoutDefinitions(string layoutId, SpaceDefinitionCatalog definitions) : EditorOperation;
public sealed record SetFrameLayoutConfidential(string layoutId) : EditorOperation;
public sealed record SetVenueName(string name) : EditorOperation;
public sealed record NumberFramesFromIslands(string planId, int firstNumber, IReadOnlySet<string>? frameIds = null) : EditorOperation, IPlanOperation;
public sealed record SetIslandStart(string planId, GridPosition cell, bool remove = false) : EditorOperation, IPlanOperation;
public interface IPlanOperation { string planId { get; } }

public sealed record DeskLayoutServiceFillAvailableCells(string planId, string deskTypeId, string idPrefix = "auto-desk") : EditorOperation, IPlanOperation;
public sealed record DeskSeatLabelEditorReplaceLabels(string planId, IReadOnlyList<DeskSeatLabel> labels) : EditorOperation, IPlanOperation;
public sealed record DeskSeatLabelEditorSetLabel(string planId, string deskPlacementId, GridPosition relativeCell, string blockName, string seatName) : EditorOperation, IPlanOperation;
public sealed record DeskSeatLabelEditorRemoveLabel(string planId, string deskPlacementId, GridPosition relativeCell) : EditorOperation, IPlanOperation;
public sealed record ParticipantAssignmentEditorSwapCellRegions(string planId, GridPosition sourceTopLeft, GridPosition destinationTopLeft, int width, int height) : EditorOperation, IPlanOperation;
public sealed record ParticipantAssignmentEditorAssign(string planId, string participantId, IReadOnlySet<GridPosition> occupiedCells, GridPosition scoringPosition, string? combinedSpaceId = null) : EditorOperation, IPlanOperation;
public sealed record ParticipantAssignmentEditorReassign(string planId, string participantId, IReadOnlySet<GridPosition> occupiedCells, GridPosition scoringPosition, string? combinedSpaceId = null) : EditorOperation, IPlanOperation;
public sealed record ParticipantAssignmentEditorUnassign(string planId, string participantId) : EditorOperation, IPlanOperation;
public sealed record ParticipantAssignmentEditorSwap(string planId, string firstParticipantId, string secondParticipantId) : EditorOperation, IPlanOperation;
public sealed record ParticipantAssignmentEditorSwapGroups(string planId, IReadOnlyCollection<string> firstParticipantIds, IReadOnlyCollection<string> secondParticipantIds) : EditorOperation, IPlanOperation;
public sealed record PlanDeskEditorAddDesk(string planId, DeskPlacement placement, DeskType? type = null) : EditorOperation, IPlanOperation;
public sealed record PlanDeskEditorRemoveDesk(string planId, string deskPlacementId) : EditorOperation, IPlanOperation;
public sealed record PlanDeskEditorMoveDesk(string planId, string deskPlacementId, GridPosition newAnchor) : EditorOperation, IPlanOperation;
public sealed record PlanDeskEditorRotateDesk(string planId, string deskPlacementId, QuarterTurn newOrientation) : EditorOperation, IPlanOperation;
public sealed record PlanDeskEditorSetDeskNumber(string planId, string deskPlacementId, string? deskNumber) : EditorOperation, IPlanOperation;
public sealed record PlanDeskEditorSetDeskNumbers(string planId, IReadOnlyList<string> deskPlacementIds, string? deskNumber) : EditorOperation, IPlanOperation;
public sealed record TemporaryPlacementEditorPark(string planId, string participantId, IReadOnlySet<GridPosition> cells, GridPosition position) : EditorOperation, IPlanOperation;
public sealed record TemporaryPlacementEditorSwap(string planId, string firstId, string secondId) : EditorOperation, IPlanOperation;
public sealed record TemporaryPlacementEditorSwapRegions(string planId, GridPosition source, GridPosition destination, int width, int height) : EditorOperation, IPlanOperation;
public sealed record VenueEditorAddPillar(GridPosition cell) : EditorOperation;
public sealed record VenueEditorRemovePillar(GridPosition cell) : EditorOperation;
public sealed record VenueEditorResize(int width, int height, int offsetX = 0, int offsetY = 0) : EditorOperation;
public sealed record VenueTopologyEditorAddConnector(string planId, string firstDeskId, string secondDeskId, GridPosition? firstCell = null, GridPosition? secondCell = null) : EditorOperation, IPlanOperation;
public sealed record VenueTopologyEditorAddFacingRegion(string planId, GridPosition first, GridPosition second) : EditorOperation, IPlanOperation;
public sealed record VenueTopologyEditorToggleAutomaticConnection(string planId, GridPosition first, GridPosition second) : EditorOperation, IPlanOperation;
public sealed record VenueTopologyEditorRemoveAt(string planId, GridPosition cell, string? deskId) : EditorOperation, IPlanOperation;
public sealed record VenueTopologyEditorRemoveConnector(string planId, string connectorId) : EditorOperation, IPlanOperation;
public sealed record LayoutCatalogServiceCreateDeskLayout(string id, string name, string? description = null) : EditorOperation;
public sealed record LayoutCatalogServiceDuplicateDeskLayout(string sourceId, string id, string name) : EditorOperation;
public sealed record LayoutCatalogServiceCreateCircleLayout(string id, string name, string deskLayoutId, string? description = null) : EditorOperation;
public sealed record LayoutCatalogServiceRemoveCircleLayout(string circleLayoutId) : EditorOperation;
public sealed record LayoutCatalogServiceRemoveDeskLayout(string deskLayoutId) : EditorOperation;
public sealed record LayoutCatalogServiceReassignCircleLayout(string circleLayoutId, string deskLayoutId) : EditorOperation;
public sealed record LayoutCatalogServiceRenameDeskLayout(string deskLayoutId, string name) : EditorOperation;
public sealed record SetDeskLayoutDescription(string deskLayoutId, string? description) : EditorOperation;
public sealed record LayoutCatalogServiceMoveDeskLayout(string deskLayoutId, int direction) : EditorOperation;
public sealed record LayoutCatalogServiceRenameCircleLayout(string circleLayoutId, string name) : EditorOperation;
public sealed record ParticipantCatalogServiceReplaceParticipants(IReadOnlyList<ParticipantImportRow> rows,
    ParticipantTableSource? source = null) : EditorOperation;
public sealed record PlanCatalogServiceCreatePlan(string newPlanId, string newPlanName, string? description = null) : EditorOperation;
public sealed record PlanCatalogServiceRenamePlan(string planId, string newPlanName) : EditorOperation, IPlanOperation;
public sealed record PlanCatalogServiceRemovePlan(string planId) : EditorOperation, IPlanOperation;
public sealed record PlanCatalogServiceDuplicatePlan(string sourcePlanId, string newPlanId, string newPlanName) : EditorOperation;
public sealed record PlanCatalogServiceAddOptimizedPlan(Plan optimizedPlan, string newPlanId, string newPlanName) : EditorOperation;
public sealed record PlanCatalogServiceCopyDeskLayout(string sourcePlanId, string destinationPlanId) : EditorOperation;
public sealed record SetGenreStyles(IReadOnlyList<GenreStyleDefinition> styles) : EditorOperation;
public sealed record SetBlockStyles(IReadOnlyList<BlockStyleDefinition> styles) : EditorOperation;
public sealed record SwapNumberAddresses(string planId, int channel, GridPosition source, GridPosition destination,
    int width, int height, IReadOnlyList<string> sourceFrameIds) : EditorOperation, IPlanOperation;
public sealed record SetExportPlan(string? planId) : EditorOperation;
public sealed record UpsertChannel(string id, string name, string? sourceColumn) : EditorOperation
{
    public string Handle { get; init; } = "";
    public double? OverallWeight { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [System.Text.Json.Serialization.JsonPropertyName("comment-for-channel")]
    public string? CommentForChannel { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [System.Text.Json.Serialization.JsonPropertyName("comment-for-weight")]
    public string? CommentForWeight { get; init; }
}
public sealed record RemoveChannel(string id) : EditorOperation;
public sealed record SetChannelWeights(string planId, string channelId, IReadOnlyList<GridPosition> cells, double weight) : EditorOperation, IPlanOperation;
