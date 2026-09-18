namespace CircleSpaceCoordinator.EditorEngine;
using CircleSpaceCoordinator.Application.Layouts;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Engine.Contracts.V1;
using CircleSpaceCoordinator.Infrastructure.Json;
using Grpc.Core;

public sealed partial class EditorService
{
    public override Task<PortableReply> Portable(PortableRequest request, ServerCallContext context) => Run(() =>
    {
        string result;
        switch (request.Action)
        {
            case "export":
                var export = WireJson.Read<PortableExportRequest>(request.Json);
                if (export.LayoutIds.Count + export.KnowledgeIds.Count is < 1 or > ProjectPortableSerializer.MaximumItems ||
                    export.LayoutIds.Distinct().Count() != export.LayoutIds.Count || export.KnowledgeIds.Distinct().Count() != export.KnowledgeIds.Count)
                    throw new ArgumentException("書き出す配置案を重複なく1～100件選んでください。");
                var projects = export.LayoutIds.Select(id => PortableSelectionService.Extract(export.Project, id, export.Definitions)).ToArray();
                var knowledge = export.KnowledgeIds.Select(id => export.Project.ChannelKnowledge.Single(item => item.Id == id))
                    .Select(item => item with { IsConfidential = item.IsConfidential || export.Project.IsConfidential,
                        Cells = export.TemplateOnly ? [] : item.Cells, Venue = export.TemplateOnly ? null : item.Venue }).ToArray();
                result = ProjectPortableSerializer.Save(export.Name, export.Description, export.Tags.ToArray(), export.IsConfidential, projects, knowledge);
                break;
            case "parse":
                var (document, sources) = ProjectPortableSerializer.Load(request.Json);
                result = WireJson.Write(new PortablePackage(document.Name, document.Description, document.Tags, document.IsConfidential,
                    document.Items.Select((item, index) => new PortableItem(item.Id, item.Name, sources[index])).ToArray())
                    { Knowledge = document.Knowledge ?? [] });
                break;
            case "preview":
                var preview = WireJson.Read<PortablePreviewRequest>(request.Json);
                var candidate = PortableSelectionService.Apply(preview.Project, preview.Package, preview.Selection);
                var chosen = preview.Package.Items.Where(item => preview.Selection.Any(selected => selected.ItemId == item.Id)).ToArray();
                var mappings = new List<PortableTypeMapping>();
                var typeOffset = preview.Project.DeskTypes.Count;
                foreach (var selected in preview.Selection)
                {
                    var source = chosen.SingleOrDefault(item => item.Id == selected.ItemId);
                    if (source is null) continue;
                    foreach (var type in source.Project.DeskTypes)
                        mappings.Add(new(source.Id, type.Id, candidate.DeskTypes[typeOffset++].Id));
                }
                result = WireJson.Write(new PortablePreview(candidate.Venue.Name, chosen.Length,
                    candidate.DeskTypes.Count - preview.Project.DeskTypes.Count, candidate.IsConfidential)
                {
                    VenueAction = chosen.Length > 0 && FrameLayoutImportService.CanAdoptVenue(preview.Project) ? "入力元を採用" : "既存を保持",
                    DefinitionCount = chosen.Sum(item => item.Project.DeskLayouts[0].Definitions!.Types.Count),
                    RequestCount = chosen.Sum(item => item.Project.DeskLayouts[0].Definitions!.Requests.Count),
                    KnowledgeCount = candidate.ChannelKnowledge.Count - preview.Project.ChannelKnowledge.Count,
                    Selection = preview.Selection,
                    TypeMappings = mappings,
                });
                break;
            default: throw new ArgumentException("未対応のポータブル照会です。");
        }
        return new PortableReply { Json = result };
    });
}
