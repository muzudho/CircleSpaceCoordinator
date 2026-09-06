namespace CircleSpaceCoordinator.Core.Model;

public sealed record Participant(
    string Id,
    string DisplayName,
    int RequiredCellCount,
    IReadOnlyDictionary<string, double> Features)
{
    public string CircleId { get; init; } = Id;

    public string? CombinedWithCircleId { get; init; }

    public string? GenreId { get; init; }

    public IReadOnlySet<string> Tags { get; init; } = new HashSet<string>();

    public double GetFeatureValue(string featureId) =>
        Features.TryGetValue(featureId, out var value) ? value : 0d;
}
