namespace CircleSpaceCoordinator.Application.Layouts;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;

public static class FrameLayoutImportService
{
    public static CircleSpaceProject Add(CircleSpaceProject project, CircleSpaceProject incoming, string id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        name = name.Trim();
        if (incoming.DeskLayouts.Count != 1 || incoming.Participants.Count != 0 || incoming.CircleLayouts.Count != 0)
            throw new InvalidOperationException("フレーム配置データを指定してください。");
        var source = incoming.DeskLayouts[0];
        if (source.Definitions is null) throw new InvalidOperationException("配置の定義がありません。");
        source.Definitions.Validate(requireRepresentativeCell: false);
        project = LayoutProjection.MigrateLegacyPlans(project);
        if (project.DeskLayouts.Any(layout => layout.Id == id))
            throw new InvalidOperationException("フレーム配置のIDが重複しています。");
        if (project.DeskLayouts.Any(layout => layout.Name == name))
            throw new InvalidOperationException("同名のフレーム配置案があります。別の名前を入力してください。");

        // Venue geometry is shared by all layouts. Adopt it only before anything has been placed.
        var empty = CanAdoptVenue(project);
        if (!empty && !SameGeometry(project.Venue, incoming.Venue))
            throw new InvalidOperationException("会場の寸法・障害物・ゾーンが異なるため追加できません。既存の配置を保つため、同じ会場のフレーム配置データを指定してください。");

        var styles = project.BlockStyles.ToList();
        foreach (var style in incoming.BlockStyles)
        {
            var current = styles.FirstOrDefault(item => item.BlockNumber == style.BlockNumber);
            if (current is null) styles.Add(style);
            else if (current != style)
                throw new InvalidOperationException($"ブロック「{style.BlockNumber}」の表示設定が異なります。設定を揃えてから読み込んでください。");
        }
        var usedIds = project.DeskTypes.Select(type => type.Id).ToHashSet(StringComparer.Ordinal);
        var typeIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var types = project.DeskTypes.ToList();
        foreach (var type in incoming.DeskTypes)
        {
            var newId = $"{id}-type-{typeIds.Count + 1}";
            while (!usedIds.Add(newId)) newId += "-copy";
            typeIds.Add(type.Id, newId);
            types.Add(type with { Id = newId });
        }
        var imported = source with
        {
            Id = id,
            Name = name,
            IsConfidential = source.IsConfidential || incoming.IsConfidential,
            DeskPlacements = source.DeskPlacements.Select(desk => desk with { DeskTypeId = typeIds[desk.DeskTypeId] }).ToArray(),
        };
        var result = project with
        {
            DeskLayouts = [.. project.DeskLayouts, imported],
            DeskTypes = types,
            Venue = empty ? incoming.Venue : project.Venue,
            BlockStyles = styles,
            IsConfidential = project.IsConfidential || incoming.IsConfidential || imported.IsConfidential,
        };
        var issues = ProjectValidator.Validate(result);
        if (issues.Count > 0) throw new ProjectValidationException(issues);
        return result;
    }

    public static bool CanAdoptVenue(CircleSpaceProject project)
    {
        var migrated = LayoutProjection.MigrateLegacyPlans(project);
        return migrated.DeskLayouts.All(layout => layout.DeskPlacements.Count == 0 && layout.FacingRegions.Count == 0) &&
            migrated.CircleLayouts.All(layout => layout.Assignments.Count == 0 && layout.TemporaryPlacements.Count == 0);
    }

    private static bool SameGeometry(Venue first, Venue second) =>
        first.Width == second.Width && first.Height == second.Height && first.BlockedCells.SetEquals(second.BlockedCells) &&
        first.Zones.Count == second.Zones.Count && first.Zones.All(zone =>
            second.Zones.Any(other => other.Name == zone.Name && other.Cells.SetEquals(zone.Cells)));
}
