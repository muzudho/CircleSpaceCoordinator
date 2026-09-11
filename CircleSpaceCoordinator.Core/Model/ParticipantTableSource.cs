namespace CircleSpaceCoordinator.Core.Model;

/// <summary>Origin and ordered columns of the last successful import. Values live in Participant.SourceValues.</summary>
public sealed record ParticipantTableSource(
    string FileName,
    string SheetName,
    IReadOnlyList<string> Headers,
    IReadOnlyList<string> ColumnKeys);
