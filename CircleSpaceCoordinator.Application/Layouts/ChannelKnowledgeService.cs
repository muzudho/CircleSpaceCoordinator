namespace CircleSpaceCoordinator.Application.Layouts;
using CircleSpaceCoordinator.Application.Editing;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;

public static class ChannelKnowledgeService
{
    public static CircleSpaceProject Capture(CircleSpaceProject project, string featureId, string id,
        string description, string purpose, ChannelInputRule rule, bool confidential)
    {
        var feature = project.Evaluation.Features.Single(item => item.Id == featureId);
        var map = project.Evaluation.WeightMaps.Single(item => item.FeatureId == featureId);
        var venue = project.Venue;
        var knowledge = new ChannelKnowledge(id, feature.Name, description, purpose, rule,
            feature.Scale, feature.Offset, feature.OverallWeight, confidential || project.IsConfidential || feature.IsConfidential,
            map.DefaultWeight, map.Cells.Select(pair => new KnowledgeCell(pair.Key.X, pair.Key.Y, pair.Value)).ToArray(),
            new(venue.Name, venue.Width, venue.Height, venue.BlockedCells.ToArray(),
                venue.Zones.Select(zone => new KnowledgeZone(zone.Name, zone.Cells.ToArray())).ToArray()));
        return Add(project, knowledge);
    }

    public static CircleSpaceProject Add(CircleSpaceProject project, ChannelKnowledge knowledge)
    {
        knowledge.Validate();
        if (project.ChannelKnowledge.Any(item => item.Id == knowledge.Id || item.Name == knowledge.Name))
            throw new InvalidOperationException("同じIDまたは名前の知見があります。改名または見送りを選んでください。");
        return project with { ChannelKnowledge = [.. project.ChannelKnowledge, knowledge],
            IsConfidential = project.IsConfidential || knowledge.IsConfidential };
    }

    public static CircleSpaceProject Bind(CircleSpaceProject project, string id, string featureId, string name, string column)
    {
        var knowledge = project.ChannelKnowledge.Single(item => item.Id == id);
        knowledge.Validate();
        if (project.Evaluation.Features.Any(item => item.Id == featureId || item.Name == name))
            throw new InvalidOperationException("既存チャンネルとIDまたは名前が重複しています。別の名前で追加してください。");
        if (knowledge.Venue is { } venue && (venue.Width != project.Venue.Width || venue.Height != project.Venue.Height ||
            !project.Venue.BlockedCells.SetEquals(venue.BlockedCells) || project.Venue.Zones.Count != venue.Zones.Length ||
            !venue.Zones.All(zone => project.Venue.Zones.Any(other => other.Name == zone.Name && other.Cells.SetEquals(zone.Cells)))))
            throw new InvalidOperationException("重みの会場形状が異なります。ひな形として書き出して利用してください。");
        ArgumentException.ThrowIfNullOrWhiteSpace(column);
        // Upsert checks that the column actually exists. Refresh validates every row before committing.
        var result = ChannelEditor.Upsert(project, featureId, name, column);
        result = result with { Evaluation = result.Evaluation with
        {
            Features = result.Evaluation.Features.Select(item => item.Id == featureId ? item with
            { Description = knowledge.Description, Purpose = knowledge.Purpose, InputRule = knowledge.InputRule,
                Scale = knowledge.Scale, Offset = knowledge.Offset, OverallWeight = knowledge.OverallWeight,
                IsConfidential = knowledge.IsConfidential } : item).ToArray(),
            WeightMaps = result.Evaluation.WeightMaps.Select(item => item.FeatureId == featureId ?
                new WeightMap(featureId, knowledge.DefaultWeight, knowledge.Cells.ToDictionary(cell => new GridPosition(cell.X, cell.Y), cell => cell.Weight)) : item).ToArray(),
        } };
        return ChannelEditor.RefreshValues(result);
    }
}
