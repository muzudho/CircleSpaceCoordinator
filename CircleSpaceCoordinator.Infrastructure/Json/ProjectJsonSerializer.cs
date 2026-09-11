namespace CircleSpaceCoordinator.Infrastructure.Json;

using System.Text.Json;
using System.Text.Json.Serialization;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;

public static class ProjectJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public static CircleSpaceProject Load(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<ProjectDocument>(json, Options)
            ?? throw new JsonException("The project JSON document is empty.");
        var project = ToCore(document);
        var issues = ProjectValidator.Validate(project);
        if (issues.Count > 0)
            throw new ProjectValidationException(issues);
        return project;
    }

    public static string Save(
        CircleSpaceProject project,
        IReadOnlyList<EvaluationResult>? evaluationResults = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var issues = ProjectValidator.Validate(project);
        if (issues.Count > 0)
            throw new ProjectValidationException(issues);
        return JsonSerializer.Serialize(FromCore(project, evaluationResults), Options) + Environment.NewLine;
    }

    private static CircleSpaceProject ToCore(ProjectDocument source)
    {
        var venue = new Venue(
            source.Venue.Id,
            source.Venue.Name,
            source.Venue.Width,
            source.Venue.Height,
            source.Venue.BlockedCells.Select(ToCore).ToHashSet())
        {
            Zones = source.Venue.Zones.Select(zone => new VenueZone(
                zone.Id,
                zone.Name,
                zone.Cells.Select(ToCore).ToHashSet())).ToArray(),
        };
        var deskTypes = source.DeskTypes.Select(item => new DeskType(
            item.Id,
            item.Name,
            item.Footprint.Select(ToCore).ToArray())).ToArray();
        var participants = source.Participants.Select(item => new Participant(
            item.Id,
            item.DisplayName,
            item.RequiredCellCount,
            new Dictionary<string, double>(item.Features))
        {
            CircleId = string.IsNullOrWhiteSpace(item.CircleId) ? item.Id : item.CircleId,
            CombinedWithCircleId = item.CombinedWithCircleId,
            GenreId = item.GenreId,
            Tags = item.Tags.ToHashSet(),
            SourceValues = new Dictionary<string, string>(item.SourceValues),
        }).ToArray();
        var features = source.Evaluation.Features.Select(item => new EvaluationFeature(
            item.Id,
            item.Name,
            item.Scale,
            item.Offset,
            item.OverallWeight)
        {
            Description = item.Description,
            SourceColumn = item.SourceColumn,
        }).ToArray();
        var weightMaps = source.Evaluation.WeightMaps.Select(item => new WeightMap(
            item.FeatureId,
            item.DefaultWeight,
            ToUniqueDictionary(
                item.Cells,
                cell => ToCore(cell.Position),
                cell => cell.Weight,
                $"evaluation.weightMaps[{item.FeatureId}].cells"))).ToArray();
        var legacyPlans = (source.Plans ?? []).Select(item => new Plan(
            item.Id,
            item.Name,
            item.DeskPlacements.Select(placement => new DeskPlacement(
                placement.Id,
                placement.DeskTypeId,
                ToCore(placement.Anchor),
                ParseOrientation(placement.Orientation))
            {
                DeskNumber = placement.DeskNumber,
            }).ToArray(),
            item.Assignments.Select(assignment => new ParticipantAssignment(
                assignment.ParticipantId,
                assignment.OccupiedCells.Select(ToCore).ToHashSet(),
                ToCore(assignment.ScoringPosition))
            {
                CombinedSpaceId = assignment.CombinedSpaceId,
            }).ToArray())
        {
            Description = item.Description,
            IslandConnectors = item.IslandConnectors.Select(connector => new IslandConnector(
                connector.Id, connector.FirstDeskId, connector.SecondDeskId,
                connector.FirstCell is null ? null : ToCore(connector.FirstCell),
                connector.SecondCell is null ? null : ToCore(connector.SecondCell))).ToArray(),
            DisabledIslandConnections = item.DisabledIslandConnections.Select(connection => new DisabledIslandConnection(
                ToCore(connection.FirstCell), ToCore(connection.SecondCell))).ToArray(),
            FacingRegions = item.FacingRegions.Select(region => new FacingRegion(
                region.Id, ToCore(region.FirstCorner), ToCore(region.SecondCorner))).ToArray(),
            SeatLabels = item.SeatLabels.Select(label => new DeskSeatLabel(
                label.DeskPlacementId,
                ToCore(label.RelativeCell),
                label.BlockName,
                label.SeatName)).ToArray(),
        }).ToArray();

        var deskLayouts = source.DeskLayouts.Select(item => new DeskLayout(
            item.Id, item.Name, item.DeskPlacements.Select(ToCore).ToArray())
        {
            Description = item.Description,
            IslandConnectors = item.IslandConnectors.Select(ToCore).ToArray(),
            DisabledIslandConnections = item.DisabledIslandConnections.Select(ToCore).ToArray(),
            FacingRegions = item.FacingRegions.Select(ToCore).ToArray(),
            SeatLabels = item.SeatLabels.Select(ToCore).ToArray(),
        }).ToArray();
        var circleLayouts = source.CircleLayouts.Select(item => new CircleLayout(
            item.Id, item.Name, item.DeskLayoutId, item.Assignments.Select(ToCore).ToArray())
        {
            Description = item.Description,
            TemporaryPlacements = item.TemporaryPlacements.Select(ToCore).ToArray(),
        }).ToArray();
        var plans = deskLayouts.Length == 0 && circleLayouts.Length == 0
            ? legacyPlans
            : circleLayouts.Select(circle =>
            {
                var desk = deskLayouts.Single(item => item.Id == circle.DeskLayoutId);
                return new Plan(circle.Id, circle.Name, desk.DeskPlacements, circle.Assignments)
                {
                    Description = circle.Description,
                    TemporaryPlacements = circle.TemporaryPlacements,
                    IslandConnectors = desk.IslandConnectors,
                    DisabledIslandConnections = desk.DisabledIslandConnections,
                    FacingRegions = desk.FacingRegions,
                    SeatLabels = desk.SeatLabels,
                };
            }).ToArray();

        return new CircleSpaceProject(
            source.SchemaVersion,
            source.Project.Id,
            source.Project.Name,
            venue,
            deskTypes,
            participants,
            new EvaluationConfiguration(features, weightMaps),
            plans)
        {
            Description = source.Project.Description,
            ParticipantTableSource = source.ParticipantTableSource,
            IsConfidential = source.Project.IsConfidential,
            GenreStyles = source.GenreStyles.Select(item => new GenreStyleDefinition(
                item.GenreId,
                item.PrimaryColor,
                item.SecondaryColor,
                item.Pattern)).OrderBy(item => item.GenreId, StringComparer.Ordinal).ToArray(),
            EditorView = source.EditorView is null
                ? null
                : ToCore(source.EditorView),
            DeskLayouts = deskLayouts,
            CircleLayouts = circleLayouts,
        };
    }

    private static ProjectDocument FromCore(
        CircleSpaceProject source,
        IReadOnlyList<EvaluationResult>? evaluationResults)
    {
        var migrated = LayoutProjection.CommitLegacyPlanEdits(source);
        return new()
    {
        SchemaVersion = source.SchemaVersion,
        ParticipantTableSource = source.ParticipantTableSource,
        Project = new ProjectMetadataDocument
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            IsConfidential = source.IsConfidential,
        },
        EditorView = source.EditorView is null
            ? null
            : new EditorViewDocument
            {
                Zoom = source.EditorView.Zoom,
                OriginX = source.EditorView.OriginX,
                OriginY = source.EditorView.OriginY,
            },
        GenreStyles = source.GenreStyles.Select(item => new GenreStyleDocument
        {
            GenreId = item.GenreId,
            PrimaryColor = item.PrimaryColor,
            SecondaryColor = item.SecondaryColor,
            Pattern = item.Pattern,
        }).OrderBy(item => item.GenreId, StringComparer.Ordinal).ToList(),
        Venue = new VenueDocument
        {
            Id = source.Venue.Id,
            Name = source.Venue.Name,
            Width = source.Venue.Width,
            Height = source.Venue.Height,
            BlockedCells = OrderPositions(source.Venue.BlockedCells).Select(FromCore).ToList(),
            Zones = source.Venue.Zones.Select(zone => new ZoneDocument
            {
                Id = zone.Id,
                Name = zone.Name,
                Cells = OrderPositions(zone.Cells).Select(FromCore).ToList(),
            }).OrderBy(zone => zone.Id, StringComparer.Ordinal).ToList(),
        },
        DeskTypes = source.DeskTypes.Select(item => new DeskTypeDocument
        {
            Id = item.Id,
            Name = item.Name,
            Footprint = item.Footprint.Select(FromCore).ToList(),
        }).ToList(),
        Participants = source.Participants.Select(item => new ParticipantDocument
        {
            Id = item.Id,
            CircleId = item.CircleId,
            CombinedWithCircleId = item.CombinedWithCircleId,
            GenreId = item.GenreId,
            DisplayName = item.DisplayName,
            RequiredCellCount = item.RequiredCellCount,
            Features = item.Features
                .OrderBy(feature => feature.Key, StringComparer.Ordinal)
                .ToDictionary(feature => feature.Key, feature => feature.Value, StringComparer.Ordinal),
            Tags = item.Tags.Order(StringComparer.Ordinal).ToList(),
            SourceValues = item.SourceValues.ToDictionary(pair => pair.Key, pair => pair.Value),
        }).ToList(),
        Evaluation = new EvaluationConfigurationDocument
        {
            Features = source.Evaluation.Features.Select(item => new EvaluationFeatureDocument
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Scale = item.Scale,
                Offset = item.Offset,
                OverallWeight = item.OverallWeight,
                SourceColumn = item.SourceColumn,
            }).ToList(),
            WeightMaps = source.Evaluation.WeightMaps.Select(item => new WeightMapDocument
            {
                FeatureId = item.FeatureId,
                DefaultWeight = item.DefaultWeight,
                Cells = OrderPositions(item.Cells.Keys).Select(position => new WeightedCellDocument
                {
                    Position = FromCore(position),
                    Weight = item.Cells[position],
                }).ToList(),
            }).ToList(),
        },
        DeskLayouts = migrated.DeskLayouts.Select(item => new DeskLayoutDocument
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            DeskPlacements = item.DeskPlacements.Select(FromCore).ToList(),
            IslandConnectors = item.IslandConnectors.Select(FromCore).ToList(),
            DisabledIslandConnections = item.DisabledIslandConnections.Select(FromCore).ToList(),
            FacingRegions = item.FacingRegions.Select(FromCore).ToList(),
            SeatLabels = item.SeatLabels.Select(FromCore).ToList(),
        }).ToList(),
        CircleLayouts = migrated.CircleLayouts.Select(item => new CircleLayoutDocument
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            DeskLayoutId = item.DeskLayoutId,
            Assignments = item.Assignments.Select(FromCore).ToList(),
            TemporaryPlacements = item.TemporaryPlacements.Select(FromCore).ToList(),
        }).ToList(),
        Plans = null,
        EvaluationResults = evaluationResults?.Select(result => new EvaluationResultDocument
        {
            PlanId = result.PlanId,
            TotalScore = result.TotalScore,
            Features = result.Features.Select(feature => new FeatureResultDocument
            {
                FeatureId = feature.FeatureId,
                RawSum = feature.RawSum,
                AdjustedScore = feature.AdjustedScore,
                WeightedScore = feature.WeightedScore,
            }).ToList(),
        }).ToList(),
        };
    }

    private static GridPosition ToCore(PositionDocument source) => new(source.X, source.Y);

    private static DeskPlacement ToCore(DeskPlacementDocument source) => new(
        source.Id, source.DeskTypeId, ToCore(source.Anchor), ParseOrientation(source.Orientation))
    { DeskNumber = source.DeskNumber };

    private static ParticipantAssignment ToCore(AssignmentDocument source) => new(
        source.ParticipantId, source.OccupiedCells.Select(ToCore).ToHashSet(), ToCore(source.ScoringPosition))
    { CombinedSpaceId = source.CombinedSpaceId };

    private static IslandConnector ToCore(IslandConnectorDocument source) => new(
        source.Id, source.FirstDeskId, source.SecondDeskId,
        source.FirstCell is null ? null : ToCore(source.FirstCell),
        source.SecondCell is null ? null : ToCore(source.SecondCell));

    private static DisabledIslandConnection ToCore(DisabledIslandConnectionDocument source) => new(
        ToCore(source.FirstCell), ToCore(source.SecondCell));

    private static FacingRegion ToCore(FacingRegionDocument source) => new(
        source.Id, ToCore(source.FirstCorner), ToCore(source.SecondCorner));

    private static DeskSeatLabel ToCore(DeskSeatLabelDocument source) => new(
        source.DeskPlacementId, ToCore(source.RelativeCell), source.BlockName, source.SeatName);

    private static EditorViewState ToCore(EditorViewDocument source)
    {
        if (!double.IsFinite(source.Zoom) || source.Zoom is < 0.25d or > 4d ||
            !double.IsFinite(source.OriginX) || !double.IsFinite(source.OriginY))
            throw new JsonException("Editor view must contain a zoom from 0.25 to 4 and finite origin coordinates.");
        return new EditorViewState(source.Zoom, source.OriginX, source.OriginY);
    }

    private static PositionDocument FromCore(GridPosition source) => new() { X = source.X, Y = source.Y };

    private static DeskPlacementDocument FromCore(DeskPlacement source) => new()
    { Id = source.Id, DeskTypeId = source.DeskTypeId, Anchor = FromCore(source.Anchor), Orientation = FormatOrientation(source.Orientation), DeskNumber = source.DeskNumber };

    private static AssignmentDocument FromCore(ParticipantAssignment source) => new()
    { ParticipantId = source.ParticipantId, OccupiedCells = OrderPositions(source.OccupiedCells).Select(FromCore).ToList(), ScoringPosition = FromCore(source.ScoringPosition), CombinedSpaceId = source.CombinedSpaceId };

    private static IslandConnectorDocument FromCore(IslandConnector source) => new()
    { Id = source.Id, FirstDeskId = source.FirstDeskId, SecondDeskId = source.SecondDeskId, FirstCell = source.FirstCell is null ? null : FromCore(source.FirstCell.Value), SecondCell = source.SecondCell is null ? null : FromCore(source.SecondCell.Value) };

    private static DisabledIslandConnectionDocument FromCore(DisabledIslandConnection source) => new()
    { FirstCell = FromCore(source.FirstCell), SecondCell = FromCore(source.SecondCell) };

    private static FacingRegionDocument FromCore(FacingRegion source) => new()
    { Id = source.Id, FirstCorner = FromCore(source.FirstCorner), SecondCorner = FromCore(source.SecondCorner) };

    private static DeskSeatLabelDocument FromCore(DeskSeatLabel source) => new()
    { DeskPlacementId = source.DeskPlacementId, RelativeCell = FromCore(source.RelativeCell), BlockName = source.BlockName, SeatName = source.SeatName };

    private static IOrderedEnumerable<GridPosition> OrderPositions(IEnumerable<GridPosition> positions) =>
        positions.OrderBy(position => position.Y).ThenBy(position => position.X);

    private static QuarterTurn ParseOrientation(string value) => value switch
    {
        "north" => QuarterTurn.North,
        "east" => QuarterTurn.East,
        "south" => QuarterTurn.South,
        "west" => QuarterTurn.West,
        _ => throw new JsonException($"Unknown desk orientation '{value}'."),
    };

    private static string FormatOrientation(QuarterTurn value) => value switch
    {
        QuarterTurn.North => "north",
        QuarterTurn.East => "east",
        QuarterTurn.South => "south",
        QuarterTurn.West => "west",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static Dictionary<TKey, TValue> ToUniqueDictionary<TSource, TKey, TValue>(
        IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, TValue> valueSelector,
        string path)
        where TKey : notnull
    {
        var result = new Dictionary<TKey, TValue>();
        foreach (var item in source)
        {
            if (!result.TryAdd(keySelector(item), valueSelector(item)))
                throw new JsonException($"Duplicate key found at '{path}'.");
        }

        return result;
    }

    private sealed class ProjectDocument
    {
        public string SchemaVersion { get; set; } = "";
        public ProjectMetadataDocument Project { get; set; } = new();
        public EditorViewDocument? EditorView { get; set; }
        public ParticipantTableSource? ParticipantTableSource { get; set; }
        public List<GenreStyleDocument> GenreStyles { get; set; } = [];
        public VenueDocument Venue { get; set; } = new();
        public List<DeskTypeDocument> DeskTypes { get; set; } = [];
        public List<ParticipantDocument> Participants { get; set; } = [];
        public EvaluationConfigurationDocument Evaluation { get; set; } = new();
        public List<PlanDocument>? Plans { get; set; }
        public List<DeskLayoutDocument> DeskLayouts { get; set; } = [];
        public List<CircleLayoutDocument> CircleLayouts { get; set; } = [];
        public List<EvaluationResultDocument>? EvaluationResults { get; set; }
    }

    private sealed class EditorViewDocument
    {
        public double Zoom { get; set; }
        public double OriginX { get; set; }
        public double OriginY { get; set; }
    }

    private sealed class GenreStyleDocument
    {
        public string GenreId { get; set; } = "";
        public string PrimaryColor { get; set; } = "";
        public string SecondaryColor { get; set; } = "";
        public string Pattern { get; set; } = "";
    }

    private sealed class ProjectMetadataDocument
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public bool IsConfidential { get; set; }
    }

    private sealed class VenueDocument
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public List<PositionDocument> BlockedCells { get; set; } = [];
        public List<ZoneDocument> Zones { get; set; } = [];
    }

    private sealed class ZoneDocument
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<PositionDocument> Cells { get; set; } = [];
    }

    private sealed class PositionDocument
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    private sealed class DeskTypeDocument
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<PositionDocument> Footprint { get; set; } = [];
    }

    private sealed class ParticipantDocument
    {
        public string Id { get; set; } = "";
        public string? CircleId { get; set; }
        public string? CombinedWithCircleId { get; set; }
        public string? GenreId { get; set; }
        public string DisplayName { get; set; } = "";
        public int RequiredCellCount { get; set; }
        public Dictionary<string, double> Features { get; set; } = [];
        public Dictionary<string, string> SourceValues { get; set; } = [];
        public List<string> Tags { get; set; } = [];
    }

    private sealed class EvaluationConfigurationDocument
    {
        public List<EvaluationFeatureDocument> Features { get; set; } = [];
        public List<WeightMapDocument> WeightMaps { get; set; } = [];
    }

    private sealed class EvaluationFeatureDocument
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public double Scale { get; set; }
        public double Offset { get; set; }
        public double OverallWeight { get; set; }
        public string? SourceColumn { get; set; }
    }

    private sealed class WeightMapDocument
    {
        public string FeatureId { get; set; } = "";
        public double DefaultWeight { get; set; }
        public List<WeightedCellDocument> Cells { get; set; } = [];
    }

    private sealed class WeightedCellDocument
    {
        public PositionDocument Position { get; set; } = new();
        public double Weight { get; set; }
    }

    private sealed class PlanDocument
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public List<DeskPlacementDocument> DeskPlacements { get; set; } = [];
        public List<AssignmentDocument> Assignments { get; set; } = [];
        public List<IslandConnectorDocument> IslandConnectors { get; set; } = [];
        public List<DisabledIslandConnectionDocument> DisabledIslandConnections { get; set; } = [];
        public List<FacingRegionDocument> FacingRegions { get; set; } = [];
        public List<DeskSeatLabelDocument> SeatLabels { get; set; } = [];
    }

    private sealed class DeskLayoutDocument
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public List<DeskPlacementDocument> DeskPlacements { get; set; } = [];
        public List<IslandConnectorDocument> IslandConnectors { get; set; } = [];
        public List<DisabledIslandConnectionDocument> DisabledIslandConnections { get; set; } = [];
        public List<FacingRegionDocument> FacingRegions { get; set; } = [];
        public List<DeskSeatLabelDocument> SeatLabels { get; set; } = [];
    }

    private sealed class CircleLayoutDocument
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string DeskLayoutId { get; set; } = "";
        public List<AssignmentDocument> Assignments { get; set; } = [];
        public List<AssignmentDocument> TemporaryPlacements { get; set; } = [];
    }

    private sealed class IslandConnectorDocument
    {
        public string Id { get; set; } = "";
        public string FirstDeskId { get; set; } = "";
        public string SecondDeskId { get; set; } = "";
        public PositionDocument? FirstCell { get; set; }
        public PositionDocument? SecondCell { get; set; }
    }

    private sealed class DisabledIslandConnectionDocument
    {
        public PositionDocument FirstCell { get; set; } = new();
        public PositionDocument SecondCell { get; set; } = new();
    }

    private sealed class FacingRegionDocument
    {
        public string Id { get; set; } = "";
        public PositionDocument FirstCorner { get; set; } = new();
        public PositionDocument SecondCorner { get; set; } = new();
    }

    private sealed class DeskPlacementDocument
    {
        public string Id { get; set; } = "";
        public string DeskTypeId { get; set; } = "";
        public PositionDocument Anchor { get; set; } = new();
        public string Orientation { get; set; } = "";
        public string? DeskNumber { get; set; }
    }

    private sealed class DeskSeatLabelDocument
    {
        public string DeskPlacementId { get; set; } = "";
        public PositionDocument RelativeCell { get; set; } = new();
        public string BlockName { get; set; } = "";
        public string SeatName { get; set; } = "";
    }

    private sealed class AssignmentDocument
    {
        public string ParticipantId { get; set; } = "";
        public List<PositionDocument> OccupiedCells { get; set; } = [];
        public PositionDocument ScoringPosition { get; set; } = new();
        public string? CombinedSpaceId { get; set; }
    }

    private sealed class EvaluationResultDocument
    {
        public string PlanId { get; set; } = "";
        public double TotalScore { get; set; }
        public List<FeatureResultDocument> Features { get; set; } = [];
    }

    private sealed class FeatureResultDocument
    {
        public string FeatureId { get; set; } = "";
        public double RawSum { get; set; }
        public double AdjustedScore { get; set; }
        public double WeightedScore { get; set; }
    }
}
