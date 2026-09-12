namespace CircleSpaceCoordinator.Core.Validation;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;

public static class ProjectValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(CircleSpaceProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var issues = new List<ValidationIssue>();

        if (project.ParticipantTableSource is { } source &&
            (source.FileName is null || source.SheetName is null || source.Headers is null || source.ColumnKeys is null ||
             source.Headers.Count != source.ColumnKeys.Count || source.Headers.Any(header => header is null) ||
             source.ColumnKeys.Any(string.IsNullOrEmpty) || source.ColumnKeys.Distinct(StringComparer.Ordinal).Count() != source.ColumnKeys.Count))
            Add("participantTableSource.invalid", "participantTableSource", "取込み元の見出しと列キーを確認してください。");

        if (project.SchemaVersion != "1.0")
            Add("schema.unsupported", "schemaVersion", "Only schema version 1.0 is supported.");

        if (project.Venue.Width <= 0 || project.Venue.Height <= 0)
            Add("venue.size", "venue", "Venue width and height must be positive.");

        foreach (var cell in project.Venue.BlockedCells.Where(cell => !project.Venue.Contains(cell)))
            Add("venue.blocked.outOfBounds", "venue.blockedCells", $"Blocked cell {cell} is outside the venue.");

        CheckUnique(project.DeskTypes.Select(item => item.Id), "deskTypes");
        CheckUnique(project.Venue.Zones.Select(item => item.Id), "venue.zones");
        CheckUnique(project.Participants.Select(item => item.Id), "participants");
        CheckUnique(project.Participants.Select(item => item.CircleId), "participants.circleId");
        CheckUnique(project.Evaluation.Features.Select(item => item.Id), "evaluation.features");
        CheckUnique(project.Evaluation.WeightMaps.Select(item => item.FeatureId), "evaluation.weightMaps");
        CheckUnique(project.GenreStyles.Select(item => item.GenreId), "genreStyles");
        CheckUnique(project.Plans.Select(item => item.Id), "plans");
        CheckUnique(project.DeskLayouts.Select(item => item.Id), "deskLayouts");
        CheckUnique(project.CircleLayouts.Select(item => item.Id), "circleLayouts");

        if (project.DeskLayouts.Count != 0 || project.CircleLayouts.Count != 0)
        {
            if (project.DeskLayouts.Count == 0)
                Add("deskLayout.required", "deskLayouts", "A project with circle layouts must contain desk layouts.");
            var deskLayoutIds = project.DeskLayouts.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var circleLayout in project.CircleLayouts)
            {
                if (string.IsNullOrWhiteSpace(circleLayout.DeskLayoutId) || !deskLayoutIds.Contains(circleLayout.DeskLayoutId))
                    Add("circleLayout.deskLayout.missing", $"circleLayouts[{circleLayout.Id}].deskLayoutId", "Circle layout must refer to an existing desk layout.");
            }
        }

        var deskTypes = project.DeskTypes.GroupBy(item => item.Id).ToDictionary(group => group.Key, group => group.First());
        var participants = project.Participants.GroupBy(item => item.Id).ToDictionary(group => group.Key, group => group.First());
        var participantCircleIds = project.Participants.Select(item => item.CircleId).ToHashSet(StringComparer.Ordinal);
        var featureIds = project.Evaluation.Features.Select(item => item.Id).ToHashSet();

        foreach (var style in project.GenreStyles)
        {
            if (string.IsNullOrWhiteSpace(style.GenreId) || string.IsNullOrWhiteSpace(style.PrimaryColor) ||
                string.IsNullOrWhiteSpace(style.SecondaryColor) || string.IsNullOrWhiteSpace(style.Pattern))
                Add("genreStyle.empty", $"genreStyles[{style.GenreId}]", "Genre style fields must not be empty.");
        }

        foreach (var zone in project.Venue.Zones)
        {
            foreach (var cell in zone.Cells.Where(cell => !project.Venue.Contains(cell)))
                Add("venue.zone.outOfBounds", $"venue.zones[{zone.Id}]", $"Zone cell {cell} is outside the venue.");
        }

        foreach (var deskType in project.DeskTypes)
        {
            if (deskType.Space is { } space &&
                (string.IsNullOrWhiteSpace(space.DefinitionId) || space.Kind is not ("机" or "場所" or "ブース") ||
                 space.Width is < 1 or > 12 || space.Height is < 1 or > 12 ||
                 space.Cells is null || space.Edges is null || space.Edges.Count != 4 ||
                 space.Edges.Any(edge => edge is not ("開放" or "壁" or "入口" or "正面")) ||
                 space.Cells.Any(cell => cell is null || cell.X < 0 || cell.Y < 0 || cell.X >= space.Width || cell.Y >= space.Height || cell.Area is < 0 or > 9) ||
                 space.Cells.Count != deskType.Footprint.Count ||
                 !space.Cells.Select(cell => new GridPosition(cell.X, cell.Y)).ToHashSet().SetEquals(deskType.Footprint)))
                Add("deskType.space.invalid", $"deskTypes[{deskType.Id}].space", "Space details must describe the footprint, allocation areas and four edges.");
            if (deskType.Footprint.Count == 0)
                Add("deskType.empty", $"deskTypes[{deskType.Id}]", "A desk footprint must contain at least one cell.");
            if (deskType.Footprint.Distinct().Count() != deskType.Footprint.Count)
                Add("deskType.duplicateCell", $"deskTypes[{deskType.Id}].footprint", "A desk footprint contains duplicate cells.");
            if (!deskType.Footprint.Contains(new GridPosition(0, 0)))
                Add("deskType.missingAnchor", $"deskTypes[{deskType.Id}].footprint", "A desk footprint must contain its anchor cell (0, 0).");
        }

        foreach (var participant in project.Participants)
        {
            if (string.IsNullOrWhiteSpace(participant.CircleId))
                Add("participant.circleId.empty", $"participants[{participant.Id}].circleId", "CircleId must not be empty.");
            if (participant.CombinedWithCircleId is { } partnerCircleId)
            {
                if (string.Equals(participant.CircleId, partnerCircleId, StringComparison.Ordinal))
                    Add("participant.combined.self", $"participants[{participant.Id}].combinedWithCircleId", "A participant cannot be combined with itself.");
                else if (!participantCircleIds.Contains(partnerCircleId))
                    Add("participant.combined.unknown", $"participants[{participant.Id}].combinedWithCircleId", $"Combined partner circle ID '{partnerCircleId}' does not exist.");
            }
            if (participant.RequiredCellCount <= 0)
                Add("participant.requiredCellCount", $"participants[{participant.Id}]", "RequiredCellCount must be positive.");
            foreach (var feature in participant.Features)
            {
                if (!featureIds.Contains(feature.Key))
                    Add("participant.feature.unknown", $"participants[{participant.Id}].features[{feature.Key}]", "The evaluation feature does not exist.");
                if (!double.IsFinite(feature.Value))
                    Add("number.nonFinite", $"participants[{participant.Id}].features[{feature.Key}]", "A feature value must be finite.");
            }
        }

        foreach (var feature in project.Evaluation.Features)
        {
            if (!double.IsFinite(feature.Scale) || !double.IsFinite(feature.Offset) || !double.IsFinite(feature.OverallWeight))
                Add("number.nonFinite", $"evaluation.features[{feature.Id}]", "Scale, offset and overall weight must be finite.");
        }

        foreach (var weightMap in project.Evaluation.WeightMaps)
        {
            if (!featureIds.Contains(weightMap.FeatureId))
                Add("weightMap.feature.unknown", $"evaluation.weightMaps[{weightMap.FeatureId}]", "The evaluation feature does not exist.");
            if (!double.IsFinite(weightMap.DefaultWeight))
                Add("number.nonFinite", $"evaluation.weightMaps[{weightMap.FeatureId}].defaultWeight", "A weight must be finite.");
            foreach (var weightedCell in weightMap.Cells)
            {
                if (!project.Venue.Contains(weightedCell.Key))
                    Add("weightMap.cell.outOfBounds", $"evaluation.weightMaps[{weightMap.FeatureId}]", $"Weighted cell {weightedCell.Key} is outside the venue.");
                if (!double.IsFinite(weightedCell.Value))
                    Add("number.nonFinite", $"evaluation.weightMaps[{weightMap.FeatureId}]", "A weight must be finite.");
            }
        }

        foreach (var featureId in featureIds)
        {
            if (project.Evaluation.WeightMaps.Count(map => map.FeatureId == featureId) != 1)
                Add("weightMap.feature.count", $"evaluation.weightMaps[{featureId}]", "Each evaluation feature must have exactly one weight map.");
        }

        foreach (var plan in project.Plans)
            ValidatePlan(plan, project.Venue, deskTypes, participants, Add);

        return issues;

        void Add(string code, string path, string message) => issues.Add(new ValidationIssue(code, path, message));

        void CheckUnique(IEnumerable<string> ids, string path)
        {
            foreach (var duplicate in ids.GroupBy(id => id).Where(group => group.Count() > 1))
                Add("id.duplicate", path, $"ID '{duplicate.Key}' is duplicated.");
        }
    }

    private static void ValidatePlan(
        Plan plan,
        Venue venue,
        IReadOnlyDictionary<string, DeskType> deskTypes,
        IReadOnlyDictionary<string, Participant> participants,
        Action<string, string, string> add)
    {
        var path = $"plans[{plan.Id}]";
        foreach (var duplicate in plan.DeskPlacements.GroupBy(item => item.Id).Where(group => group.Count() > 1))
            add("id.duplicate", $"{path}.deskPlacements", $"Desk placement ID '{duplicate.Key}' is duplicated.");
        foreach (var duplicate in plan.IslandConnectors.GroupBy(item => item.Id).Where(group => group.Count() > 1))
            add("id.duplicate", $"{path}.islandConnectors", $"Island connector ID '{duplicate.Key}' is duplicated.");
        foreach (var duplicate in plan.FacingRegions.GroupBy(item => item.Id).Where(group => group.Count() > 1))
            add("id.duplicate", $"{path}.facingRegions", $"Facing region ID '{duplicate.Key}' is duplicated.");
        foreach (var duplicate in plan.SeatLabels.GroupBy(item => (item.DeskPlacementId, item.RelativeCell)).Where(group => group.Count() > 1))
            add("seatLabel.cell.duplicate", $"{path}.seatLabels", "More than one seat label is assigned to a desk cell.");
        foreach (var connector in plan.IslandConnectors.Where(item => item.FirstDeskId == item.SecondDeskId))
            add("islandConnector.sameDesk", $"{path}.islandConnectors[{connector.Id}]", "An island connector must join two different desks.");
        foreach (var region in plan.FacingRegions)
        {
            var regionPath = $"{path}.facingRegions[{region.Id}]";
            if (!venue.Contains(region.FirstCorner) || !venue.Contains(region.SecondCorner))
                add("facingRegion.outOfBounds", regionPath, "A facing region must stay inside the venue.");
        }

        var deskCells = new HashSet<GridPosition>();
        var deskCellsByPlacement = new Dictionary<string, IReadOnlySet<GridPosition>>();
        foreach (var placement in plan.DeskPlacements)
        {
            if (!deskTypes.TryGetValue(placement.DeskTypeId, out var deskType))
            {
                add("deskPlacement.type.unknown", $"{path}.deskPlacements[{placement.Id}]", $"Desk type '{placement.DeskTypeId}' does not exist.");
                continue;
            }

            var occupiedByDesk = placement.GetOccupiedCells(deskType);
            deskCellsByPlacement.TryAdd(placement.Id, occupiedByDesk);
            foreach (var cell in occupiedByDesk)
            {
                if (!venue.Contains(cell))
                    add("deskPlacement.outOfBounds", $"{path}.deskPlacements[{placement.Id}]", $"Desk cell {cell} is outside the venue.");
                else if (venue.BlockedCells.Contains(cell))
                    add("deskPlacement.blocked", $"{path}.deskPlacements[{placement.Id}]", $"Desk cell {cell} is blocked.");
                if (!deskCells.Add(cell))
                    add("deskPlacement.overlap", $"{path}.deskPlacements[{placement.Id}]", $"Desk cell {cell} overlaps another desk.");
            }
        }

        foreach (var label in plan.SeatLabels)
        {
            var labelPath = $"{path}.seatLabels[{label.DeskPlacementId}]";
            if (string.IsNullOrWhiteSpace(label.BlockName) && string.IsNullOrWhiteSpace(label.SeatName))
                add("seatLabel.empty", labelPath, "A seat label needs a block name or a seat name.");
            var placement = plan.DeskPlacements.SingleOrDefault(item => item.Id == label.DeskPlacementId);
            if (placement is null)
            {
                add("seatLabel.desk.unknown", labelPath, $"Desk placement '{label.DeskPlacementId}' does not exist.");
                continue;
            }
            if (deskTypes.TryGetValue(placement.DeskTypeId, out var deskType) && !deskType.Footprint.Contains(label.RelativeCell))
                add("seatLabel.cell.invalid", labelPath, "The seat label does not refer to a cell of its desk.");
        }

        var assignedParticipants = new HashSet<string>();
        var assignedCells = new HashSet<GridPosition>();
        foreach (var assignment in plan.Assignments)
        {
            var assignmentPath = $"{path}.assignments[{assignment.ParticipantId}]";
            if (!participants.TryGetValue(assignment.ParticipantId, out var participant))
                add("assignment.participant.unknown", assignmentPath, $"Participant '{assignment.ParticipantId}' does not exist.");
            else if (assignment.OccupiedCells.Count != participant.RequiredCellCount)
                add("assignment.cellCount", assignmentPath, $"Participant requires {participant.RequiredCellCount} cell(s), but {assignment.OccupiedCells.Count} are assigned.");

            if (!assignedParticipants.Add(assignment.ParticipantId))
                add("assignment.participant.duplicate", assignmentPath, "A participant is assigned more than once in the plan.");

            foreach (var cell in assignment.OccupiedCells)
            {
                if (!deskCells.Contains(cell))
                    add("assignment.cell.withoutDesk", assignmentPath, $"Assigned cell {cell} is not occupied by a desk.");
                if (!assignedCells.Add(cell))
                    add("assignment.cell.duplicate", assignmentPath, $"Assigned cell {cell} is used by more than one participant.");
            }

            if (!assignment.OccupiedCells.Contains(assignment.ScoringPosition))
                add("assignment.scoringPosition", assignmentPath, "The scoring position (the former Excel circle-ID cell) must be one of the participant's occupied cells.");
            if (assignment.CombinedSpaceId is not null && string.IsNullOrWhiteSpace(assignment.CombinedSpaceId))
                add("combinedSpace.id.empty", assignmentPath, "A combined space ID must not be empty.");
        }

        foreach (var temporary in plan.TemporaryPlacements)
        {
            var temporaryPath = $"{path}.temporaryPlacements[{temporary.ParticipantId}]";
            if (!participants.TryGetValue(temporary.ParticipantId, out var participant))
                add("temporary.participant.unknown", temporaryPath, "Unknown temporarily placed participant.");
            else if (temporary.OccupiedCells.Count != participant.RequiredCellCount)
                add("temporary.cellCount", temporaryPath, "Temporary placement has the wrong cell count.");
            if (!assignedParticipants.Add(temporary.ParticipantId))
                add("temporary.participant.duplicate", temporaryPath, "Participant is already placed.");
            if (!temporary.OccupiedCells.Contains(temporary.ScoringPosition))
                add("temporary.position", temporaryPath, "Temporary position must be an occupied cell.");
            foreach (var cell in temporary.OccupiedCells)
            {
                if (deskCells.Contains(cell) || venue.BlockedCells.Contains(cell))
                    add("temporary.cell.obstructed", temporaryPath, "Temporary placement overlaps a desk or pillar.");
                if (!assignedCells.Add(cell))
                    add("temporary.cell.overlap", temporaryPath, "Temporary placements overlap.");
            }
        }

        foreach (var group in plan.Assignments.Concat(plan.TemporaryPlacements)
                     .Where(assignment => assignment.CombinedSpaceId is not null)
                     .GroupBy(assignment => assignment.CombinedSpaceId!))
        {
            var combinedPath = $"{path}.combinedSpaces[{group.Key}]";
            var assignments = group.ToArray();
            if (assignments.Length != 2)
            {
                add("combinedSpace.participantCount", combinedPath, "A combined space must be shared by exactly two participants.");
                continue;
            }

            var combinedCells = assignments.SelectMany(assignment => assignment.OccupiedCells).ToHashSet();
            if (assignments.Any(item => plan.TemporaryPlacements.Any(temporary => temporary.ParticipantId == item.ParticipantId)))
                continue;
            if (!deskCellsByPlacement.Values.Any(cells => cells.IsSupersetOf(combinedCells)))
                add("combinedSpace.splitAcrossDesks", combinedPath, "Both participants in a combined space must use cells on the same physical desk.");
        }
    }
}
