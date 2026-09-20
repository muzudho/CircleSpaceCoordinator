namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private void CopyMultipleShadingRows()
    {
        if (mappingDraft is not { } project || packageGenreTable is null) return;
        var toRight = !shadingSelection.Right;
        var sources = (shadingSelection.Right ? PackageGenreRows() : project.Rows.ToArray())
            .Where(row => shadingSelection.Keys.Contains(row.Key)).ToArray();
        if (sources.Length == 0) return;
        SetMappingTextFocus(false);
        var target = toRight ? CreatePackageCellDraft() : project;
        if (toRight) target.ReorderRows(PackageGenreRows().Select(row => row.Key));
        var staged = new StyleMappingDraft(target.Rows.Select(row => row.Key), target.Build(), target.OverallComment);
        staged.ReorderRows(target.Rows.Select(row => row.Key));
        var copies = new List<(StyleMappingEntry Row, string Key, bool Overwrite)>();
        var index = 0;
        bool Exists(string key) => staged.Build().Any(row => row.Key == key);
        void Stage(string key, bool overwrite)
        {
            staged.CopyRow(sources[index], key, overwrite);
            copies.Add((sources[index], key, overwrite));
            index++;
        }
        void Commit()
        {
            target.CopyRows(copies);
            var order = target.Rows.Select(row => row.Key).ToArray();
            if (toRight)
            {
                ApplyPackageCellDraft(target);
                packageGenreTable = packageGenreTable!.WithOrder(order);
            }
            else
            {
                mappingGenreCodeOrder = order;
                genreCodeOrder = order.ToArray();
                mappingOrderChanged = true;
            }
            shadingSelection.Set(toRight, copies.Select(copy => copy.Key));
            if (toRight) SelectPackageGenreTarget(copies[0].Key, preserveSelection: true);
            else SelectGenreTarget(copies[0].Key, preserveSelection: true);
            mappingWidth = -1;
        }
        void Continue()
        {
            while (index < sources.Length)
            {
                var source = sources[index];
                if (!Exists(source.Key)) { Stage(source.Key, false); continue; }
                OpenModal(new ModalDialogModel(ModalDialogKind.Confirmation, "名前の重複",
                    $"{sources.Length} 行をコピーします。反対側に「{source.Key}」があります。\nコピー方法を選んでください。キャンセルすると今回の一括コピー全体を取り消します。"), action =>
                {
                    if (action == ModalDialogAction.Accept) { Stage(source.Key, true); Continue(); }
                    else if (action == ModalDialogAction.Increase)
                        OpenUnderlineInput("名前を変えてコピーする", source.Key, value =>
                        {
                            Stage(PersonCredits.NormalizeChangeLog(value), false);
                            Continue();
                        }, "反対側にまだない名前を入力してください。キャンセルすると今回の一括コピー全体を取り消します。", 1000,
                            validate: value =>
                            {
                                try { return Exists(PersonCredits.NormalizeChangeLog(value)) ? "同じ名前が既にあります。" : null; }
                                catch (ArgumentException ex) { return ex.Message; }
                            });
                }, [("上書きする", ModalDialogAction.Accept), ("名前を変えてコピーする", ModalDialogAction.Increase), ("キャンセル", ModalDialogAction.Cancel)]);
                return;
            }
            Commit();
        }
        Continue();
    }
}
