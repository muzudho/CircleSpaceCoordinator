namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core;
using Microsoft.Xna.Framework;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private Plan? islandDistancePlan;
    private VenueTopologyGraph? islandDistanceGraph;
    private IReadOnlyDictionary<GridPosition, IslandDistance> islandDistances = new Dictionary<GridPosition, IslandDistance>();

    private void DrawIslandDistances()
    {
        if (workspace is null || workspace.SelectedPlan.IslandStarts.Count == 0) return;
        var plan = workspace.SelectedPlan;
        var graph = workspace.View.Topology;
        if (!ReferenceEquals(plan, islandDistancePlan) || !ReferenceEquals(graph, islandDistanceGraph))
        {
            islandDistances = IslandTraversal.Build(plan, graph);
            islandDistancePlan = plan;
            islandDistanceGraph = graph;
        }
        foreach (var cell in graph.Neighbors.Keys)
        {
            var bounds = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(cell));
            var center = GetCellCenter(cell);
            var size = Math.Min(bounds.Width * .65, 32d);
            var badge = new ScreenRectangle(center.X - size / 2, center.Y - size / 2, size, size);
            var distance = islandDistances.GetValueOrDefault(cell);
            DrawRectangle(badge, distance?.Distance == 0 ? new Color(20, 110, 65) : new Color(20, 28, 40, 235));
            textRenderer?.Draw(distance?.Distance.ToString() ?? "—", ToRectangle(badge, 0), Color.White, VenueTextSize(14), true);
        }
        var desks = plan.DeskPlacements.ToDictionary(item => item.Id);
        foreach (var start in plan.IslandStarts)
        {
            if (!desks.TryGetValue(start.DeskPlacementId, out var desk)) continue;
            var cell = start.GetCell(desk);
            var bounds = viewport.GetCellBounds(VenueCanvasMapper.ToCanvasCell(cell));
            var center = GetCellCenter(cell);
            var vector = new GridPosition(0, -1).Rotate(start.GetDirection(desk));
            var length = Math.Min(bounds.Width * .43, 26d);
            var tail = new ScreenPoint(center.X + vector.X * length * .5, center.Y + vector.Y * length * .5);
            var tip = new ScreenPoint(center.X + vector.X * length, center.Y + vector.Y * length);
            var color = new Color(80, 255, 160);
            DrawOutline(bounds, 3, color);
            DrawLine(tail, tip, 3, color);
            DrawLine(tip, new(tip.X - vector.X * 6 - vector.Y * 5, tip.Y - vector.Y * 6 + vector.X * 5), 3, color);
            DrawLine(tip, new(tip.X - vector.X * 6 + vector.Y * 5, tip.Y - vector.Y * 6 - vector.X * 5), 3, color);
        }
    }
}
