namespace CircleSpaceCoordinator.Desktop.Core.Interaction;


using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Validation;
using StationeryUI.Canvas;

public sealed record DeskDropResult(
    bool Applied,
    IReadOnlyList<ValidationIssue> Issues,
    QuarterTurn? AffectedOrientation = null)
{
    public static DeskDropResult Success { get; } = new(true, []);
}

public sealed class DeskDragController(IEditorWorkspace workspace, GridViewport viewport)
{
    private string? draggedPlanId;
    private string? draggedDeskId;
    private GridPosition grabOffset;

    public bool IsDragging => draggedDeskId is not null;

    public string? DraggedDeskId => draggedDeskId;

    public GridPosition? PreviewAnchor { get; private set; }

    public bool BeginDrag(ScreenPoint pointer)
    {
        if (IsDragging)
            throw new InvalidOperationException("A desk drag is already active.");

        var pointerCell = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
        var desk = workspace.GetSelectedPlanSnapshot().Desks
            .LastOrDefault(item => item.OccupiedCells.Contains(pointerCell));
        if (desk is null)
            return false;

        draggedPlanId = workspace.SelectedPlanId;
        draggedDeskId = desk.Id;
        grabOffset = new GridPosition(pointerCell.X - desk.Anchor.X, pointerCell.Y - desk.Anchor.Y);
        PreviewAnchor = desk.Anchor;
        return true;
    }

    public bool UpdateDrag(ScreenPoint pointer)
    {
        if (!IsDragging)
            return false;

        var pointerCell = VenueCanvasMapper.ToGridPosition(viewport.ScreenToCell(pointer));
        PreviewAnchor = new GridPosition(pointerCell.X - grabOffset.X, pointerCell.Y - grabOffset.Y);
        return true;
    }

    public DeskDropResult Drop()
    {
        if (draggedPlanId is null || draggedDeskId is null || PreviewAnchor is null)
            throw new InvalidOperationException("There is no active desk drag.");

        try
        {
            var planId = draggedPlanId;
            var deskId = draggedDeskId;
            var anchor = PreviewAnchor.Value;
            var orientation = workspace.SelectedPlan.DeskPlacements
                .Single(item => item.Id == deskId)
                .Orientation;
            workspace.Execute(new PlanDeskEditorMoveDesk( planId, deskId, anchor));
            return new DeskDropResult(true, [], orientation);
        }
        catch (ProjectValidationException exception)
        {
            return new DeskDropResult(false, exception.Issues);
        }
        finally
        {
            ClearDrag();
        }
    }

    public void Cancel() => ClearDrag();

    private void ClearDrag()
    {
        draggedPlanId = null;
        draggedDeskId = null;
        PreviewAnchor = null;
        grabOffset = default;
    }
}
