namespace CircleSpaceCoordinator.Engine.Model;

using CircleSpaceCoordinator.Application.Plans;
using CircleSpaceCoordinator.Application.Queries;
using CircleSpaceCoordinator.Core.Model;

public interface IEditorWorkspace
{
    CircleSpaceProject Project { get; }
    string SelectedPlanId { get; }
    string SelectedDeskLayoutId { get; }
    bool HasSelectedCircleLayout { get; }
    bool CanRemoveSelectedDeskLayout { get; }
    Plan SelectedPlan { get; }
    bool CanUndo { get; }
    bool CanRedo { get; }
    void SelectPlan(string id);
    void SelectDeskLayout(string id);
    void LoadProject(CircleSpaceProject project);
    void Execute(EditorOperation operation, bool selectedPlanEdit = true);
    CircleSpaceProject Undo();
    CircleSpaceProject Redo();
    IReadOnlyList<RankedPlan> RankPlans();
    PlanSnapshot GetSelectedPlanSnapshot();
}
