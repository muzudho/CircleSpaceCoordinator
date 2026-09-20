namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Core.Model;

/// <summary>網掛け対応表: adapts legacy genre/block storage to the same editor rows and metadata.</summary>
public static class ShadingTable
{
    public static StyleMappingEntry[] Rows(this PortableMaterial table) => table.Kind switch
    {
        "genre-styles" => (table.GenreStyles ?? []).Select(row => new StyleMappingEntry(row.GenreId, row.PrimaryColor, row.SecondaryColor, row.Pattern)
            { KnowledgeComment = row.KnowledgeComment }).ToArray(),
        "block-styles" => (table.BlockStyles ?? []).Select(row => new StyleMappingEntry(row.BlockNumber, row.PrimaryColor, row.SecondaryColor, row.Pattern)
            { KnowledgeComment = row.KnowledgeComment }).ToArray(),
        _ => throw new InvalidOperationException("網掛け対応表ではありません。"),
    };

    public static ShadingTableMetadata Metadata(this PortableMaterial table) => table.Kind == "block-styles"
        ? (table.TableMetadata ?? new()) with { Name = table.Name, OverallComment = table.OverallComment ?? table.TableMetadata?.OverallComment }
        : new() { Name = table.Name, OverallComment = table.OverallComment, RowOrder = table.GenreCodeOrder ?? [], OrderComment = table.GenreCodeOrderComment };

    public static PortableMaterial WithRows(this PortableMaterial table, IEnumerable<StyleMappingEntry> rows) => table.Kind == "block-styles"
        ? table with { BlockStyles = rows.Select(row => new BlockStyleDefinition(row.Key, row.PrimaryColor, row.SecondaryColor, row.Pattern)
            { KnowledgeComment = row.KnowledgeComment }).ToArray() }
        : table with { GenreStyles = rows.Select(row => new GenreStyleDefinition(row.Key, row.PrimaryColor, row.SecondaryColor, row.Pattern)
            { KnowledgeComment = row.KnowledgeComment }).ToArray() };

    public static PortableMaterial WithOrder(this PortableMaterial table, IReadOnlyList<string> order) => table.Kind == "block-styles"
        ? table with { TableMetadata = table.Metadata() with { RowOrder = order } }
        : table with { GenreCodeOrder = order };

    public static PortableMaterial WithOrderComment(this PortableMaterial table, string? comment) => table.Kind == "block-styles"
        ? table with { TableMetadata = table.Metadata() with { OrderComment = GenreStyleDefinition.NormalizeKnowledgeComment(comment) } }
        : table with { GenreCodeOrderComment = GenreStyleDefinition.NormalizeKnowledgeComment(comment) };

    public static StyleMappingDraft CreateDraft(CircleSpaceProject project, bool blocks)
    {
        if (!blocks) return new GenreStyleDraft(project).Mapping;
        var labels = project.DeskLayouts.Count > 0 ? project.DeskLayouts.SelectMany(layout => layout.SeatLabels)
            : project.Plans.SelectMany(plan => plan.SeatLabels);
        var table = new PortableMaterial("project", "block-styles", project.GetBlockStyleTableName(), project.IsConfidential)
            { BlockStyles = project.BlockStyles.ToArray() };
        return new(project.BlockStyleTable.RowOrder.Concat(labels.Select(label => label.BlockName))
            .Concat(project.BlockStyles.Select(row => row.BlockNumber)), table.Rows(), project.BlockStyleTable.OverallComment);
    }
}
