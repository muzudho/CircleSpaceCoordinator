namespace CircleSpaceCoordinator.Application.Participants;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;

public static class ParticipantCatalogService
{
    public static CircleSpaceProject SetFieldColumns(CircleSpaceProject project, ParticipantFieldColumns fields)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(fields);
        var source = project.ParticipantTableSource ?? throw new InvalidOperationException("先に Excel / CSV を取り込んでください。");
        var keys = source.ColumnKeys.ToHashSet(StringComparer.Ordinal);
        if (!keys.Contains(fields.CircleId) || !keys.Contains(fields.DisplayName) ||
            new[] { fields.RequiredCellCount, fields.CombinedWithCircleId, fields.GenreId }
                .Any(key => key is not null && !keys.Contains(key)))
            throw new ArgumentException("取込み表にない列は対応付けできません。", nameof(fields));
        string Value(Participant participant, string key) => participant.SourceValues.TryGetValue(key, out var value)
            ? value.Trim() : throw new ArgumentException($"サークル「{participant.CircleId}」に列「{key}」がありません。");
        var participants = project.Participants.Select(participant =>
        {
            var countText = fields.RequiredCellCount is { } countColumn ? Value(participant, countColumn) : "";
            var count = 1;
            if (countText.Length > 0 && (!int.TryParse(countText, out count) || count < 1))
                throw new ArgumentException($"サークル「{participant.CircleId}」の必要セル数は正の整数で入力してください。");
            return participant with
            {
                CircleId = Value(participant, fields.CircleId),
                DisplayName = Value(participant, fields.DisplayName),
                RequiredCellCount = count,
                CombinedWithCircleId = fields.CombinedWithCircleId is { } combinedColumn
                    ? NormalizeOptional(Value(participant, combinedColumn)) : null,
                GenreId = fields.GenreId is { } genreColumn
                    ? NormalizeOptional(Value(participant, genreColumn)) : null,
            };
        }).ToArray();
        var counts = participants.ToDictionary(item => item.Id, item => item.RequiredCellCount, StringComparer.Ordinal);
        var plans = project.Plans.Select(plan => plan with
        {
            TemporaryPlacements = plan.TemporaryPlacements.Where(item =>
                item.OccupiedCells.Count == counts[item.ParticipantId]).ToArray(),
            Assignments = plan.Assignments.Where(item =>
                item.OccupiedCells.Count == counts[item.ParticipantId]).ToArray(),
        }).ToArray();
        var result = project with { Participants = participants, Plans = plans,
            ParticipantTableSource = source with { FieldColumns = fields } };
        var issues = ProjectValidator.Validate(result);
        if (issues.Count > 0) throw new ProjectValidationException(issues);
        return result;
    }

    public static CircleSpaceProject SetMappingOrder(CircleSpaceProject project, IReadOnlyList<string> order)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(order);
        var source = project.ParticipantTableSource ?? throw new InvalidOperationException("先に Excel / CSV を取り込んでください。");
        if (order.Any(string.IsNullOrWhiteSpace) || order.Distinct(StringComparer.Ordinal).Count() != order.Count)
            throw new ArgumentException("対応表の並び順が正しくありません。", nameof(order));
        return project with { ParticipantTableSource = source with { MappingOrder = order.ToArray() } };
    }

    public static CircleSpaceProject ReplaceParticipants(
        CircleSpaceProject project,
        IReadOnlyList<ParticipantImportRow> rows,
        ParticipantTableSource? source = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(rows);
        if (source is not null && (source.Headers.Count != source.ColumnKeys.Count ||
            source.ColumnKeys.Distinct(StringComparer.Ordinal).Count() != source.ColumnKeys.Count ||
            rows.Any(row => source.ColumnKeys.Any(key => !row.SourceValues.ContainsKey(key)))))
            throw new ArgumentException("取込み元の列情報と値が一致しません。", nameof(source));
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
                    SourceValues = row.SourceValues,
                };
            return new Participant(NewInternalId(), row.DisplayName, row.RequiredCellCount,
                new Dictionary<string, double>())
            {
                CircleId = row.CircleId,
                CombinedWithCircleId = NormalizeOptional(row.CombinedWithCircleId),
                GenreId = NormalizeOptional(row.GenreId),
                SourceValues = row.SourceValues,
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
        var result = project with { Participants = participants, Plans = plans, ParticipantTableSource = source };
        if (source is not null)
        {
            var columns = source.ColumnKeys.ToHashSet(StringComparer.Ordinal);
            var missingIds = result.Evaluation.Features
                .Where(item => item.SourceColumn is not null && !columns.Contains(item.SourceColumn))
                .Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            if (missingIds.Count > 0)
                result = result with
                {
                    Evaluation = result.Evaluation with
                    {
                        Features = result.Evaluation.Features.Select(item => missingIds.Contains(item.Id)
                            ? item with { SourceColumn = null } : item).ToArray(),
                    },
                    Participants = result.Participants.Select(participant => participant with
                    {
                        Features = participant.Features.Where(pair => !missingIds.Contains(pair.Key))
                            .ToDictionary(pair => pair.Key, pair => pair.Value),
                    }).ToArray(),
                };
        }
        result = CircleSpaceCoordinator.Application.Editing.ChannelEditor.RefreshValues(result);
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
