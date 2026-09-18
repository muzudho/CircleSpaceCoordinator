namespace CircleSpaceCoordinator.Infrastructure.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Evaluation;

// Each layout owns its dependency snapshot: definition IDs are local to that snapshot.
public sealed record PortableDocumentItem(
    [property: JsonRequired] string Id, [property: JsonRequired] string Kind,
    [property: JsonRequired] string Name, [property: JsonRequired] JsonElement Data);
public sealed record PortableDocument(
    [property: JsonRequired] string Kind, [property: JsonRequired] int FormatVersion,
    [property: JsonRequired] string Name, [property: JsonRequired] string Description,
    [property: JsonRequired] string[] Tags, [property: JsonRequired] bool IsConfidential,
    [property: JsonRequired] PortableDocumentItem[] Items)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChannelKnowledge[]? Knowledge { get; init; }
}

public static class ProjectPortableSerializer
{
    public const string Kind = "circle-space-project-portable";
    public const int MaximumBytes = 16 * 1024 * 1024;
    public const int MaximumItems = 100;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    public static string Save(string name, string description, string[] tags, bool confidential,
        IReadOnlyList<CircleSpaceProject> projects, ChannelKnowledge[]? knowledge = null)
    {
        var items = projects.Select(project =>
        {
            using var data = JsonDocument.Parse(FrameLayoutDocumentSerializer.Save(project));
            var layout = project.DeskLayouts.Single();
            return new PortableDocumentItem(layout.Id, "frame-layout", layout.Name, data.RootElement.Clone());
        }).ToArray();
        var document = new PortableDocument(Kind, 1, name, description, tags,
            confidential || projects.Any(project => project.IsConfidential) || knowledge?.Any(item => item.IsConfidential) == true, items)
            { Knowledge = knowledge is { Length: > 0 } ? knowledge : null };
        var json = JsonSerializer.Serialize(document, Options) + Environment.NewLine;
        Load(json);
        return json;
    }

    public static (PortableDocument Document, CircleSpaceProject[] Projects) Load(string json)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumBytes)
            throw new InvalidDataException("部分読込みファイルは16 MiB以内にしてください。");
        using var parsed = JsonDocument.Parse(json);
        if (parsed.RootElement.TryGetProperty("kind", out var kind) && kind.GetString() == FrameLayoutDocumentSerializer.Kind)
        {
            var project = FrameLayoutDocumentSerializer.Import(json);
            var layout = project.DeskLayouts.Single();
            return (new PortableDocument(Kind, 1, layout.Name, "", [], project.IsConfidential,
                [new(layout.Id, "frame-layout", layout.Name, parsed.RootElement.Clone())]), [project]);
        }
        var document = JsonSerializer.Deserialize<PortableDocument>(json, Options)
            ?? throw new InvalidDataException("データが空です。");
        if (document.Kind != Kind || document.FormatVersion != 1)
            throw new InvalidDataException("未対応の部分読込み形式・バージョンです。");
        if (string.IsNullOrWhiteSpace(document.Name) || document.Description is null || document.Tags is null ||
            document.Tags.Any(tag => tag is null) || document.Items is null ||
            document.Items.Length + (document.Knowledge?.Length ?? 0) is < 1 or > MaximumItems)
            throw new InvalidDataException("名前・説明・タグと1～100件の項目が必要です。");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var projects = new List<CircleSpaceProject>();
        foreach (var knowledge in document.Knowledge ?? [])
        {
            if (knowledge is null) throw new InvalidDataException("知見が空です。");
            knowledge.Validate();
            if (!ids.Add("knowledge:" + knowledge.Id) || knowledge.IsConfidential && !document.IsConfidential)
                throw new InvalidDataException("知見IDが重複しているかマル秘が一致しません。");
        }
        foreach (var item in document.Items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id) || item.Kind != "frame-layout")
                throw new InvalidDataException("項目IDが重複しているか、未対応の項目種類です。");
            var project = FrameLayoutDocumentSerializer.Import(item.Data.GetRawText());
            var layout = project.DeskLayouts.Single();
            if (layout.Id != item.Id || layout.Name != item.Name || project.IsConfidential && !document.IsConfidential)
                throw new InvalidDataException("項目一覧・実データ・マル秘フラグが一致しません。");
            projects.Add(project);
        }
        return (document, projects.ToArray());
    }
}
