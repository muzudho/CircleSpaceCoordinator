namespace CircleSpaceCoordinator.Core.Model;

/// <summary>Shared metadata for a named shading table, independent of its row keys.</summary>
public sealed record ShadingTableMetadata
{
    public string? Name { get; init; }
    public string? OverallComment { get; init; }
    public IReadOnlyList<string> RowOrder { get; init; } = [];
    public string? OrderComment { get; init; }

    public void Validate()
    {
        if (Name is not null) GenreStyleDefinition.NormalizeTableName(Name);
        GenreStyleDefinition.NormalizeKnowledgeComment(OverallComment);
        GenreStyleDefinition.NormalizeKnowledgeComment(OrderComment);
        if (RowOrder is null || RowOrder.Any(string.IsNullOrWhiteSpace) || RowOrder.Distinct(StringComparer.Ordinal).Count() != RowOrder.Count)
            throw new InvalidDataException("網掛け対応表の並び順に空欄または重複があります。");
    }
}
