namespace CircleSpaceCoordinator.Desktop.Windows;

using StationeryUI.Canvas;
using CircleSpaceCoordinator.Desktop.Core.Interaction;

public sealed partial class VenueEditorGame
{
    private bool genreRowActionsRight;
    private bool GenreRowActionsRight => GenreGridSplit && genreRowActionsRight;

    private ScreenRectangle GenreRowActionBounds(double offset, double width)
    {
        var origin = MappingGridBounds(20, 460, 960, 32);
        return new(origin.X + offset * MappingEditorScale, origin.Y, width * MappingEditorScale, origin.Height);
    }

    private void CreateGenreRow() => EditGenreRows(create: true);
    private void DeleteGenreRow() => EditGenreRows(create: false);

    private void EditGenreRows(bool create)
    {
        if (mappingDraft is null) return;
        var right = GenreRowActionsRight;
        if (right && packageGenreTable is null) return;
        SetMappingTextFocus(false);
        var draft = right ? CreatePackageCellDraft() : mappingDraft;
        // Material storage order can differ from the order currently visible in the right pane.
        if (right) draft.ReorderRows(PackageGenreRows().Select(row => row.Key));
        var selected = right ? selectedPackageGenreKey : selectedGenreKey;
        var index = draft.Rows.ToList().FindIndex(row => row.Key == selected);
        string? next;
        if (create)
        {
            // With no target, start at the first visible row (or the first row of an empty table).
            if (index < 0) index = Math.Clamp(right ? packageGenreScroll : mappingScroll, 0, draft.Rows.Count);
            next = draft.InsertNewGenreRow(index, mappingBlocks ? "新しいブロック" : "新しいジャンルコード");
        }
        else
        {
            if (index < 0) return;
            draft.DeleteRow(index);
            next = draft.Rows.Count == 0 ? null : draft.Rows[Math.Min(index, draft.Rows.Count - 1)].Key;
        }
        // Persist the visible insertion position instead of immediately sorting the new name away.
        genreOrdinalSort = genreSpaceSort = false;
        genreCodeSort = true;
        var order = draft.Rows.Select(row => row.Key).ToArray();
        if (right)
        {
            ApplyPackageCellDraft(draft);
            packageGenreTable = packageGenreTable!.WithOrder(order);
            selectedPackageGenreKey = next;
            selectedGenreKey = null;
            if (next is not null) SelectPackageGenreTarget(next);
            else packageGenreScroll = 0;
        }
        else
        {
            mappingGenreCodeOrder = order;
            genreCodeOrder = order.ToArray();
            mappingOrderChanged = true;
            selectedGenreKey = next;
            selectedPackageGenreKey = null;
            if (next is not null) SelectGenreTarget(next);
            else mappingScroll = mappingRow = 0;
        }
        mappingWidth = -1;
        mappingFocus = -1;
    }
}
