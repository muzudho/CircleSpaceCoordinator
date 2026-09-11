namespace CircleSpaceCoordinator.Core.Model;

using CircleSpaceCoordinator.Core.Evaluation;

public sealed record CircleSpaceProject(
    string SchemaVersion,
    string Id,
    string Name,
    Venue Venue,
    IReadOnlyList<DeskType> DeskTypes,
    IReadOnlyList<Participant> Participants,
    EvaluationConfiguration Evaluation,
    IReadOnlyList<Plan> Plans)
{
    public string? Description { get; init; }

    /// <summary>Marks business data that must not be published. This is a visual warning, not access control.</summary>
    public bool IsConfidential { get; init; }

    public IReadOnlyList<GenreStyleDefinition> GenreStyles { get; init; } = [];

    public EditorViewState? EditorView { get; init; }

    public ParticipantTableSource? ParticipantTableSource { get; init; }

    /// <summary>
    /// Physical layouts. Empty means this is a legacy in-memory project whose Plans
    /// have not yet been migrated. New projects must populate both layout lists.
    /// </summary>
    public IReadOnlyList<DeskLayout> DeskLayouts { get; init; } = [];

    /// <summary>Circle layouts, each referring to an item in <see cref="DeskLayouts"/>.</summary>
    public IReadOnlyList<CircleLayout> CircleLayouts { get; init; } = [];
}
