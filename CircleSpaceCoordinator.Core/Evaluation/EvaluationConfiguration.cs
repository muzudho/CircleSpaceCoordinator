namespace CircleSpaceCoordinator.Core.Evaluation;

using CircleSpaceCoordinator.Core.Geometry;

public sealed record EvaluationFeature(
    string Id,
    string Name,
    double Scale,
    double Offset,
    double OverallWeight)
{
    public CircleSpaceCoordinator.Core.Model.PersonCredits? Credits { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [System.Text.Json.Serialization.JsonPropertyName("comment-for-channel")]
    public string? CommentForChannel { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [System.Text.Json.Serialization.JsonPropertyName("comment-for-weight")]
    public string? CommentForWeight { get; init; }
    public string? Description { get; init; }
    public string? SourceColumn { get; init; }
    public ChannelInputRule? InputRule { get; init; }
    public string? Purpose { get; init; }
    public bool IsConfidential { get; init; }
}

public sealed record WeightMap(
    string FeatureId,
    double DefaultWeight,
    IReadOnlyDictionary<GridPosition, double> Cells)
{
    public double GetWeight(GridPosition position) =>
        Cells.TryGetValue(position, out var weight) ? weight : DefaultWeight;
}

public sealed record EvaluationConfiguration(
    IReadOnlyList<EvaluationFeature> Features,
    IReadOnlyList<WeightMap> WeightMaps);

public sealed record FeatureEvaluationResult(
    string FeatureId,
    double RawSum,
    double AdjustedScore,
    double WeightedScore);

public sealed record EvaluationResult(
    string PlanId,
    double TotalScore,
    IReadOnlyList<FeatureEvaluationResult> Features);
