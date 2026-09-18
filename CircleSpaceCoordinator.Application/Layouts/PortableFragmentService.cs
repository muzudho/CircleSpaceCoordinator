namespace CircleSpaceCoordinator.Application.Layouts;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Validation;
using CircleSpaceCoordinator.Engine.Model;

public static class PortableFragmentService
{
    public static CircleSpaceProject Extract(CircleSpaceProject project, string layoutId, IReadOnlyList<string> frameIds, SpaceDefinitionCatalog common)
    {
        var layout = project.DeskLayouts.Single(item => item.Id == layoutId);
        var ids = frameIds.ToHashSet(StringComparer.Ordinal);
        if (ids.Count == 0 || ids.Count != frameIds.Count || ids.Any(id => layout.DeskPlacements.All(desk => desk.Id != id)))
            throw new InvalidOperationException("切り出すフレームを重複なく選択してください。");
        var desks = layout.DeskPlacements.Where(desk => ids.Contains(desk.Id)).ToArray();
        var types = project.DeskTypes.ToDictionary(type => type.Id);
        var cells = desks.SelectMany(desk => desk.GetOccupiedCells(types[desk.DeskTypeId])).ToHashSet();
        var minX = cells.Min(cell => cell.X); var maxX = cells.Max(cell => cell.X);
        var minY = cells.Min(cell => cell.Y); var maxY = cells.Max(cell => cell.Y);
        var catalog = layout.Definitions ?? common;
        var definitionIds = desks.Select(desk => types[desk.DeskTypeId].Space?.DefinitionId).OfType<string>().ToHashSet();
        bool Inside(GridPosition cell) => cell.X >= minX && cell.X <= maxX && cell.Y >= minY && cell.Y <= maxY;
        var fragment = layout with
        {
            Name = layout.Name + "（部分）", DeskPlacements = desks,
            Definitions = new(catalog.Types.Where(type => definitionIds.Contains(type.Id)).ToArray(), []) { IsConfidential = catalog.IsConfidential },
            SeatLabels = layout.SeatLabels.Where(label => ids.Contains(label.DeskPlacementId)).ToArray(),
            IslandStarts = layout.IslandStarts.Where(start => ids.Contains(start.DeskPlacementId)).ToArray(),
            IslandConnectors = layout.IslandConnectors.Where(link => ids.Contains(link.FirstDeskId) && ids.Contains(link.SecondDeskId)).ToArray(),
            DisabledIslandConnections = layout.DisabledIslandConnections.Where(link => cells.Contains(link.FirstCell) && cells.Contains(link.SecondCell)).ToArray(),
            // Include a facing region only when its full rectangle and every touching frame are selected.
            FacingRegions = layout.FacingRegions.Where(region => Inside(region.FirstCorner) && Inside(region.SecondCorner) &&
                layout.DeskPlacements.Where(desk => !ids.Contains(desk.Id)).All(desk => !desk.GetOccupiedCells(types[desk.DeskTypeId]).Any(cell =>
                    cell.X >= Math.Min(region.FirstCorner.X, region.SecondCorner.X) && cell.X <= Math.Max(region.FirstCorner.X, region.SecondCorner.X) &&
                    cell.Y >= Math.Min(region.FirstCorner.Y, region.SecondCorner.Y) && cell.Y <= Math.Max(region.FirstCorner.Y, region.SecondCorner.Y)))).ToArray(),
        };
        return PortableSelectionService.Extract(project with { DeskLayouts = [fragment] }, layoutId, common);
    }

    public static CircleSpaceProject Apply(CircleSpaceProject project, PortableItem item, PortableImportItem selected, bool confidential)
    {
        var incoming = item.Project with { IsConfidential = confidential || item.Project.IsConfidential };
        var delta = new GridPosition(selected.OffsetX, selected.OffsetY);
        var source = incoming.DeskLayouts.Single();
        if (selected.OffsetX != 0 || selected.OffsetY != 0)
        {
            source = source with
            {
                DeskPlacements = source.DeskPlacements.Select(desk => desk with { Anchor = desk.Anchor + delta }).ToArray(),
                IslandConnectors = source.IslandConnectors.Select(link => link with
                    { FirstCell = link.FirstCell is { } first ? first + delta : null, SecondCell = link.SecondCell is { } second ? second + delta : null }).ToArray(),
                DisabledIslandConnections = source.DisabledIslandConnections.Select(link => new DisabledIslandConnection(link.FirstCell + delta, link.SecondCell + delta)).ToArray(),
                FacingRegions = source.FacingRegions.Select(region => region with { FirstCorner = region.FirstCorner + delta, SecondCorner = region.SecondCorner + delta }).ToArray(),
            };
            incoming = incoming with { DeskLayouts = [source] };
        }
        if (selected.Mode == "add") return FrameLayoutImportService.Add(project, incoming, selected.NewId, selected.Name);
        var target = project.DeskLayouts.SingleOrDefault(layout => layout.Id == selected.TargetLayoutId)
            ?? throw new InvalidOperationException("挿入・置換する配置案を明示的に選択してください。");
        if (selected.Mode == "replace")
        {
            if (project.CircleLayouts.Any(circle => circle.DeskLayoutId == target.Id))
                throw new InvalidOperationException("サークル配置から参照されている案は置換できません。別案として追加してください。");
            if (!FrameLayoutImportService.SameGeometry(project.Venue, incoming.Venue))
                throw new InvalidOperationException("置換では会場の形状を変更できません。");
            var reduced = project with { DeskLayouts = project.DeskLayouts.Where(layout => layout.Id != target.Id).ToArray(), Plans = [] };
            var result = FrameLayoutImportService.Add(reduced, incoming, target.Id, selected.Name);
            var replacement = result.DeskLayouts.Single(layout => layout.Id == target.Id) with { IsConfidential = target.IsConfidential || incoming.IsConfidential };
            return result with { DeskLayouts = project.DeskLayouts.Select(layout => layout.Id == target.Id ? replacement : layout).ToArray() };
        }
        if (!FrameLayoutImportService.SameGeometry(project.Venue, incoming.Venue))
            throw new InvalidOperationException("挿入する部分配置の会場形状が一致しません。");
        // Allocate isolated physical types using the same policy as an ordinary import.
        var staged = FrameLayoutImportService.Add(project, incoming, selected.NewId, "部分挿入-" + selected.NewId);
        var fragment = staged.DeskLayouts.Last();
        var definitionIds = new Dictionary<string, string>();
        var definitions = PortableMaterialService.Merge(target.Definitions ?? selected.FallbackDefinitions ?? new([], []),
            fragment.Definitions!, selected.NewId, typeMapping: definitionIds);
        var frameIds = target.DeskPlacements.Select(desk => desk.Id).ToHashSet();
        var mapping = new Dictionary<string, string>();
        foreach (var desk in fragment.DeskPlacements)
        {
            var id = selected.NewId + "-" + desk.Id;
            while (!frameIds.Add(id)) id += "-copy";
            mapping.Add(desk.Id, id);
        }
        var connectionIds = target.IslandConnectors.Select(link => link.Id).Concat(target.FacingRegions.Select(region => region.Id)).ToHashSet();
        string NewId(string id) { id = selected.NewId + "-" + id; while (!connectionIds.Add(id)) id += "-copy"; return id; }
        var merged = target with
        {
            Definitions = definitions, IsConfidential = target.IsConfidential || incoming.IsConfidential,
            DeskPlacements = [.. target.DeskPlacements, .. fragment.DeskPlacements.Select(desk => desk with { Id = mapping[desk.Id] })],
            SeatLabels = [.. target.SeatLabels, .. fragment.SeatLabels.Select(label => label with { DeskPlacementId = mapping[label.DeskPlacementId] })],
            IslandStarts = [.. target.IslandStarts, .. fragment.IslandStarts.Select(start => start with { DeskPlacementId = mapping[start.DeskPlacementId] })],
            IslandConnectors = [.. target.IslandConnectors, .. fragment.IslandConnectors.Select(link => link with
                { Id = NewId(link.Id), FirstDeskId = mapping[link.FirstDeskId], SecondDeskId = mapping[link.SecondDeskId] })],
            DisabledIslandConnections = target.DisabledIslandConnections.Concat(fragment.DisabledIslandConnections).Distinct().ToArray(),
            FacingRegions = [.. target.FacingRegions, .. fragment.FacingRegions.Select(region => region with { Id = NewId(region.Id) })],
        };
        var addedTypes = staged.DeskTypes.Skip(project.DeskTypes.Count).Select(type => type.Space is { } space
            ? type with { Space = space with { DefinitionId = definitionIds[space.DefinitionId] } } : type).ToArray();
        var candidate = staged with
        {
            DeskLayouts = project.DeskLayouts.Select(layout => layout.Id == target.Id ? merged : layout).ToArray(),
            DeskTypes = [.. project.DeskTypes, .. addedTypes],
        };
        candidate = candidate with { Plans = LayoutProjection.ToPlans(candidate) };
        var issues = ProjectValidator.Validate(candidate);
        if (issues.Count > 0) throw new ProjectValidationException(issues);
        return candidate;
    }
}
