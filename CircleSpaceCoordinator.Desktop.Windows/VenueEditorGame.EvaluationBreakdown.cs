namespace CircleSpaceCoordinator.Desktop.Windows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StationeryUI.Canvas;
using StationeryUI.Controls;

public sealed partial class VenueEditorGame
{
    private sealed record EvaluationRow(string Name, string Points = "", string Weight = "", string Result = "", double? Contribution = null, bool IsGeneral = false);
    private List<EvaluationRow>? evaluationRows;
    private int evaluationTop;
    private double evaluationGeneralTotal;
    private double evaluationCircleTotal;
    private bool evaluationValid;
    private bool evaluationScrollbarDragging;
    private double evaluationScrollbarGrab;

    private void OpenEvaluationBreakdown()
    {
        if (workspace?.HasSelectedCircleLayout != true) return;
        var snapshot = workspace.GetSelectedPlanSnapshot();
        var audience = snapshot.AudienceEvaluation;
        OpenModal(new ModalDialogModel(ModalDialogKind.Message, $"評価の内訳：{snapshot.PlanName}", ""));
        evaluationTop = 0;
        evaluationScrollbarDragging = false;
        evaluationValid = audience.CombinedSpaceRequirementsSatisfied;
        evaluationGeneralTotal = audience.GeneralAttendeeScore;
        evaluationCircleTotal = audience.CircleParticipantScore;
        static string Number(double value) => value.ToString("G8");
        evaluationRows = [];
        if (!evaluationValid)
            evaluationRows.Add(new("合体条件未達：以下は適用前の得点です。実評価は両方とも0点です。"));
        var genreScore = audience.Genres.Sum(item => item.Score);
        evaluationRows.Add(new("ジャンル", Number(genreScore), "1", Number(genreScore), genreScore, IsGeneral: true));
        evaluationRows.Add(new("一般評価値", Result: Number(audience.GeneralAttendeeScore)));
        evaluationRows.Add(new(""));
        foreach (var result in snapshot.Evaluation.Features)
        {
            var feature = workspace.Project.Evaluation.Features.Single(item => item.Id == result.FeatureId);
            evaluationRows.Add(new(feature.Name, Number(result.AdjustedScore), Number(feature.OverallWeight),
                Number(result.WeightedScore), result.WeightedScore));
            if (feature.Scale != 1 || feature.Offset != 0)
                evaluationRows.Add(new($"  補正：{Number(result.RawSum)} × {Number(feature.Scale)} ＋ {Number(feature.Offset)} ＝ {Number(result.AdjustedScore)}"));
        }
        if (snapshot.Evaluation.Features.Count == 0) evaluationRows.Add(new("（評価チャンネルなし）"));
        evaluationRows.Add(new("サークル評価値", Result: Number(audience.CircleParticipantScore)));
        evaluationRows.Add(new(""));
        evaluationRows.Add(new("配置案評価値（一般＋サークル）", Result: Number(evaluationGeneralTotal + evaluationCircleTotal)));
        evaluationRows.Add(new(""));
        evaluationRows.Add(new("寄与率：一般の項目は一般評価値、サークルの項目はサークル評価値で割ります。"));
        evaluationRows.Add(new("各評価値が0・非有限値、または条件未達の場合、その寄与率は「—」。"));
        evaluationRows.Add(new("負数や100％超もあります。棒は一般・サークル別に寄与の絶対値を比較します。"));
        evaluationRows.Add(new("順位・最適化は一般評価を優先し、同点ならサークル評価で比較します。"));
        evaluationRows.Add(new("チャンネル得点は配置済みサークルの列値×評価対象セルの重みの合計です。"));
        evaluationRows.Add(new("仮置き・未配置は集計対象外。表示は丸めており、合計は丸め前で計算します。"));
    }

    private ScreenRectangle EvaluationArea()
    {
        var bounds = ModalBounds();
        return new(bounds.X + 20, bounds.Y + 96, bounds.Width - 64, Math.Max(32, bounds.Height - 150));
    }
    private int EvaluationPageSize => Math.Max(1, (int)(EvaluationArea().Height / 32));
    private (ScreenRectangle Track, ScreenRectangle Thumb, int Maximum) EvaluationScrollbar()
    {
        var area = EvaluationArea();
        var count = evaluationRows!.Count;
        var maximum = Math.Max(0, count - EvaluationPageSize);
        evaluationTop = Math.Clamp(evaluationTop, 0, maximum);
        var track = new ScreenRectangle(area.X + area.Width + 8, area.Y, 14, EvaluationPageSize * 32);
        var height = maximum == 0 ? track.Height : Math.Min(track.Height, Math.Max(24, track.Height * EvaluationPageSize / count));
        var offset = maximum == 0 ? 0 : (track.Height - height) * evaluationTop / maximum;
        return (track, new(track.X, track.Y + offset, track.Width, height), maximum);
    }

    private bool UpdateEvaluationBreakdown(KeyboardState keyboard, MouseState mouse)
    {
        if (evaluationRows is null) return false;
        var wheel = mouse.ScrollWheelValue - previousMouse.ScrollWheelValue;
        var delta = wheel > 0 ? -3 : wheel < 0 ? 3 : 0;
        delta += IsPressed(keyboard, Keys.Down) ? 1 : IsPressed(keyboard, Keys.Up) ? -1 : 0;
        delta += IsPressed(keyboard, Keys.PageDown) ? EvaluationPageSize : IsPressed(keyboard, Keys.PageUp) ? -EvaluationPageSize : 0;
        var maximum = Math.Max(0, evaluationRows.Count - EvaluationPageSize);
        evaluationTop = Math.Clamp(evaluationTop + delta, 0, maximum);
        if (IsPressed(keyboard, Keys.Home)) evaluationTop = 0;
        if (IsPressed(keyboard, Keys.End)) evaluationTop = maximum;
        var bar = EvaluationScrollbar();
        var pointer = new ScreenPoint(mouse.X, mouse.Y);
        if (evaluationScrollbarDragging)
        {
            if (mouse.LeftButton == ButtonState.Released) evaluationScrollbarDragging = false;
            else
            {
                var travel = bar.Track.Height - bar.Thumb.Height;
                evaluationTop = travel <= 0 ? 0 : (int)Math.Round(Math.Clamp((pointer.Y - bar.Track.Y - evaluationScrollbarGrab) / travel, 0, 1) * maximum);
            }
            return true;
        }
        if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released && Contains(bar.Track, pointer))
        {
            if (Contains(bar.Thumb, pointer) && maximum > 0)
            {
                evaluationScrollbarDragging = true;
                evaluationScrollbarGrab = pointer.Y - bar.Thumb.Y;
            }
            else evaluationTop = Math.Clamp(evaluationTop + (pointer.Y < bar.Thumb.Y ? -EvaluationPageSize : EvaluationPageSize), 0, maximum);
            return true;
        }
        return false;
    }

    private void DrawEvaluationBreakdown()
    {
        if (evaluationRows is null) return;
        var area = EvaluationArea();
        void Cell(string value, double fraction, double width, double y, Color color, bool bold = false) =>
            textRenderer?.Draw(value, ToRectangle(new ScreenRectangle(area.X + area.Width * fraction, y, area.Width * width - 6, 28)), color, 16, bold);
        var headerY = area.Y - 34;
        Cell("項目", 0, .38, headerY, Color.LightGray);
        Cell("得点", .38, .14, headerY, Color.LightGray);
        Cell("× 倍率", .52, .12, headerY, Color.LightGray);
        Cell("＝ 評価値", .64, .16, headerY, Color.LightGray);
        Cell("一般・サークル別寄与率", .8, .2, headerY, Color.LightGray);
        var maxima = evaluationRows.Where(row => row.Contribution is { } value && double.IsFinite(value))
            .GroupBy(row => row.IsGeneral)
            .ToDictionary(group => group.Key, group => group.Max(row => Math.Abs(row.Contribution!.Value)));
        var bar = EvaluationScrollbar();
        for (var i = 0; i < EvaluationPageSize && evaluationTop + i < evaluationRows.Count; i++)
        {
            var row = evaluationRows[evaluationTop + i];
            var y = area.Y + i * 32;
            var summary = row.Contribution is null && row.Result.Length > 0;
            if (summary) DrawRectangle(new(area.X, y, area.Width, 30), new Color(40, 66, 75));
            Cell(row.Name, 0, row.Result.Length == 0 ? 1 : .38, y, Color.White, summary);
            Cell(row.Points, .38, .14, y, Color.White);
            Cell(row.Weight, .52, .12, y, Color.White);
            Cell(row.Result, .64, .16, y, Color.White, summary);
            if (row.Contribution is { } contribution)
            {
                var total = row.IsGeneral ? evaluationGeneralTotal : evaluationCircleTotal;
                var percent = contribution / total * 100;
                var text = evaluationValid && total != 0 && double.IsFinite(total) && double.IsFinite(percent) ? $"{percent:0.##}%" : "—";
                var maximum = maxima.GetValueOrDefault(row.IsGeneral);
                if (maximum > 0 && double.IsFinite(contribution))
                    DrawRectangle(new(area.X + area.Width * .8, y + 25, area.Width * .19 * (Math.Abs(contribution) / maximum), 3),
                        contribution < 0 ? Color.Orange : Color.Turquoise);
                Cell(text, .8, .2, y, Color.White);
            }
        }
        DrawRectangle(bar.Track, new Color(13, 17, 22));
        DrawRectangle(bar.Thumb, bar.Maximum == 0 ? new Color(48, 57, 68) : new Color(104, 157, 204));
        var bounds = ModalBounds();
        textRenderer?.Draw("ホイール・↑↓・PageUp/Down・Home/End ／ 右のスクロールバー ／ ×で閉じる",
            ToRectangle(new ScreenRectangle(area.X, bounds.Y + bounds.Height - 38, area.Width, 24)), Color.LightGray, 13);
    }
}
