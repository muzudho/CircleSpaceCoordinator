namespace CircleSpaceCoordinator.Application.Participants;

public sealed record ParticipantImportRow(
    string CircleId,
    string DisplayName,
    int RequiredCellCount,
    string? CombinedWithCircleId = null,
    string? GenreId = null)
{
    public IReadOnlyDictionary<string, string> SourceValues { get; init; } = new Dictionary<string, string>();
}

