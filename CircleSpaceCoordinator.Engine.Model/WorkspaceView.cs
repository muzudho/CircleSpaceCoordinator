namespace CircleSpaceCoordinator.Engine.Model;

using CircleSpaceCoordinator.Application.Plans;
using CircleSpaceCoordinator.Application.Queries;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;

public sealed record WorkspaceView(
    CircleSpaceProject Project,
    Plan SelectedPlan,
    string SelectedDeskLayoutId,
    bool HasSelectedCircleLayout,
    bool CanRemoveSelectedDeskLayout,
    PlanSnapshot Snapshot,
    IReadOnlyList<RankedPlan> Ranking,
    VenueTopologyGraph Topology,
    IReadOnlyList<(GridPosition FirstCell, GridPosition SecondCell)> AutomaticEdges,
    IReadOnlyDictionary<string, IReadOnlySet<string>> CombinedPartners);
