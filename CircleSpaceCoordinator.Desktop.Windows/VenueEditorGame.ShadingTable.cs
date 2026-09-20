namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Engine.Model;

public sealed partial class VenueEditorGame
{
    private bool mappingBlocks;
    private string MappingMaterialKind => mappingBlocks ? "block-styles" : "genre-styles";
    private string MappingRowLabel => mappingBlocks ? "ブロック" : "ジャンル";

    // 網掛け対応表: both callers share editing, package exchange, comments and autosave.
    private void OpenShadingTable(bool blocks)
    {
        if (workspace is not { } owner) return;
        mappingBlocks = blocks;
        var project = owner.Project;
        var draft = ShadingTable.CreateDraft(project, blocks);
        var metadata = blocks ? project.BlockStyleTable : new ShadingTableMetadata
        {
            Name = project.GetGenreCodeTableName(), OverallComment = project.GenreStyleComment,
            RowOrder = project.GenreCodeOrder, OrderComment = project.GenreCodeOrderComment,
        };
        mappingGenreCodeTableName = blocks ? project.GetBlockStyleTableName() : project.GetGenreCodeTableName();
        mappingGenreCodeOrder = metadata.RowOrder.ToArray();
        genreCodeOrder = mappingGenreCodeOrder.ToArray();
        mappingGenreCodeOrderComment = metadata.OrderComment;
        genreCodeSort = mappingGenreCodeOrder.Length > 0;
        genreSpaceSort = genreOrdinalSort = false;
        if (genreCodeSort) draft.ReorderRows(mappingGenreCodeOrder);
        OpenShadingTableEditor(draft, blocks ? "ブロック番号" : "ジャンルコード", "この網掛け対応表に行はありません。［新規作成］で追加できます。",
            (styles, log, handle, date) =>
            {
                var credits = GenreProjectChanged || genreProjectRequiresComment
                    ? (mappingPreviousCredits ?? new PersonCredits()).WrittenBy(handle, date, log.Length == 0 ? null : log)
                        with { ModifiedAt = DateTimeOffset.Now } : mappingPreviousCredits;
                EditorOperation operation = blocks
                    ? new SetBlockStyles(styles.Select(row => new BlockStyleDefinition(row.Key, row.PrimaryColor, row.SecondaryColor, row.Pattern)
                        { KnowledgeComment = row.KnowledgeComment }).ToArray())
                    {
                        Table = new() { Name = mappingGenreCodeTableName, OverallComment = draft.OverallComment,
                            RowOrder = mappingGenreCodeOrder, OrderComment = mappingGenreCodeOrderComment },
                        UpdateCredits = true, Credits = credits, ChangeLog = log, ActorHandle = handle, WorkDate = date,
                    }
                    : new SetGenreStyles(styles.Select(row => new GenreStyleDefinition(row.Key, row.PrimaryColor, row.SecondaryColor, row.Pattern)
                        { KnowledgeComment = row.KnowledgeComment }).ToArray())
                    {
                        GenreCodeTableName = mappingGenreCodeTableName, UpdateOverallComment = true, OverallComment = draft.OverallComment,
                        GenreCodeOrder = mappingGenreCodeOrder, GenreCodeOrderComment = mappingGenreCodeOrderComment, UpdateGenreCodeOrder = true,
                        UpdateCredits = true, Credits = credits, ChangeLog = log, ActorHandle = handle, WorkDate = date,
                    };
                owner.Execute(operation, selectedPlanEdit: false);
            }, blocks ? project.BlockStyleCredits : project.GenreStyleCredits, knowledgeComments: true);
    }
}
