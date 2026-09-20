namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private string? selectedPackageGenreKey;

    private void SelectPackageGenreTarget(string key)
    {
        genreRowActionsRight = true;
        selectedPackageGenreKey = key;
        selectedGenreKey = null;
        var index = Array.FindIndex(PackageGenreRows(), row => row.GenreId == key);
        if (index >= 0) packageGenreScroll = EnsureGenreRowVisible(packageGenreScroll, index, PackageGenreRows().Length);
        mappingFocus = -1;
        mappingWidth = -1;
    }

    private void CopyGenreToOtherPane()
    {
        if (mappingDraft is not { } draft || packageGenreTable is not { } package) return;
        var toLeft = selectedPackageGenreKey is not null;
        var source = toLeft
            ? package.GenreStyles?.Where(row => row.GenreId == selectedPackageGenreKey)
                .Select(row => new StyleMappingEntry(row.GenreId, row.PrimaryColor, row.SecondaryColor, row.Pattern) { KnowledgeComment = row.KnowledgeComment }).SingleOrDefault()
            : draft.Rows.SingleOrDefault(row => row.Key == selectedGenreKey);
        if (source is null) return;
        SetMappingTextFocus(false);
        bool Exists(string key) => toLeft ? draft.Rows.Any(row => row.Key == key) : packageGenreTable?.GenreStyles?.Any(row => row.GenreId == key) == true;
        void Copy(string key, bool overwrite)
        {
            if (toLeft)
            {
                draft.CopyRow(source, key, overwrite);
                if (!overwrite && mappingGenreCodeOrder.Length > 0 && !mappingGenreCodeOrder.Contains(key, StringComparer.Ordinal))
                {
                    mappingGenreCodeOrder = [.. mappingGenreCodeOrder, key];
                    genreCodeOrder = mappingGenreCodeOrder.ToArray();
                    mappingOrderChanged = true;
                }
                draft.ReorderRows(BuildGenrePreviewGroups().Select(group => group.GenreId));
                SelectGenreTarget(key);
            }
            else
            {
                var rows = packageGenreTable!.GenreStyles ?? [];
                var target = new StyleMappingDraft(rows.Select(row => row.GenreId), rows.Select(row =>
                    new StyleMappingEntry(row.GenreId, row.PrimaryColor, row.SecondaryColor, row.Pattern) { KnowledgeComment = row.KnowledgeComment }));
                target.CopyRow(source, key, overwrite);
                var copy = target.Rows.Single(row => row.Key == key);
                var definition = new GenreStyleDefinition(copy.Key, copy.PrimaryColor, copy.SecondaryColor, copy.Pattern) { KnowledgeComment = copy.KnowledgeComment };
                var order = packageGenreTable.GenreCodeOrder;
                if (!overwrite && order is { Count: > 0 } && !order.Contains(key, StringComparer.Ordinal)) order = order.Append(key).ToArray();
                packageGenreTable = packageGenreTable with
                {
                    GenreStyles = overwrite ? rows.Select(row => row.GenreId == key ? definition : row).ToArray() : [.. rows, definition],
                    GenreCodeOrder = order,
                };
                SelectPackageGenreTarget(key);
            }
            mappingWidth = -1;
            mappingFocus = -1;
        }
        if (!Exists(source.Key)) { Copy(source.Key, false); return; }
        OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "ジャンルコードの重複",
            $"反対側に「{source.Key}」があります。コピー方法を選んでください。"), action =>
        {
            if (action == ModalDialogAction.Accept) Copy(source.Key, true);
            else if (action == ModalDialogAction.Increase)
                OpenUnderlineInput("名前を変えてコピーする", source.Key, value => Copy(PersonCredits.NormalizeChangeLog(value), false),
                    "反対側にまだないジャンルコードを入力してください。コピー元の名前は変わりません。", 1000,
                    validate: value =>
                    {
                        try { return Exists(PersonCredits.NormalizeChangeLog(value)) ? "同じジャンルコードが既にあります。別の名前を入力してください。" : null; }
                        catch (ArgumentException ex) { return ex.Message; }
                    });
        }, [("上書きする", ModalDialogAction.Accept), ("名前を変えてコピーする", ModalDialogAction.Increase), ("キャンセル", ModalDialogAction.Cancel)]);
    }
}
