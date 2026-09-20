namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;

public sealed partial class VenueEditorGame
{
    private StyleMappingDraft? packageCellDraft;
    private int packageCellRow;
    private ScreenRectangle GenreTargetRowBounds(int row, bool right = false)
    {
        var bounds = MappingGridBounds(20, 142 + row * 52, 960, 46, right);
        return new(bounds.X - 5, bounds.Y - 5, bounds.Width + 10, bounds.Height + 10);
    }

    private static bool GenreCellEditable(int column, string pattern) =>
        column is 0 or 1 or 2 or 4 or 6 || column == 3 && pattern != "solid";

    private void DrawGenreCellHover(int row, string pattern, bool right)
    {
        if (!GenreGridVisible || MultipleShadingRows || mappingPickerColumn != 0 || !CanShowEditorHover) return;
        var mouse = Mouse.GetState();
        for (var column = 0; column < 7; column++)
        {
            var bounds = MappingCell(row, column, right);
            if (GenreCellEditable(column, pattern) && Contains(bounds, new(mouse.X, mouse.Y)))
                DrawOutline(bounds, 2 * MappingEditorScale, OperationTargetColor);
        }
    }

    private bool TryClickGenreGridRow(ScreenPoint pointer, KeyboardState keyboard)
    {
        for (var pane = 0; pane < (GenreGridSplit ? 2 : 1); pane++)
        {
            var right = pane == 1;
            if (shadingSelection.IsLocked(right)) continue;
            var packageRows = right ? PackageGenreRows() : [];
            var count = right ? packageRows.Length : mappingDraft!.Rows.Count;
            var scroll = right ? packageGenreScroll : mappingScroll;
            if (count == 0 && (!right || packageGenreTable is not null) &&
                Contains(MappingGridBounds(20, 142, 960, 306, right), pointer))
            {
                SetMappingTextFocus(false);
                genreRowActionsRight = right;
                selectedGenreKey = selectedPackageGenreKey = null;
                shadingSelection.Clear();
                mappingWidth = -1;
                mappingFocus = -1;
                return true;
            }
            for (var row = 0; row < MappingVisibleRows && scroll + row < count; row++)
            {
                if (!Contains(MappingGridBounds(20, 142 + row * 52, 960, 46, right), pointer)) continue;
                var index = scroll + row;
                var key = right ? packageRows[index].Key : mappingDraft!.Rows[index].Key;
                var selected = key == (right ? selectedPackageGenreKey : selectedGenreKey);
                var wasMultiple = MultipleShadingRows;
                var modified = IsControlDown(keyboard) || keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
                SetMappingTextFocus(false);
                ClickShadingRow(right, key, keyboard);
                if (!selected || wasMultiple || modified || MultipleShadingRows) return true;
                var pattern = right ? packageRows[index].Pattern : mappingDraft!.Rows[index].Pattern;
                for (var column = 0; column < 7; column++)
                    if (GenreCellEditable(column, pattern) && Contains(MappingCell(row, column, right), pointer))
                    {
                        OpenMappingPicker(index, column, right);
                        return true;
                    }
                return true;
            }
        }
        return false;
    }

    private StyleMappingDraft CreatePackageCellDraft()
    {
        var rows = packageGenreTable!.Rows() ?? [];
        var draft = new StyleMappingDraft(rows.Select(row => row.Key),
            rows.Select(row => new StyleMappingEntry(row.Key, row.PrimaryColor, row.SecondaryColor, row.Pattern)
            { KnowledgeComment = row.KnowledgeComment }));
        draft.ReorderRows(rows.Select(row => row.Key));
        return draft;
    }

    private void ApplyPackageCellDraft(StyleMappingDraft draft)
    {
        if (packageGenreTable is null) return;
        packageGenreTable = packageGenreTable.WithRows(draft.Build());
        mappingWidth = -1;
    }

    private void OpenGenreNameEditor(int row, bool right)
    {
        var key = right ? PackageGenreRows()[row].Key : mappingDraft!.Rows[row].Key;
        var draft = right ? CreatePackageCellDraft() : mappingDraft!;
        var visibleOrder = right ? PackageGenreRows().Select(item => item.Key).ToArray() : mappingGenreCodeOrder;
        var index = draft.Rows.ToList().FindIndex(item => item.Key == key);
        string? Validate(string value)
        {
            try
            {
                var name = PersonCredits.NormalizeChangeLog(value);
                return draft.Build().Any(item => item.Key != key && item.Key == name) ? "同じ名前が既にあります。" : null;
            }
            catch (ArgumentException ex) { return ex.Message; }
        }
        SetMappingTextFocus(false);
        OpenUnderlineInput(MappingRowLabel + "名", key, value =>
        {
            var name = PersonCredits.NormalizeChangeLog(value);
            if (name == key) return;
            draft.RenameRow(index, name);
            if (right)
            {
                ApplyPackageCellDraft(draft);
                packageGenreTable = packageGenreTable!.WithOrder(visibleOrder.Select(item => item == key ? name : item).ToArray());
                SelectPackageGenreTarget(name);
            }
            else
            {
                mappingGenreCodeOrder = mappingGenreCodeOrder.Select(item => item == key ? name : item).ToArray();
                genreCodeOrder = mappingGenreCodeOrder.ToArray();
                mappingOrderChanged = true;
                draft.ReorderRows(BuildGenrePreviewGroups().Select(group => group.GenreId));
                SelectGenreTarget(name);
            }
            mappingWidth = -1;
        }, "この表の行名を変更します。サークルや配置の値は変更しません。", 1000, validate: Validate);
    }

}
