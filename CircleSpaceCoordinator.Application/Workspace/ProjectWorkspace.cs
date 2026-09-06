namespace CircleSpaceCoordinator.Application.Workspace;

using CircleSpaceCoordinator.Application.Editing;
using CircleSpaceCoordinator.Application.Plans;
using CircleSpaceCoordinator.Application.Queries;
using CircleSpaceCoordinator.Core.Model;

public sealed class ProjectWorkspace
{
    private readonly ProjectEditHistory history;
    private string selectedPlanId = "";
    private string? selectedDeskLayoutId;

    public ProjectWorkspace(CircleSpaceProject project, string? selectedPlanId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        project = LayoutProjection.MigrateLegacyPlans(project);
        if (project.Plans.Count == 0)
            throw new InvalidOperationException("A workspace requires at least one plan.");

        history = new ProjectEditHistory(project);
        SelectedPlanId = selectedPlanId ?? project.Plans[0].Id;
        EnsurePlanExists(SelectedPlanId);
    }

    public CircleSpaceProject Project => history.Current;

    public string SelectedPlanId
    {
        get => HasSelectedCircleLayout ? selectedPlanId : DeskPreviewId;
        private set => selectedPlanId = value;
    }

    public string SelectedDeskLayoutId => selectedDeskLayoutId ?? Project.CircleLayouts
        .Single(circle => circle.Id == selectedPlanId).DeskLayoutId;

    public bool HasSelectedCircleLayout => selectedDeskLayoutId is null || Project.CircleLayouts
        .Any(circle => circle.Id == selectedPlanId && circle.DeskLayoutId == selectedDeskLayoutId);

    private string DeskPreviewId
    {
        get
        {
            var id = $"desk-preview:{SelectedDeskLayoutId}";
            while (Project.Plans.Any(plan => plan.Id == id)) id += ":";
            return id;
        }
    }

    public Plan SelectedPlan => HasSelectedCircleLayout
        ? Project.Plans.Single(plan => plan.Id == selectedPlanId)
        : BuildDeskPreview();

    public void SelectDeskLayout(string deskLayoutId)
    {
        if (!Project.DeskLayouts.Any(desk => desk.Id == deskLayoutId))
            throw new KeyNotFoundException($"Desk layout '{deskLayoutId}' does not exist.");
        selectedDeskLayoutId = deskLayoutId;
        if (!HasSelectedCircleLayout)
        {
            var child = Project.CircleLayouts.FirstOrDefault(circle => circle.DeskLayoutId == deskLayoutId);
            if (child is not null) selectedPlanId = child.Id;
        }
    }

    public bool CanRemoveSelectedDeskLayout => !Project.CircleLayouts
        .Any(circle => circle.DeskLayoutId == SelectedDeskLayoutId);

    public bool CanUndo => history.CanUndo;

    public bool CanRedo => history.CanRedo;

    public void SelectPlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        EnsurePlanExists(planId);
        SelectedPlanId = planId;
        selectedDeskLayoutId = null;
    }

    public void LoadProject(CircleSpaceProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        project = LayoutProjection.MigrateLegacyPlans(project);
        if (project.Plans.Count == 0)
            throw new InvalidOperationException("A workspace requires at least one plan.");
        history.Reset(project);
        selectedDeskLayoutId = null;
        SelectedPlanId = project.Plans[0].Id;
    }

    public void Apply(Func<CircleSpaceProject, string, CircleSpaceProject> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        ApplySelectedPlanEdit(project => edit(project, SelectedPlanId));
    }

    // Existing canvas editors consume a combined Plan. An unused desk gets a
    // temporary view for that call only; it never creates a CircleLayout.
    public void ApplySelectedPlanEdit(Func<CircleSpaceProject, CircleSpaceProject> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        if (HasSelectedCircleLayout)
        {
            // Canvas editors update Plans; commit immediately so later layout
            // operations and sibling plans see the current edit, not stale data.
            var planId = SelectedPlanId;
            ApplyProjectEdit(project => LayoutProjection.CommitLegacyPlanEdits(edit(project), planId));
            return;
        }
        var preview = BuildDeskPreview();
        var deskId = SelectedDeskLayoutId;
        history.Apply(project =>
        {
            var edited = edit(project with { Plans = [.. project.Plans, preview] });
            var result = edited.Plans.Single(plan => plan.Id == preview.Id);
            if (result.Assignments.Count != 0)
                throw new InvalidOperationException("Create a circle layout before assigning circles.");
            return edited with
            {
                Plans = edited.Plans.Where(plan => plan.Id != preview.Id).ToArray(),
                DeskLayouts = edited.DeskLayouts.Select(desk => desk.Id != deskId ? desk : desk with
                {
                    DeskPlacements = result.DeskPlacements,
                    SeatLabels = result.SeatLabels,
                    IslandConnectors = result.IslandConnectors,
                    DisabledIslandConnections = result.DisabledIslandConnections,
                    FacingRegions = result.FacingRegions,
                }).ToArray(),
            };
        });
        ReconcileSelection();
    }

    public void ApplyProjectEdit(Func<CircleSpaceProject, CircleSpaceProject> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        history.Apply(edit);
        ReconcileSelection();
    }

    public CircleSpaceProject Undo()
    {
        var project = history.Undo();
        ReconcileSelection();
        return project;
    }

    public CircleSpaceProject Redo()
    {
        var project = history.Redo();
        ReconcileSelection();
        return project;
    }

    public IReadOnlyList<RankedPlan> RankPlans() => PlanCatalogService.RankPlans(Project);

    public PlanSnapshot GetSelectedPlanSnapshot() => PlanViewService.Build(
        HasSelectedCircleLayout ? Project : Project with { Plans = [.. Project.Plans, BuildDeskPreview()] }, SelectedPlanId);

    private Plan BuildDeskPreview()
    {
        var desk = Project.DeskLayouts.Single(item => item.Id == SelectedDeskLayoutId);
        return new Plan(DeskPreviewId, desk.Name, desk.DeskPlacements, [])
        {
            SeatLabels = desk.SeatLabels,
            IslandConnectors = desk.IslandConnectors,
            DisabledIslandConnections = desk.DisabledIslandConnections,
            FacingRegions = desk.FacingRegions,
        };
    }

    private void ReconcileSelection()
    {
        if (Project.Plans.All(plan => plan.Id != selectedPlanId))
            selectedPlanId = Project.Plans[0].Id;
        if (selectedDeskLayoutId is not null)
        {
            if (Project.DeskLayouts.Any(desk => desk.Id == selectedDeskLayoutId))
                SelectDeskLayout(selectedDeskLayoutId);
            else
                selectedDeskLayoutId = null;
        }
    }

    private void EnsurePlanExists(string planId)
    {
        if (Project.Plans.All(plan => plan.Id != planId))
            throw new KeyNotFoundException($"Plan '{planId}' does not exist.");
    }
}
