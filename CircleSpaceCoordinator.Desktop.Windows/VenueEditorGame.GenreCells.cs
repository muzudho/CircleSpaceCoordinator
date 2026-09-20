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

    private static bool GenreCellEditable(int column, string pattern, bool right) =>
        column is 1 or 2 or 4 or 6 || column == 3 && pattern != "solid" || column == 0 && !right;

    private void DrawGenreCellHover(int row, string pattern, bool right)
    {
        if (!GenreGridVisible || mappingPickerColumn != 0 || !CanShowEditorHover) return;
        var mouse = Mouse.GetState();
        for (var column = 0; column < 7; column++)
        {
            var bounds = MappingCell(row, column, right);
            if (GenreCellEditable(column, pattern, right) && Contains(bounds, new(mouse.X, mouse.Y)))
                DrawOutline(bounds, 2 * MappingEditorScale, OperationTargetColor);
        }
    }

    private bool TryClickGenreGridRow(ScreenPoint pointer)
    {
        for (var pane = 0; pane < (GenreGridSplit ? 2 : 1); pane++)
        {
            var right = pane == 1;
            var packageRows = right ? PackageGenreRows() : [];
            var count = right ? packageRows.Length : mappingDraft!.Rows.Count;
            var scroll = right ? packageGenreScroll : mappingScroll;
            if (count == 0 && (!right || packageGenreTable is not null) &&
                Contains(MappingGridBounds(20, 142, 960, 306, right), pointer))
            {
                SetMappingTextFocus(false);
                genreRowActionsRight = right;
                selectedGenreKey = selectedPackageGenreKey = null;
                mappingWidth = -1;
                mappingFocus = -1;
                return true;
            }
            for (var row = 0; row < MappingVisibleRows && scroll + row < count; row++)
            {
                if (!Contains(MappingGridBounds(20, 142 + row * 52, 960, 46, right), pointer)) continue;
                var index = scroll + row;
                var key = right ? packageRows[index].GenreId : mappingDraft!.Rows[index].Key;
                var selected = key == (right ? selectedPackageGenreKey : selectedGenreKey);
                SetMappingTextFocus(false);
                if (right) SelectPackageGenreTarget(key); else SelectGenreTarget(key);
                if (!selected) return true;
                var pattern = right ? packageRows[index].Pattern : mappingDraft!.Rows[index].Pattern;
                for (var column = 0; column < 7; column++)
                    if (GenreCellEditable(column, pattern, right) && Contains(MappingCell(row, column, right), pointer))
                    {
                        if (right) OpenPackageGenreCell(index, column); else OpenMappingPicker(index, column);
                        return true;
                    }
                return true;
            }
        }
        return false;
    }

    private StyleMappingDraft CreatePackageCellDraft()
    {
        var rows = packageGenreTable!.GenreStyles ?? [];
        var draft = new StyleMappingDraft(rows.Select(row => row.GenreId),
            rows.Select(row => new StyleMappingEntry(row.GenreId, row.PrimaryColor, row.SecondaryColor, row.Pattern)
            { KnowledgeComment = row.KnowledgeComment }));
        draft.ReorderRows(rows.Select(row => row.GenreId));
        return draft;
    }

    private void ApplyPackageCellDraft(StyleMappingDraft draft)
    {
        if (packageGenreTable is null) return;
        packageGenreTable = packageGenreTable with
        {
            GenreStyles = draft.Build().Select(row => new GenreStyleDefinition(row.Key, row.PrimaryColor, row.SecondaryColor, row.Pattern)
            { KnowledgeComment = row.KnowledgeComment }).ToArray(),
        };
        mappingWidth = -1;
    }

    private void OpenGenreNameEditor(int row, bool right)
    {
        var key = right ? PackageGenreRows()[row].GenreId : mappingDraft!.Rows[row].Key;
        var draft = right ? CreatePackageCellDraft() : mappingDraft!;
        var index = draft.Rows.ToList().FindIndex(item => item.Key == key);
        string? Validate(string value)
        {
            try
            {
                var name = PersonCredits.NormalizeChangeLog(value);
                return draft.Build().Any(item => item.Key != key && item.Key == name) ? "同じジャンルコードが既にあります。" : null;
            }
            catch (ArgumentException ex) { return ex.Message; }
        }
        SetMappingTextFocus(false);
        OpenUnderlineInput("ジャンル名", key, value =>
        {
            var name = PersonCredits.NormalizeChangeLog(value);
            if (name == key) return;
            draft.RenameRow(index, name);
            if (right)
            {
                ApplyPackageCellDraft(draft);
                packageGenreTable = packageGenreTable! with { GenreCodeOrder = packageGenreTable.GenreCodeOrder?.Select(item => item == key ? name : item).ToArray() };
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
        }, "このジャンルコード表の名前を変更します。参加サークルのジャンル値は変更しません。", 1000, validate: Validate);
    }

    private void OpenPackageGenreCell(int row, int column)
    {
        if (column == 1) { OpenGenreNameEditor(row, true); return; }
        var key = PackageGenreRows()[row].GenreId;
        var draft = CreatePackageCellDraft();
        var index = draft.Rows.ToList().FindIndex(item => item.Key == key);
        if (column == 6)
        {
            OpenUnderlineInput($"{key} の知見コメント", draft.Rows[index].KnowledgeComment ?? "", value =>
            {
                draft.SetKnowledgeComment(index, value);
                ApplyPackageCellDraft(draft);
            }, "1000文字以内で入力してください。空欄で確定すると削除します。", maxLength: int.MaxValue, allowEmpty: true, validate: ValidateGenreComment);
            return;
        }
        packageCellDraft = draft;
        packageCellRow = index;
        mappingPickerColumn = column - 1;
        var style = draft.Rows[index];
        var current = column == 4 ? style.Pattern : column == 2 ? style.PrimaryColor : style.SecondaryColor;
        var choices = column == 4 ? StyleMappingDraft.Patterns : StyleMappingDraft.Colors;
        mappingFocus = choices.Select((choice, i) => (choice, i)).Where(item => item.choice.Id == current)
            .Select(item => item.i).DefaultIfEmpty(column == 4 ? 0 : choices.Count).First();
        mappingWidth = -1;
        pressedMappingButton = null;
        modalInputDrain = true;
    }
}
