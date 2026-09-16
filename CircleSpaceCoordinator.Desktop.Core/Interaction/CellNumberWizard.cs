namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;

public enum CellNumberOrder { VenueTopLeft, FrameTopLeft }

public sealed record CellNumberTarget(string FrameId, GridPosition RelativeCell, GridPosition Cell);

/// <summary>Builds a numbering draft without changing the plan.</summary>
public sealed class CellNumberWizard
{
    private readonly Plan plan;
    private readonly CellNumberTarget[] venueOrder;
    private readonly CellNumberTarget[] frameOrder;
    public int Count => venueOrder.Length;
    public string DefaultNumbers => string.Join(", ", Enumerable.Range(1, Count));
    public int NumbersPerFrame => frameOrder.GroupBy(target => target.FrameId).Select(group => group.Count()).DefaultIfEmpty(0).Max();
    public string DefaultFrameNumbers => string.Join(", ", Enumerable.Range(1, NumbersPerFrame));

    public CellNumberWizard(CircleSpaceProject project, Plan plan, Func<GridPosition, bool>? includes = null)
    {
        this.plan = plan;
        var types = project.DeskTypes.ToDictionary(type => type.Id);
        var frames = plan.DeskPlacements.Where(frame => types[frame.DeskTypeId].Footprint.Count > 0)
            .OrderBy(frame => frame.GetOccupiedCells(types[frame.DeskTypeId]).Min(cell => cell.Y))
            .ThenBy(frame => frame.GetOccupiedCells(types[frame.DeskTypeId]).Min(cell => cell.X))
            .ThenBy(frame => frame.Id, StringComparer.Ordinal);
        frameOrder = frames.SelectMany(frame => frame.GetSeatCells(types[frame.DeskTypeId])
            .Where(cell => includes?.Invoke(cell) ?? true)
            .Select(cell => new CellNumberTarget(frame.Id,
                new GridPosition(cell.X - frame.Anchor.X, cell.Y - frame.Anchor.Y)
                    .Rotate((QuarterTurn)((4 - (int)frame.Orientation) % 4)), cell))
            .OrderBy(target => target.RelativeCell.Y).ThenBy(target => target.RelativeCell.X)).ToArray();
        venueOrder = frameOrder.OrderBy(target => target.Cell.Y).ThenBy(target => target.Cell.X)
            .ThenBy(target => target.FrameId, StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyList<CellNumberTarget> Targets(CellNumberOrder order) =>
        Array.AsReadOnly(order == CellNumberOrder.VenueTopLeft ? venueOrder : frameOrder);

    public string[] ParseNumbers(string text, bool repeatPerFrame = false)
    {
        var values = text.Split(',').Select(value => value.Trim()).ToArray();
        var required = repeatPerFrame ? NumbersPerFrame : Count;
        if (values.Length != required) throw new ArgumentException($"番号を{required}個入力してください（現在{values.Length}個）。" +
            (repeatPerFrame ? "フレーム内の対象セル数の最大値に合わせてください。" : $"対象は{Count}セルです。"));
        if (values.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 80))
            throw new ArgumentException("各番号は1～80文字で入力してください。空の項目は使えません。");
        return values;
    }

    public string[] AssignedNumbers(CellNumberOrder order, string text, bool repeatPerFrame = false)
    {
        var numbers = ParseNumbers(text, repeatPerFrame);
        if (!repeatPerFrame) return numbers;
        var offsets = new Dictionary<string, int>(StringComparer.Ordinal);
        return Targets(order).Select(target =>
        {
            var offset = offsets.GetValueOrDefault(target.FrameId);
            offsets[target.FrameId] = offset + 1;
            return numbers[offset];
        }).ToArray();
    }

    public DeskSeatLabel[] Build(CellNumberOrder order, string text, bool repeatPerFrame = false)
    {
        var numbers = AssignedNumbers(order, text, repeatPerFrame);
        var labels = plan.SeatLabels.ToDictionary(label => (label.DeskPlacementId, label.RelativeCell));
        var targets = Targets(order);
        for (var i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            var key = (target.FrameId, target.RelativeCell);
            var previous = labels.GetValueOrDefault(key) ?? new DeskSeatLabel(target.FrameId, target.RelativeCell, "", "");
            labels[key] = previous with { SeatName = numbers[i] };
        }
        return labels.Values.ToArray();
    }
}
