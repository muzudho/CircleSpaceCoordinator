namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Engine.Model;

public sealed partial class VenueEditorGame
{
    private void OpenGenreStyleEditor()
    {
        if (workspace is not { } targetWorkspace) return;
        var draft = new GenreStyleDraft(targetWorkspace.Project);
        mappingGenreCodeTableName = targetWorkspace.Project.GetGenreCodeTableName();
        mappingGenreCodeOrder = targetWorkspace.Project.GenreCodeOrder.ToArray();
        genreCodeOrder = mappingGenreCodeOrder.ToArray();
        mappingGenreCodeOrderComment = targetWorkspace.Project.GenreCodeOrderComment;
        genreCodeSort = mappingGenreCodeOrder.Length > 0;
        genreSpaceSort = false;
        genreOrdinalSort = false;
        if (genreCodeSort) draft.Mapping.ReorderRows(mappingGenreCodeOrder);
        OpenStyleMappingEditor(draft.Mapping, "ジャンルコード", "ジャンルが設定されたサークルはありません。",
            (styles, log, handle, date) => targetWorkspace.Execute(new SetGenreStyles(styles.Select(style =>
                new GenreStyleDefinition(style.Key, style.PrimaryColor, style.SecondaryColor, style.Pattern)
                    { KnowledgeComment = style.KnowledgeComment }).ToArray())
                { ChangeLog = log, ActorHandle = handle, WorkDate = date,
                    UpdateCredits = true,
                    Credits = GenreProjectChanged ? (mappingPreviousCredits ?? new PersonCredits()).WrittenBy(handle, date, log) : mappingPreviousCredits,
                    GenreCodeTableName = mappingGenreCodeTableName,
                    UpdateOverallComment = true, OverallComment = draft.Mapping.OverallComment,
                    GenreCodeOrder = mappingGenreCodeOrder,
                    GenreCodeOrderComment = mappingGenreCodeOrderComment,
                    UpdateGenreCodeOrder = true },
                selectedPlanEdit: false), targetWorkspace.Project.GenreStyleCredits, knowledgeComments: true);
    }
}
