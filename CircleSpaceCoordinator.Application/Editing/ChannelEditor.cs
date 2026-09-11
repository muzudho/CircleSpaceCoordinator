namespace CircleSpaceCoordinator.Application.Editing;

using System.Globalization;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;

public static class ChannelEditor
{
    public static CircleSpaceProject Upsert(CircleSpaceProject project, string id, string name, string? sourceColumn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        name = name.Trim();
        if (name == "番地" || project.Evaluation.Features.Any(item => item.Id != id && item.Name == name))
            throw new ArgumentException("番地以外の、重複しないチャンネル名を入力してください。");
        if (sourceColumn is not null && !project.Participants.Any(item => item.SourceValues.ContainsKey(sourceColumn)))
            throw new ArgumentException("対応する列がありません。参加サークル一覧を取り込んでください。");
        var existing = project.Evaluation.Features.FirstOrDefault(item => item.Id == id);
        var feature = (existing ?? new EvaluationFeature(id, name, 1, 0, 1)) with { Name = name, SourceColumn = sourceColumn };
        var features = existing is null ? project.Evaluation.Features.Append(feature).ToArray()
            : project.Evaluation.Features.Select(item => item.Id == id ? feature : item).ToArray();
        var maps = project.Evaluation.WeightMaps.Any(item => item.FeatureId == id)
            ? project.Evaluation.WeightMaps
            : project.Evaluation.WeightMaps.Append(new WeightMap(id, 0, new Dictionary<GridPosition, double>())).ToArray();
        var result = project with { Evaluation = new EvaluationConfiguration(features, maps) };
        if (sourceColumn is null && existing?.SourceColumn is not null)
            result = result with { Participants = result.Participants.Select(participant => participant with
            {
                Features = participant.Features.Where(pair => pair.Key != id).ToDictionary(pair => pair.Key, pair => pair.Value),
            }).ToArray() };
        return RefreshValues(result);
    }

    public static CircleSpaceProject Remove(CircleSpaceProject project, string id) => project with
    {
        Evaluation = new EvaluationConfiguration(
            project.Evaluation.Features.Where(item => item.Id != id).ToArray(),
            project.Evaluation.WeightMaps.Where(item => item.FeatureId != id).ToArray()),
        Participants = project.Participants.Select(participant => participant with
        {
            Features = participant.Features.Where(pair => pair.Key != id).ToDictionary(pair => pair.Key, pair => pair.Value),
        }).ToArray(),
    };

    public static CircleSpaceProject SetWeights(CircleSpaceProject project, string planId, string id,
        IReadOnlyList<GridPosition> cells, double weight)
    {
        if (!double.IsFinite(weight) || weight < 0 || weight > 1)
            throw new ArgumentOutOfRangeException(nameof(weight), "重みは 0～1 の実数で入力してください。");
        var plan = project.Plans.Single(item => item.Id == planId);
        var types = project.DeskTypes.ToDictionary(item => item.Id);
        var deskCells = plan.DeskPlacements.SelectMany(desk => desk.GetOccupiedCells(types[desk.DeskTypeId])).ToHashSet();
        if (cells.Count == 0 || cells.Any(cell => !deskCells.Contains(cell)))
            throw new ArgumentException("机上のセルを選んでください。");
        var map = project.Evaluation.WeightMaps.Single(item => item.FeatureId == id);
        var updated = map.Cells.ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var cell in cells) updated[cell] = weight;
        return project with { Evaluation = project.Evaluation with
        {
            WeightMaps = project.Evaluation.WeightMaps.Select(item => item.FeatureId == id ? map with { Cells = updated } : item).ToArray(),
        } };
    }

    public static CircleSpaceProject RefreshValues(CircleSpaceProject project) => project with
    {
        Participants = project.Participants.Select(participant =>
        {
            var values = participant.Features.ToDictionary(pair => pair.Key, pair => pair.Value);
            foreach (var feature in project.Evaluation.Features.Where(item => item.SourceColumn is not null))
            {
                if (!participant.SourceValues.TryGetValue(feature.SourceColumn!, out var text))
                    throw new ArgumentException($"取込みデータに列「{feature.SourceColumn}」がありません。");
                var value = 0d;
                if (!string.IsNullOrWhiteSpace(text) &&
                    (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || !double.IsFinite(value)))
                    throw new ArgumentException($"サークルID「{participant.CircleId}」の列「{feature.SourceColumn}」は有限の数値で入力してください。");
                values[feature.Id] = value;
            }
            return participant with { Features = values };
        }).ToArray(),
    };
}
