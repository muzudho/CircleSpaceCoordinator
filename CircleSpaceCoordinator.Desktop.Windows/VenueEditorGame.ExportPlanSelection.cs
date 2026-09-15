namespace CircleSpaceCoordinator.Desktop.Windows;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Engine.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private IReadOnlyList<ExportPlanChoice>? exportPlanChoices;
    private CircleSpaceProject? exportPreviewProject;
    private IReadOnlyList<ExportPlanChoice> allExportPlanChoices = [];
    private object? exportPinsOwner;
    private HashSet<string> exportPlanPins = new(StringComparer.Ordinal);
    private bool exportPinnedOnly;

    private HashSet<string> GetExportPlanPins()
    {
        if (ReferenceEquals(exportPinsOwner, workspace)) return exportPlanPins;
        exportPinsOwner = workspace;
        exportPlanPins = new HashSet<string>(projectSavePath is not null
            ? settings?.GetWorkingState(projectSavePath)?.PinnedExportPlanIds ?? [] : [], StringComparer.Ordinal);
        return exportPlanPins;
    }

    private ScreenRectangle ExportPinFilterBounds()
    {
        var area = SelectionArea();
        return new ScreenRectangle(area.X, area.Y - 34, Math.Min(220, area.Width), 28);
    }

    private void FilterExportPlanChoices()
    {
        var selectedId = exportPlanChoices is { } old && selectionIndex < old.Count ? old[selectionIndex].Plan.Id : null;
        exportPlanChoices = ExportPlanChoices.Filter(allExportPlanChoices, GetExportPlanPins(), exportPinnedOnly);
        selectionLabels = exportPlanChoices.Select(item => item.Plan.Name).Append("配置案を未決定に戻す").ToArray();
        selectionIndex = Math.Max(0, exportPlanChoices.ToList().FindIndex(item => item.Plan.Id == selectedId));
        selectionScroll = Math.Max(0, selectionIndex - SelectionPageSize + 1);
        selectionPressed = -1;
    }

    private bool UpdateExportPlanPins(KeyboardState keyboard, MouseState mouse)
    {
        if (exportPlanChoices is not { } choices) return false;
        var click = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        if ((click && Contains(ExportPinFilterBounds(), pointer)) || IsPressed(keyboard, Keys.F))
        {
            exportPinnedOnly = !exportPinnedOnly;
            FilterExportPlanChoices();
            return true;
        }
        var area = SelectionArea();
        var pinRow = click && Contains(area, pointer) && pointer.X >= area.X + area.Width - 66
            && pointer.Y < area.Y + SelectionPageSize * SelectionRowHeight
            ? selectionScroll + (int)((pointer.Y - area.Y) / SelectionRowHeight) : -1;
        if (IsPressed(keyboard, Keys.P)) pinRow = selectionIndex;
        if (pinRow < 0 || pinRow >= choices.Count) return false;
        var id = choices[pinRow].Plan.Id;
        var pins = GetExportPlanPins();
        if (!pins.Add(id)) pins.Remove(id);
        PersistWorkingState();
        FilterExportPlanChoices();
        return true;
    }

    private void OpenExportPlanSelection()
    {
        if (workspace is null) return;
        var owner = workspace;
        var project = owner.Project;
        var choices = ExportPlanChoices.Build(project, owner.RankPlans());
        var initial = choices.ToList().FindIndex(item => item.Plan.Id == project.ExportPlanId);
        if (initial < 0) initial = choices.Count > 0 ? 0 : choices.Count;
        OpenSelection("書き出すサークル配置案を選択", choices.Select(item => item.Plan.Name).Append("配置案を未決定に戻す").ToArray(),
            initial, index =>
            {
                if (workspace != owner) return;
                var visible = exportPlanChoices ?? choices;
                owner.Execute(new SetExportPlan(index < visible.Count ? visible[index].Plan.Id : null), selectedPlanEdit: false);
            });
        exportPlanChoices = choices;
        allExportPlanChoices = choices;
        exportPinnedOnly = false;
        GetExportPlanPins();
        exportPreviewProject = project;
        modalChoices = [("キャンセル", ModalDialogAction.Cancel), ("この案に確定", ModalDialogAction.Accept)];
    }

    private void DrawExportPlanSelection()
    {
        if (exportPlanChoices is not { } choices || exportPreviewProject is not { } project) return;
        var bounds = ModalBounds();
        var area = SelectionArea();
        var left = new ScreenRectangle(bounds.X + 20, bounds.Y + 92, (bounds.Width - 60) / 2, bounds.Height - 210);
        textRenderer?.Draw("配置図プレビュー", ToRectangle(new ScreenRectangle(left.X, bounds.Y + 57, left.Width, 28)), Color.White, 18, true);
        textRenderer?.Draw("一般参加者評価値 ↓ ／ 同点はサークル評価値 ↓",
            ToRectangle(new ScreenRectangle(area.X, bounds.Y + 58, area.Width, 26)), Color.LightGray, 14);
        var filter = ExportPinFilterBounds();
        DrawRectangle(filter, exportPinnedOnly ? new Color(45, 95, 100) : new Color(48, 56, 68));
        textRenderer?.Draw($"{(exportPinnedOnly ? "☑" : "□")} ピンのみ（F）", ToRectangle(filter, 4), Color.White, 15);
        for (var row = 0; row < SelectionPageSize && selectionScroll + row < selectionLabels!.Length; row++)
        {
            var index = selectionScroll + row;
            var rect = new ScreenRectangle(area.X, area.Y + row * SelectionRowHeight, area.Width, SelectionRowHeight - 4);
            DrawRectangle(rect, index == selectionIndex ? new Color(45, 95, 100) : new Color(32, 40, 49));
            if (index < choices.Count)
            {
                var choice = choices[index];
                var mark = choice.Plan.Id == project.ExportPlanId ? "［確定中］" : "";
                textRenderer?.Draw($"{index + 1}. {choice.Plan.Name} {mark}",
                    ToRectangle(new ScreenRectangle(rect.X + 6, rect.Y + 4, rect.Width - 78, 27)), Color.White, 17, true);
                var pin = new ScreenRectangle(rect.X + rect.Width - 64, rect.Y + 4, 60, 27);
                DrawRectangle(pin, GetExportPlanPins().Contains(choice.Plan.Id) ? new Color(142, 110, 35) : new Color(58, 66, 77));
                textRenderer?.Draw(GetExportPlanPins().Contains(choice.Plan.Id) ? "ピン済" : "ピン", ToRectangle(pin, 3), Color.White, 14);
                textRenderer?.Draw($"一般参加者評価値：{choice.Evaluation.GeneralAttendeeScore:0.##}　サークル評価値：{choice.Evaluation.CircleParticipantScore:0.##}",
                    ToRectangle(new ScreenRectangle(rect.X + 6, rect.Y + 33, rect.Width - 12, 24)), Color.LightGray, 14);
            }
            else textRenderer?.Draw("配置案を未決定に戻す" + (project.ExportPlanId is null ? "［現在未決定］" : ""), ToRectangle(rect, 6), Color.White, 17);
        }
        DrawRectangle(left, new Color(15, 21, 28));
        if (selectionIndex < choices.Count)
            DrawExportPlanPreview(project, choices[selectionIndex].Plan, left);
        else textRenderer?.Draw(choices.Count == 0 && exportPinnedOnly ? "ピン付きの候補がありません。\n［ピンのみ］を解除して候補を選べます。" : "確定を解除します。", ToRectangle(left, 12), Color.LightGray, 18);
        textRenderer?.Draw($"{selectionIndex + 1} / {selectionLabels!.Length}　↑↓・ホイール：選択　P：ピン　F：絞込",
            ToRectangle(new ScreenRectangle(area.X, bounds.Y + bounds.Height - 82, area.Width, 22)), Color.LightGray, 12);
    }

    private void DrawExportPlanPreview(CircleSpaceProject project, Plan plan, ScreenRectangle area)
    {
        var types = project.DeskTypes.ToDictionary(item => item.Id);
        var cells = plan.DeskPlacements.SelectMany(desk => desk.GetOccupiedCells(types[desk.DeskTypeId])).ToArray();
        var allCells = cells.Concat(plan.TemporaryPlacements.SelectMany(item => item.OccupiedCells)).ToArray();
        var minX = Math.Min(0, allCells.Select(cell => cell.X).DefaultIfEmpty(0).Min());
        var minY = Math.Min(0, allCells.Select(cell => cell.Y).DefaultIfEmpty(0).Min());
        var maxX = Math.Max(project.Venue.Width, allCells.Select(cell => cell.X + 1).DefaultIfEmpty(1).Max());
        var maxY = Math.Max(project.Venue.Height, allCells.Select(cell => cell.Y + 1).DefaultIfEmpty(1).Max());
        var scale = Math.Max(0.01, Math.Min((area.Width - 24) / Math.Max(1, maxX - minX), (area.Height - 54) / Math.Max(1, maxY - minY)));
        var x = area.X + (area.Width - (maxX - minX) * scale) / 2;
        var y = area.Y + 12 + (area.Height - 54 - (maxY - minY) * scale) / 2;
        ScreenRectangle Cell(GridPosition cell) => new(x + (cell.X - minX) * scale, y + (cell.Y - minY) * scale, scale, scale);
        DrawOutline(new ScreenRectangle(x - minX * scale, y - minY * scale, project.Venue.Width * scale, project.Venue.Height * scale), 1, Color.LightGray);
        foreach (var cell in project.Venue.BlockedCells) DrawRectangle(Cell(cell), new Color(90, 96, 105));
        foreach (var cell in cells)
        {
            DrawRectangle(Cell(cell), new Color(128, 101, 65));
            DrawOutline(Cell(cell), Math.Min(1, scale / 10), new Color(208, 183, 142));
        }
        var participants = project.Participants.ToDictionary(item => item.Id);
        foreach (var assignment in plan.Assignments.Concat(plan.TemporaryPlacements))
        {
            var temporary = plan.TemporaryPlacements.Contains(assignment);
            participants.TryGetValue(assignment.ParticipantId, out var participant);
            foreach (var cell in assignment.OccupiedCells)
            {
                var rect = Cell(cell);
                DrawCircle(new ScreenPoint(rect.X + scale / 2, rect.Y + scale / 2), scale * .36,
                    temporary ? Color.Orange : GetGenreStyle(participant?.GenreId).Primary);
            }
            if (scale >= 24 && assignment.OccupiedCells.Count > 0 && participant is not null)
                textRenderer?.Draw(participant.DisplayName, ToRectangle(Cell(assignment.OccupiedCells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).First())), Color.White, 10);
        }
        textRenderer?.Draw($"配置済み {plan.Assignments.Count} ／ 仮置き {plan.TemporaryPlacements.Count} ／ 未配置 {project.Participants.Count - plan.Assignments.Count - plan.TemporaryPlacements.Count}",
            ToRectangle(new ScreenRectangle(area.X + 8, area.Y + area.Height - 30, area.Width - 16, 24)), Color.LightGray, 13);
    }
}
