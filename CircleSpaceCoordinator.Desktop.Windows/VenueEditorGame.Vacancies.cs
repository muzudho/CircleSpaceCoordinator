namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using Microsoft.Xna.Framework;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private CircleSpaceProject? vacancyProject;
    private readonly Dictionary<string, IReadOnlySet<GridPosition>> vacancyCache = new(StringComparer.Ordinal);
    private bool ShowsVacantSeats => editorMode is EditorMode.GenrePlacement or EditorMode.CirclePlacement;
    private static readonly Color VacancyColor = new(255, 205, 80);

    private IReadOnlySet<GridPosition> GetVacantSeats(Plan plan)
    {
        var project = workspace!.Project;
        if (!ReferenceEquals(project, vacancyProject))
        {
            vacancyProject = project;
            vacancyCache.Clear();
        }
        if (!vacancyCache.TryGetValue(plan.Id, out var cells))
            vacancyCache[plan.Id] = cells = CircleLayoutVacancies.Find(project, plan);
        return cells;
    }

    private void DrawVacantSeats()
    {
        if (!ShowsVacantSeats || workspace is null) return;
        foreach (var cell in GetVacantSeats(workspace.SelectedPlan))
        {
            var bounds = viewport.GetCellBounds(new GridCellAddress(cell.X, cell.Y));
            if (bounds.X + bounds.Width < 0 || bounds.X > GraphicsDevice.Viewport.Width ||
                bounds.Y + bounds.Height < ToolbarHeight || bounds.Y > GraphicsDevice.Viewport.Height - StatusBarHeight) continue;
            var center = new ScreenPoint(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            var radius = Math.Min(12, Math.Min(bounds.Width, bounds.Height) * 0.26);
            DrawCircle(center, radius + 1, new Color(20, 25, 32));
            DrawCircle(center, radius, VacancyColor);
        }
    }

    private int VacantSeatCount(string planId)
    {
        if (!ShowsVacantSeats || workspace is null) return 0;
        var plan = workspace.Project.Plans.FirstOrDefault(item => item.Id == planId);
        return plan is null ? 0 : GetVacantSeats(plan).Count;
    }
}
