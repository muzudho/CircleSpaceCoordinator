namespace CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Evaluation;

public sealed record PortableItem(string Id, string Name, CircleSpaceProject Project);
public sealed record PortablePackage(string Name, string Description, IReadOnlyList<string> Tags,
    bool IsConfidential, IReadOnlyList<PortableItem> Items)
{
    public IReadOnlyList<ChannelKnowledge> Knowledge { get; init; } = [];
}
public sealed record PortableImportItem(string ItemId, string NewId, string Name);
public sealed record PortableExportRequest(CircleSpaceProject Project, IReadOnlyList<string> LayoutIds,
    SpaceDefinitionCatalog Definitions, string Name, string Description, IReadOnlyList<string> Tags, bool IsConfidential)
{
    public IReadOnlyList<string> KnowledgeIds { get; init; } = [];
    public bool TemplateOnly { get; init; }
}
public sealed record PortablePreviewRequest(CircleSpaceProject Project, PortablePackage Package,
    IReadOnlyList<PortableImportItem> Selection);
public sealed record PortableTypeMapping(string ItemId, string SourceTypeId, string TargetTypeId);
public sealed record PortablePreview(string VenueName, int LayoutCount, int TypeCount, bool IsConfidential)
{
    public string VenueAction { get; init; } = "保持";
    public int DefinitionCount { get; init; }
    public int RequestCount { get; init; }
    public int KnowledgeCount { get; init; }
    public IReadOnlyList<PortableImportItem> Selection { get; init; } = [];
    public IReadOnlyList<PortableTypeMapping> TypeMappings { get; init; } = [];
}
