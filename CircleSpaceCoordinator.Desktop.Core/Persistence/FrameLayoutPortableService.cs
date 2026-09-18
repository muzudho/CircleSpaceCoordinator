namespace CircleSpaceCoordinator.Desktop.Core.Persistence;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.EditorClient;

/// <summary>A frame-only document. Project serialization stays behind the engine boundary.</summary>
public static class FrameLayoutPortableService
{
    public const string Kind = "circle-space-frame-layout";
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };
    private sealed record Document(
        [property: JsonRequired] string Kind,
        [property: JsonRequired] int FormatVersion,
        [property: JsonRequired] bool IsConfidential,
        [property: JsonRequired] JsonElement Project);

    public static string Export(CircleSpaceProject project, string layoutId, SpaceDefinitionCatalog commonDefinitions)
    {
        var layout = project.DeskLayouts.Single(item => item.Id == layoutId);
        var catalog = layout.Definitions ?? commonDefinitions;
        catalog.Validate(requireRepresentativeCell: false);
        var types = project.DeskTypes.Where(type => layout.DeskPlacements.Any(p => p.DeskTypeId == type.Id)).ToArray();
        var needed = catalog.Requests.SelectMany(request => request.Targets).Select(target => target.TypeId).ToHashSet();
        foreach (var type in types)
            if (type.Space is { } space) needed.Add(space.DefinitionId);
        var definitions = catalog.Types.Where(type => needed.Contains(type.Id)).ToList();
        // Placed frames retain their exact historical shape, even if the template was edited later.
        foreach (var type in types)
            if (type.Space is { } space)
            {
                var historical = new SpaceTypeDefinition(space.DefinitionId, type.Name, space.Kind, space.Width, space.Height,
                    space.Cells.Select(cell => new SpaceCell(cell.X, cell.Y, cell.Area)).ToArray(), space.Edges)
                    { Connections = space.Connections };
                var existing = definitions.FirstOrDefault(definition => definition.Id == space.DefinitionId);
                if (existing is null) definitions.Add(historical);
                else if (!SameShape(existing, historical))
                    throw new InvalidDataException($"フレーム「{type.Name}」の配置時の定義とカタログの定義が異なります。\nフレーム定義を配置時の内容に揃えるか、現在の定義でフレームを配置し直してから書き出してください。");
            }
        catalog = catalog with { Types = definitions.ToArray() };
        catalog.Validate(requireRepresentativeCell: false);
        var confidential = project.IsConfidential || layout.IsConfidential;
        var portable = new CircleSpaceProject(project.SchemaVersion, "portable-frame-layout", layout.Name,
            project.Venue, types, [], new EvaluationConfiguration([], []), [])
        {
            IsConfidential = confidential,
            BlockStyles = project.BlockStyles,
            DeskLayouts = [layout with { Definitions = catalog, IsConfidential = confidential }],
        };
        var payload = JsonNode.Parse(EditorConnection.Current.Encode(portable))!.AsObject();
        payload.Remove("evaluationResults");
        return JsonSerializer.Serialize(new Document(Kind, 1, confidential, JsonSerializer.SerializeToElement(payload)), Options) + Environment.NewLine;
    }

    /// <summary>Validates without changing any file, catalog or open workspace.</summary>
    public static CircleSpaceProject Import(string json)
    {
        var document = JsonSerializer.Deserialize<Document>(json, Options)
            ?? throw new InvalidDataException("フレーム配置データが空です。");
        if (document.Kind != Kind || document.FormatVersion != 1)
            throw new InvalidDataException("未対応のフレーム配置データ形式です。");
        if (document.Project.ValueKind != JsonValueKind.Object ||
            document.Project.TryGetProperty("plans", out _) || document.Project.TryGetProperty("evaluationResults", out _) ||
            document.Project.TryGetProperty("channelKnowledge", out _) ||
            !document.Project.TryGetProperty("project", out var metadata) ||
            !metadata.TryGetProperty("isConfidential", out _) ||
            !document.Project.TryGetProperty("deskLayouts", out var layouts) || layouts.ValueKind != JsonValueKind.Array ||
            layouts.GetArrayLength() != 1 || !layouts[0].TryGetProperty("isConfidential", out _))
            throw new InvalidDataException("フレーム配置とマル秘フラグを持つ専用形式のファイルが必要です。");
        var project = EditorConnection.Current.Decode(document.Project.GetRawText());
        if (project.DeskLayouts.Count != 1 || project.CircleLayouts.Count != 0 || project.Plans.Count != 0 ||
            project.Participants.Count != 0 || project.ParticipantTableSource is not null ||
            project.EditorView is not null || project.ExportPlanId is not null || project.GenreStyles.Count != 0 ||
            project.Evaluation.Features.Count != 0 || project.Evaluation.WeightMaps.Count != 0 || project.ChannelKnowledge.Count != 0)
            throw new InvalidDataException("フレーム配置データには１つのフレーム配置だけを含めてください。");
        var layout = project.DeskLayouts[0];
        if (layout.Definitions is null)
            throw new InvalidDataException("フレーム定義と申込スペース定義が必要です。");
        layout.Definitions.Validate(requireRepresentativeCell: false);
        if (document.IsConfidential != project.IsConfidential || document.IsConfidential != layout.IsConfidential)
            throw new InvalidDataException("マル秘フラグが一致していません。");
        return project with { Id = "event-" + Guid.NewGuid().ToString("N") };
    }

    public static void SaveNewEvent(string path, CircleSpaceProject project)
    {
        // Refuse replacement: an import never overwrites an existing event.
        var json = EditorConnection.Current.Encode(project);
        SaveDocument(path, json, overwrite: false);
    }

    public static void SaveDocument(string path, string json, bool overwrite)
    {
        var fullPath = Path.GetFullPath(path);
        var temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, fullPath, overwrite);
        }
        finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
    }

    private static bool SameShape(SpaceTypeDefinition first, SpaceTypeDefinition second) =>
        first.Kind == second.Kind && first.Width == second.Width && first.Height == second.Height &&
        first.Edges.SequenceEqual(second.Edges) &&
        first.NormalizeCellStates().Cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X)
            .SequenceEqual(second.NormalizeCellStates().Cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X)) &&
        EffectiveConnections(first).SetEquals(EffectiveConnections(second));

    private static HashSet<FrameCellConnection> EffectiveConnections(SpaceTypeDefinition definition) =>
        (definition.Connections ?? FrameCellConnection.Adjacent(definition.Cells.Where(cell => cell.Area > 0)
            .Select(cell => new GridPosition(cell.X, cell.Y)).ToHashSet())).Select(link => link.Normalize()).ToHashSet();
}
