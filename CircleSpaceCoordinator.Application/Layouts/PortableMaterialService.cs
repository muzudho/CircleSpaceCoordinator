namespace CircleSpaceCoordinator.Application.Layouts;
using System.Text.Json;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Validation;
using CircleSpaceCoordinator.Engine.Model;

public static class PortableMaterialService
{
    public static PortableMaterial Extract(CircleSpaceProject project, SpaceDefinitionCatalog common, PortableMaterialSelection selected)
    {
        if (selected.Kind == "genre-styles")
            return new("genre-styles:" + selected.SourceId, selected.Kind, project.GetGenreCodeTableName(), project.IsConfidential)
            { Credits = project.GenreStyleCredits, GenreStyles = project.GenreStyles.ToArray(), OverallComment = project.GenreStyleComment,
                GenreCodeOrder = project.GenreCodeOrder.ToArray(), GenreCodeOrderComment = project.GenreCodeOrderComment };
        if (selected.Kind == "block-styles")
            return new("block-styles:" + selected.SourceId, selected.Kind, "ブロック色の対応表", project.IsConfidential)
            { Credits = project.BlockStyleCredits, BlockStyles = project.BlockStyles.ToArray() };
        if (selected.Kind == "venue")
        {
            var venue = project.Venue;
            return new("venue:" + venue.Id, "venue", venue.Name, project.IsConfidential)
            { Credits = venue.Credits, Venue = new(venue.Name, venue.Width, venue.Height, venue.BlockedCells.ToArray(),
                venue.Zones.Select(zone => new KnowledgeZone(zone.Name, zone.Cells.ToArray())).ToArray()) };
        }
        var layout = selected.LayoutId is null ? null : project.DeskLayouts.Single(item => item.Id == selected.LayoutId);
        var catalog = layout?.Definitions ?? common;
        catalog.Validate(requireRepresentativeCell: false);
        var confidential = project.IsConfidential || layout?.IsConfidential == true || catalog.IsConfidential;
        var id = selected.Kind + ":" + (selected.LayoutId ?? "common") + ":" + selected.SourceId;
        if (selected.Kind == "frame-definition")
        {
            var type = catalog.Types.Single(item => item.Id == selected.SourceId);
            return new(id, selected.Kind, type.Name, confidential) { Credits = type.Credits, Definitions = new([type], []) { IsConfidential = confidential } };
        }
        if (selected.Kind == "request-definition")
        {
            var request = catalog.Requests.Single(item => item.Id == selected.SourceId);
            var ids = request.Targets.Select(target => target.TypeId).ToHashSet();
            return new(id, selected.Kind, request.Value, confidential)
            { Credits = request.Credits, Definitions = new(catalog.Types.Where(type => ids.Contains(type.Id)).ToArray(), [request]) { IsConfidential = confidential } };
        }
        throw new InvalidOperationException("未対応の素材種類です。");
    }

    public static SpaceDefinitionCatalog Merge(SpaceDefinitionCatalog target, SpaceDefinitionCatalog incoming, string prefix, string? requestValue = null,
        Dictionary<string, string>? typeMapping = null)
    {
        target.Validate(requireRepresentativeCell: false);
        incoming.Validate(requireRepresentativeCell: false);
        var types = target.Types.ToList();
        var requests = target.Requests.ToList();
        var ids = types.Select(type => type.Id).ToHashSet(StringComparer.Ordinal);
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var type in incoming.Types)
        {
            var existing = types.FirstOrDefault(item => item.Id == type.Id);
            if (existing is not null && Same(existing with { Credits = null }, type with { Credits = null }))
            {
                if (existing.Credits is null) types[types.IndexOf(existing)] = existing with { Credits = type.Credits };
                mapping.Add(type.Id, existing.Id); continue;
            }
            var id = type.Id;
            if (!ids.Add(id))
            {
                id = prefix + "-" + type.Id;
                while (!ids.Add(id)) id += "-copy";
            }
            mapping.Add(type.Id, id);
            types.Add(type with { Id = id });
        }
        var requestIds = requests.Select(request => request.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var request in incoming.Requests)
        {
            var mapped = request with { Value = requestValue ?? request.Value,
                Targets = request.Targets.Select(target => target with { TypeId = mapping[target.TypeId] }).ToArray() };
            var sameValue = requests.FirstOrDefault(item => item.Value == mapped.Value);
            if (sameValue is not null)
            {
                if (Same(sameValue with { Id = mapped.Id, Credits = null }, mapped with { Credits = null }))
                {
                    if (sameValue.Credits is null) requests[requests.IndexOf(sameValue)] = sameValue with { Credits = mapped.Credits };
                    continue;
                }
                throw new InvalidOperationException($"申込値「{mapped.Value}」の意味・参照先が既存定義と異なります。読込み値を変更するか、この項目を見送ってください。");
            }
            var id = mapped.Id;
            if (!requestIds.Add(id))
            {
                id = prefix + "-" + id;
                while (!requestIds.Add(id)) id += "-copy";
            }
            requests.Add(mapped with { Id = id });
        }
        var result = target with { Types = types, Requests = requests, IsConfidential = target.IsConfidential || incoming.IsConfidential };
        result.Validate(requireRepresentativeCell: false);
        if (typeMapping is not null) foreach (var pair in mapping) typeMapping.Add(pair.Key, pair.Value);
        return result;
    }

    public static CircleSpaceProject Apply(CircleSpaceProject project, PortableMaterial material, PortableImportItem selected, bool confidential)
    {
        material.Validate();
        if (material.Kind == "genre-styles")
            project = project with { GenreStyles = material.GenreStyles!, GenreStyleCredits = material.Credits, GenreStyleComment = material.OverallComment,
                GenreCodeTableName = GenreStyleDefinition.NormalizeTableName(selected.Name),
                GenreCodeOrder = material.GenreCodeOrder ?? project.GenreCodeOrder,
                GenreCodeOrderComment = material.GenreCodeOrderComment };
        else if (material.Kind == "block-styles")
            project = project with { BlockStyles = material.BlockStyles!, BlockStyleCredits = material.Credits };
        else if (material.Kind == "venue")
        {
            var source = material.Venue!;
            var venue = new Venue(selected.NewId, selected.Name, source.Width, source.Height, source.BlockedCells.ToHashSet())
            { Credits = material.Credits, Zones = source.Zones.Select((zone, index) => new VenueZone("zone-" + index, zone.Name, zone.Cells.ToHashSet())).ToArray() };
            if (!FrameLayoutImportService.CanAdoptVenue(project) && !FrameLayoutImportService.SameGeometry(project.Venue, venue))
                throw new InvalidOperationException("既存配置があるため、形状の異なる会場は取り込めません。新しいイベントで取り込んでください。");
            project = project with { Venue = venue };
        }
        else
        {
            var layout = project.DeskLayouts.SingleOrDefault(item => item.Id == selected.TargetLayoutId)
                ?? throw new InvalidOperationException("定義を追加するフレーム配置案を指定してください。");
            var incoming = material.Definitions!;
            if (material.Kind == "frame-definition") incoming = incoming with { Types = [incoming.Types[0] with { Name = selected.Name }] };
            var merged = Merge(layout.Definitions ?? selected.FallbackDefinitions ?? new([], []), incoming, selected.NewId, selected.RequestValue);
            project = project with { DeskLayouts = project.DeskLayouts.Select(item => item.Id == layout.Id
                ? item with { Definitions = merged, IsConfidential = item.IsConfidential || confidential } : item).ToArray() };
        }
        project = project with { IsConfidential = project.IsConfidential || confidential };
        var issues = ProjectValidator.Validate(project);
        if (issues.Count > 0) throw new ProjectValidationException(issues);
        return project;
    }

    public static SpaceDefinitionCatalog ApplyCatalog(PortableCatalogRequest request)
    {
        if (request.Selection.Count == 0 || request.Selection.Select(item => item.ItemId).Distinct().Count() != request.Selection.Count)
            throw new InvalidOperationException("登録する定義を重複なく選択してください。");
        var catalog = request.Catalog;
        foreach (var selected in request.Selection)
        {
            var material = request.Package.Materials.SingleOrDefault(item => item.Id == selected.ItemId);
            if (material?.Definitions is null) throw new InvalidOperationException("共通カタログに登録できるのはフレーム定義と申込定義だけです。");
            material.Validate();
            var definitions = material.Definitions;
            if (material.Kind == "frame-definition") definitions = definitions with { Types = [definitions.Types[0] with { Name = selected.Name }] };
            catalog = Merge(catalog, definitions, selected.NewId, selected.RequestValue);
            catalog = catalog with { IsConfidential = catalog.IsConfidential || material.IsConfidential || request.Package.IsConfidential };
        }
        return catalog;
    }

    private static bool Same<T>(T first, T second) => JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second);
}
