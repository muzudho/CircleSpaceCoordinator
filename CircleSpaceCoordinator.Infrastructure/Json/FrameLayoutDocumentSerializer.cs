namespace CircleSpaceCoordinator.Infrastructure.Json;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Model;


/// <summary>A frame-only document. Project serialization stays behind the engine boundary.</summary>
public static class FrameLayoutDocumentSerializer
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

    public static string Save(CircleSpaceProject project)
    {
        var payload = JsonNode.Parse(ProjectJsonSerializer.Save(project))!.AsObject();
        payload.Remove("evaluationResults");
        return JsonSerializer.Serialize(new Document(Kind, 1, project.IsConfidential, JsonSerializer.SerializeToElement(payload)), Options);
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
        var project = ProjectJsonSerializer.Load(document.Project.GetRawText());
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

}
