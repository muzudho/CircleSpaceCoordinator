namespace CircleSpaceCoordinator.Core.Evaluation;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;

public static class IslandNumbering
{
    public static IReadOnlyList<GridPosition> OrderedCells(Plan plan, VenueTopologyGraph graph,
        IReadOnlySet<GridPosition>? targets = null)
    {
        var distances = IslandTraversal.Build(plan, graph);
        if (distances.Count == 0) throw new InvalidOperationException("スタートの旗がありません。［島定義］で配置してください。");
        if (targets is not null && targets.Any(cell => !distances.ContainsKey(cell)))
            throw new InvalidOperationException("旗から到達できない対象セルがあります。旗の入口方向と接続を確認してください。");
        var roots = (targets ?? distances.Keys.ToHashSet()).Select(cell => distances[cell].StartCell).ToHashSet();
        var visits = distances.Where(pair => roots.Contains(pair.Value.StartCell)).ToArray();
        if (visits.Where(pair => pair.Value.ParentCell is not null).GroupBy(pair => pair.Value.ParentCell!.Value).Any(group => group.Count() > 1))
            throw new InvalidOperationException("経路が分岐するツリー構造のため、自動連番できません。分岐のないシーケンスになるよう、旗と接続を見直してください。");
        return visits.OrderBy(pair => pair.Value.StartCell.Y).ThenBy(pair => pair.Value.StartCell.X)
            .ThenBy(pair => pair.Value.Distance).Where(pair => targets is null || targets.Contains(pair.Key))
            .Select(pair => pair.Key).ToArray();
    }

    public static IReadOnlyList<string> OrderedFrames(Plan plan, VenueTopologyGraph graph, IReadOnlySet<string>? frameIds = null)
    {
        var targets = frameIds is null ? null : graph.DeskIdByCell
            .Where(pair => frameIds.Contains(pair.Value) && graph.Neighbors.ContainsKey(pair.Key)).Select(pair => pair.Key).ToHashSet();
        if (frameIds is not null && frameIds.Any(id => !targets!.Any(cell => graph.DeskIdByCell[cell] == id)))
            throw new InvalidOperationException("対象フレームに配置可能セルがありません。");
        // A frame receives one number, when it is first encountered along the sequence.
        return OrderedCells(plan, graph, targets).Select(cell => graph.DeskIdByCell[cell]).Distinct(StringComparer.Ordinal).ToArray();
    }
}
