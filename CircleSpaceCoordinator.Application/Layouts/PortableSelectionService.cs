namespace CircleSpaceCoordinator.Application.Layouts;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Core.Validation;

public static class PortableSelectionService
{
    public static CircleSpaceProject Extract(CircleSpaceProject project, string layoutId, SpaceDefinitionCatalog commonDefinitions)
    {
        var layout = project.DeskLayouts.Single(item => item.Id == layoutId);
        var catalog = layout.Definitions ?? commonDefinitions;
        catalog.Validate(requireRepresentativeCell: false);
        var types = project.DeskTypes.Where(type => layout.DeskPlacements.Any(p => p.DeskTypeId == type.Id)).ToArray();
        var needed = catalog.Requests.SelectMany(request => request.Targets).Select(target => target.TypeId).ToHashSet();
        foreach (var type in types)
            if (type.Space is { } space) needed.Add(space.DefinitionId);
        var definitions = catalog.Types.Where(type => needed.Contains(type.Id)).ToList();
        // Placed frames retain their exact historical shape, even if the template was edited later.
        foreach (var type in types)
            if (type.Space is { } space)
            {
                var historical = new SpaceTypeDefinition(space.DefinitionId, type.Name, space.Kind, space.Width, space.Height,
                    space.Cells.Select(cell => new SpaceCell(cell.X, cell.Y, cell.Area)).ToArray(), space.Edges)
                    { Connections = space.Connections };
                var existing = definitions.FirstOrDefault(definition => definition.Id == space.DefinitionId);
                if (existing is null) definitions.Add(historical);
                else if (!SameShape(existing, historical))
                    throw new InvalidDataException($"フレーム「{type.Name}」の配置時の定義とカタログの定義が異なります。\nフレーム定義を配置時の内容に揃えるか、現在の定義でフレームを配置し直してから書き出してください。");
            }
        catalog = catalog with { Types = definitions.ToArray() };
        catalog.Validate(requireRepresentativeCell: false);
        var confidential = project.IsConfidential || layout.IsConfidential || catalog.IsConfidential;
        var portable = new CircleSpaceProject(project.SchemaVersion, "portable-frame-layout", layout.Name,
            project.Venue, types, [], new EvaluationConfiguration([], []), [])
        {
            IsConfidential = confidential,
            BlockStyles = project.BlockStyles.Where(style => layout.SeatLabels.Any(label => label.BlockName == style.BlockNumber)).ToArray(),
            DeskLayouts = [layout with { Definitions = catalog, IsConfidential = confidential }],
        };
        return portable;
    }

    public static CircleSpaceProject Apply(CircleSpaceProject project, PortablePackage package, IReadOnlyList<PortableImportItem> selection)
    {
        if (selection.Count is < 1 or > 100 || selection.Select(item => item.ItemId).Distinct().Count() != selection.Count)
            throw new InvalidOperationException("取込み対象を重複なく選択してください。");
        if (package.Materials.Count(material => material.Kind == "venue" && selection.Any(item => item.ItemId == material.Id)) > 1)
            throw new InvalidOperationException("採用する会場は１件だけ選択してください。");
        var targetEdits = selection.Where(selected => selected.TargetLayoutId is not null &&
            (selected.Mode is "insert" or "replace" || package.Materials.Any(material =>
                material.Id == selected.ItemId && material.Kind is "frame-definition" or "request-definition")));
        if (targetEdits.GroupBy(selected => selected.TargetLayoutId).Any(group =>
            group.Any(selected => selected.Mode == "replace") && group.Count() > 1))
            throw new InvalidOperationException("置換先の配置案への取込みは１件ずつ確定してください。同じ案への追加と置換は同時に実行できません。");
        // Build a detached candidate. The workspace commits only after every item succeeds.
        project = LayoutProjection.MigrateLegacyPlans(project);
        foreach (var selected in selection)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(selected.NewId);
            ArgumentException.ThrowIfNullOrWhiteSpace(selected.Name);
            if (selected.Mode is not ("add" or "insert" or "replace")) throw new InvalidOperationException("未対応の取込み方法です。");
            var material = package.Materials.SingleOrDefault(item => item.Id == selected.ItemId);
            if (material is not null)
            {
                if (selected.Mode != "add") throw new InvalidOperationException("素材は追加方式で取り込んでください。");
                project = PortableMaterialService.Apply(project, material, selected, package.IsConfidential || material.IsConfidential);
                continue;
            }
            var knowledge = package.Knowledge.SingleOrDefault(item => "knowledge:" + item.Id == selected.ItemId);
            if (knowledge is not null)
            {
                if (selected.Mode != "add") throw new InvalidOperationException("知見は新規追加してください。");
                project = ChannelKnowledgeService.Add(project, knowledge with { Id = selected.NewId, Name = selected.Name,
                    IsConfidential = package.IsConfidential || knowledge.IsConfidential });
                continue;
            }
            var item = package.Items.Single(item => item.Id == selected.ItemId);
            project = PortableFragmentService.Apply(project, item, selected, package.IsConfidential);
        }
        project = project with { Plans = LayoutProjection.ToPlans(project) };
        var issues = ProjectValidator.Validate(project);
        if (issues.Count > 0) throw new ProjectValidationException(issues);
        return project;
    }

    private static bool SameShape(SpaceTypeDefinition first, SpaceTypeDefinition second) =>
        first.Kind == second.Kind && first.Width == second.Width && first.Height == second.Height &&
        first.Edges.SequenceEqual(second.Edges) &&
        first.NormalizeCellStates().Cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X)
            .SequenceEqual(second.NormalizeCellStates().Cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X)) &&
        EffectiveConnections(first).SetEquals(EffectiveConnections(second));

    private static HashSet<FrameCellConnection> EffectiveConnections(SpaceTypeDefinition definition) =>
        (definition.Connections ?? FrameCellConnection.Adjacent(definition.Cells.Where(cell => cell.Area > 0)
            .Select(cell => new GridPosition(cell.X, cell.Y)).ToHashSet())).Select(link => link.Normalize()).ToHashSet();
}
