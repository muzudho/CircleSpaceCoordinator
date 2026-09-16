namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using Microsoft.Xna.Framework;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private CircleSpaceProject? seatTokenProject;
    private string? seatTokenPlanId;
    private HashSet<GridPosition> tokenSeatCells = [];
    private static readonly Color OffSeatColor = new(255, 164, 72);
    private CircleSpaceProject? stagingProject;
    private Plan? stagingPlan;
    private IReadOnlyList<ParticipantAssignment> stagedParticipants = [];

    private IReadOnlyList<ParticipantAssignment> GetStagedParticipants()
    {
        if (workspace is null || !workspace.HasSelectedCircleLayout) return [];
        if (!ReferenceEquals(stagingProject, workspace.Project) || !ReferenceEquals(stagingPlan, workspace.SelectedPlan))
        {
            stagingProject = workspace.Project;
            stagingPlan = workspace.SelectedPlan;
            stagedParticipants = CircleSpaceCoordinator.Desktop.Core.Interaction.UnassignedParticipantStaging.Build(stagingProject, stagingPlan);
        }
        return stagedParticipants;
    }

    private bool IsOffSeatToken(ParticipantToken token) => !token.Assigned || !AreTokenCellsSeats(token.DisplayCells);

    private bool AreTokenCellsSeats(IEnumerable<GridPosition> cells)
    {
        if (workspace is null) return false;
        if (!ReferenceEquals(seatTokenProject, workspace.Project) || seatTokenPlanId != workspace.SelectedPlanId)
        {
            seatTokenProject = workspace.Project;
            seatTokenPlanId = workspace.SelectedPlanId;
            var types = workspace.Project.DeskTypes.ToDictionary(type => type.Id);
            tokenSeatCells = workspace.SelectedPlan.DeskPlacements
                .SelectMany(desk => desk.GetSeatCells(types[desk.DeskTypeId]))
                .Where(workspace.Project.Venue.CanPlaceAt).ToHashSet();
        }
        return cells.All(tokenSeatCells.Contains);
    }

    private void DrawOffSeatToken(ScreenRectangle bounds, string? genreId, bool hollow, string? label = null)
    {
        var style = GetGenreStyle(genreId);
        FillRoundToken(bounds, bounds, hollow ? CanvasGridColor : style.Primary);
        if (!hollow) DrawGenrePattern(bounds, style.Pattern, style.Secondary, round: true);
        var center = new ScreenPoint(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        DrawCircle(center, Math.Min(bounds.Width, bounds.Height) / 2, OffSeatColor);
        if (label is not null)
            textRenderer?.Draw(label, ToRectangle(bounds, 5), Color.White, VenueTextSize(12), true);
    }

    private void FillRoundToken(ScreenRectangle circle, ScreenRectangle clip, Color color)
    {
        var radius = Math.Min(circle.Width, circle.Height) / 2;
        if (radius <= 0) return;
        var centerX = circle.X + circle.Width / 2;
        var centerY = circle.Y + circle.Height / 2;
        var bottom = Math.Min(centerY + radius, clip.Y + clip.Height);
        for (var y = Math.Max(centerY - radius, clip.Y); y < bottom; y += 1)
        {
            var dy = y + Math.Min(1, bottom - y) / 2 - centerY;
            var halfWidth = Math.Sqrt(Math.Max(0, radius * radius - dy * dy));
            var left = Math.Max(centerX - halfWidth, clip.X);
            var right = Math.Min(centerX + halfWidth, clip.X + clip.Width);
            if (right > left) DrawRectangle(new ScreenRectangle(left, y, right - left, Math.Min(1, bottom - y)), color);
        }
    }
}
