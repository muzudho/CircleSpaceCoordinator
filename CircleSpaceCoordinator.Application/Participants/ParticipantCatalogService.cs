namespace CircleSpaceCoordinator.Application.Participants;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;

public sealed record ParticipantImportRow(
    string CircleId,
    string DisplayName,
    int RequiredCellCount,
    string? CombinedWithCircleId = null,
    string? GenreId = null);

public static class ParticipantCatalogService
{
    public static CircleSpaceProject ReplaceParticipants(
        CircleSpaceProject project,
        IReadOnlyList<ParticipantImportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(rows);
        var issues = Validate(rows);
        if (issues.Count > 0)
            throw new ProjectValidationException(issues);

        var existing = project.Participants.ToDictionary(item => item.CircleId, StringComparer.Ordinal);
        var usedInternalIds = project.Participants.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var nextNumber = 1;
        string NewInternalId()
        {
            string candidate;
            do candidate = $"participant-{nextNumber++:0000}";
            while (usedInternalIds.Contains(candidate));
            usedInternalIds.Add(candidate);
            return candidate;
        }

        var participants = rows.Select(row =>
        {
            if (existing.TryGetValue(row.CircleId, out var current))
                return current with
                {
                    CircleId = row.CircleId,
                    DisplayName = row.DisplayName,
                    RequiredCellCount = row.RequiredCellCount,
                    CombinedWithCircleId = NormalizeOptional(row.CombinedWithCircleId),
                    GenreId = NormalizeOptional(row.GenreId),
                };
            return new Participant(NewInternalId(), row.DisplayName, row.RequiredCellCount,
                new Dictionary<string, double>())
            {
                CircleId = row.CircleId,
                CombinedWithCircleId = NormalizeOptional(row.CombinedWithCircleId),
                GenreId = NormalizeOptional(row.GenreId),
            };
        }).ToArray();
        var retainedIds = participants.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var requiredCellsById = participants.ToDictionary(item => item.Id, item => item.RequiredCellCount, StringComparer.Ordinal);
        var plans = project.Plans.Select(plan => plan with
        {
            TemporaryPlacements = plan.TemporaryPlacements
                .Where(item => retainedIds.Contains(item.ParticipantId) && item.OccupiedCells.Count == requiredCellsById[item.ParticipantId]).ToArray(),
            Assignments = plan.Assignments
                .Where(assignment => retainedIds.Contains(assignment.ParticipantId) &&
                    assignment.OccupiedCells.Count == requiredCellsById[assignment.ParticipantId])
                .ToArray(),
        }).ToArray();
        var result = project with { Participants = participants, Plans = plans };
        var projectIssues = ProjectValidator.Validate(result);
        if (projectIssues.Count > 0)
            throw new ProjectValidationException(projectIssues);
        return result;
    }

    private static IReadOnlyList<ValidationIssue> Validate(IReadOnlyList<ParticipantImportRow> rows)
    {
        var issues = new List<ValidationIssue>();
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (string.IsNullOrWhiteSpace(row.CircleId))
                issues.Add(new ValidationIssue("participantImport.circleId.empty", $"rows[{index}]", "サークルIDが空です。"));
            if (string.IsNullOrWhiteSpace(row.DisplayName))
                issues.Add(new ValidationIssue("participantImport.displayName.empty", $"rows[{index}]", "サークル名が空です。"));
            if (row.RequiredCellCount <= 0)
                issues.Add(new ValidationIssue("participantImport.requiredCellCount", $"rows[{index}]", "必要セル数は1以上にしてください。"));
            if (string.Equals(row.CircleId, row.CombinedWithCircleId?.Trim(), StringComparison.Ordinal))
                issues.Add(new ValidationIssue("participantImport.combined.self", $"rows[{index}]", "自分自身を合体先にはできません。"));
        }
        foreach (var duplicate in rows.Where(row => !string.IsNullOrWhiteSpace(row.CircleId))
                     .GroupBy(row => row.CircleId, StringComparer.Ordinal).Where(group => group.Count() > 1))
            issues.Add(new ValidationIssue("participantImport.circleId.duplicate", "rows", $"サークルID「{duplicate.Key}」が重複しています。"));
        var circleIds = rows.Select(row => row.CircleId).ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < rows.Count; index++)
            if (NormalizeOptional(rows[index].CombinedWithCircleId) is { } partner && !circleIds.Contains(partner))
                issues.Add(new ValidationIssue("participantImport.combined.unknown", $"rows[{index}]", $"合体先サークルID「{partner}」が一覧にありません。"));
        return issues;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
