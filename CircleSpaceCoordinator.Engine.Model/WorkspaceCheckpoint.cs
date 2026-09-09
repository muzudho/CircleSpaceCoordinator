namespace CircleSpaceCoordinator.Engine.Model;

using CircleSpaceCoordinator.Core.Model;

public sealed record EditHistoryState(CircleSpaceProject Current, CircleSpaceProject[] Undo, CircleSpaceProject[] Redo);
public sealed record WorkspaceCheckpoint(EditHistoryState History, string SelectedPlanId, string? SelectedDeskLayoutId);
