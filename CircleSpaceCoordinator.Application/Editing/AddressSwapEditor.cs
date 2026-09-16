namespace CircleSpaceCoordinator.Application.Editing;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;

public static class AddressSwapEditor
{
    public static CircleSpaceProject Swap(CircleSpaceProject project, string planId, int channel,
        GridPosition source, GridPosition destination, int width, int height, IReadOnlyList<string> sourceFrameIds)
    {
        void Reject(string message) => throw new ProjectValidationException([new ValidationIssue("address.swap", "plan", message)]);
        if (channel is < 0 or > 2 || width <= 0 || height <= 0) Reject("番地チャンネルと範囲を確認してください。");
        bool Valid(GridPosition origin) => origin.X >= 0 && origin.Y >= 0 &&
            (long)origin.X + width <= project.Venue.Width && (long)origin.Y + height <= project.Venue.Height;
        if (!Valid(source) || !Valid(destination)) Reject("会場内の範囲を選択してください。");
        if (source == destination) return project;
        bool Inside(GridPosition cell, GridPosition origin) => cell.X >= origin.X && cell.X < origin.X + width && cell.Y >= origin.Y && cell.Y < origin.Y + height;
        var plan = project.Plans.Single(item => item.Id == planId);
        var types = project.DeskTypes.ToDictionary(type => type.Id);
        Plan updated;
        if (channel == 1)
        {
            var footprints = plan.DeskPlacements.ToDictionary(frame => frame.Id, frame => frame.GetOccupiedCells(types[frame.DeskTypeId]));
            var selected = sourceFrameIds.ToHashSet(StringComparer.Ordinal);
            DeskPlacement[] Order(IEnumerable<DeskPlacement> frames) => frames.OrderBy(frame => footprints[frame.Id].Min(cell => cell.Y))
                .ThenBy(frame => footprints[frame.Id].Min(cell => cell.X)).ThenBy(frame => frame.Id, StringComparer.Ordinal).ToArray();
            var first = Order(plan.DeskPlacements.Where(frame => selected.Contains(frame.Id)));
            var second = Order(plan.DeskPlacements.Where(frame => footprints[frame.Id].Any(cell => Inside(cell, destination))));
            if (first.Length == 0 || first.Length != selected.Count || first.Length != second.Length)
                Reject("移動元と移動先のフレーム数をそろえてください。");
            if (second.Any(frame => selected.Contains(frame.Id))) Reject("移動元と移動先に同じフレームを含めることはできません。");
            var numbers = new Dictionary<string, string?>();
            for (var i = 0; i < first.Length; i++)
            {
                numbers[first[i].Id] = second[i].DeskNumber;
                numbers[second[i].Id] = first[i].DeskNumber;
            }
            updated = plan with { DeskPlacements = plan.DeskPlacements.Select(frame => numbers.TryGetValue(frame.Id, out var number)
                ? frame with { DeskNumber = number } : frame).ToArray() };
        }
        else
        {
            if (source.X < destination.X + width && destination.X < source.X + width && source.Y < destination.Y + height && destination.Y < source.Y + height)
                Reject("重ならない2つの範囲を選択してください。");
            var cells = plan.DeskPlacements.SelectMany(frame => frame.GetSeatCells(types[frame.DeskTypeId]).Select(cell =>
                (Cell: cell, Key: (frame.Id, new GridPosition(cell.X - frame.Anchor.X, cell.Y - frame.Anchor.Y)
                    .Rotate((QuarterTurn)((4 - (int)frame.Orientation) % 4)))))).ToDictionary(item => item.Cell, item => item.Key);
            var first = cells.Keys.Where(cell => Inside(cell, source)).ToArray();
            var second = cells.Keys.Where(cell => Inside(cell, destination)).ToArray();
            var delta = new GridPosition(destination.X - source.X, destination.Y - source.Y);
            if (first.Length == 0 || first.Length != second.Length || first.Any(cell => !cells.ContainsKey(cell + delta)))
                Reject("移動元と移動先の配置可能セルの位置・形をそろえてください。");
            var labels = plan.SeatLabels.ToDictionary(label => (label.DeskPlacementId, label.RelativeCell));
            DeskSeatLabel Read((string Id, GridPosition Cell) key) => labels.GetValueOrDefault(key) ?? new(key.Id, key.Cell, "", "");
            void Write(DeskSeatLabel label)
            {
                var key = (label.DeskPlacementId, label.RelativeCell);
                if (string.IsNullOrWhiteSpace(label.BlockName) && string.IsNullOrWhiteSpace(label.SeatName)) labels.Remove(key);
                else labels[key] = label;
            }
            foreach (var cell in first)
            {
                var a = Read(cells[cell]);
                var b = Read(cells[cell + delta]);
                Write(channel == 0 ? a with { BlockName = b.BlockName } : a with { SeatName = b.SeatName });
                Write(channel == 0 ? b with { BlockName = a.BlockName } : b with { SeatName = a.SeatName });
            }
            updated = plan with { SeatLabels = labels.Values.ToArray() };
        }
        var result = project with { Plans = project.Plans.Select(item => item.Id == planId ? updated : item).ToArray() };
        var issues = ProjectValidator.Validate(result);
        if (issues.Count > 0) throw new ProjectValidationException(issues);
        return result;
    }
}
