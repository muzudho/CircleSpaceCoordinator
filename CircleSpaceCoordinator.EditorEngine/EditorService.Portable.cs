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
            case "create-shading-table":
                var newTable = WireJson.Read<PortableShadingTableCreate>(request.Json);
                result = ProjectPortableSerializer.CreateShadingTable(newTable.Json, newTable.Kind, newTable.Name,
                    newTable.Comment, newTable.Handle, newTable.WorkDate);
                break;
            case "create-empty":
                var packageName = CircleSpaceCoordinator.Core.Model.PersonCredits.NormalizeChangeLog(WireJson.Read<string>(request.Json));
                result = ProjectPortableSerializer.Save(packageName, "", [], false, []);
                break;
            case "export":
                var export = WireJson.Read<PortableExportRequest>(request.Json);
                if (export.LayoutIds.Count + export.KnowledgeIds.Count + export.Materials.Count is < 1 or > ProjectPortableSerializer.MaximumItems ||
                    export.LayoutIds.Distinct().Count() != export.LayoutIds.Count || export.KnowledgeIds.Distinct().Count() != export.KnowledgeIds.Count)
                    throw new ArgumentException("書き出す配置案を重複なく1～100件選んでください。");
                if (export.Fragments.Keys.Any(id => !export.LayoutIds.Contains(id))) throw new ArgumentException("部分配置の元となる案を選択してください。");
                var projects = export.LayoutIds.Select(id => export.Fragments.TryGetValue(id, out var frames)
                    ? PortableFragmentService.Extract(export.Project, id, frames, export.Definitions)
                    : PortableSelectionService.Extract(export.Project, id, export.Definitions))
                    .Select(project => PortableCreditsService.Provide(project, export.Handle)).ToArray();
                var materials = export.Materials.Select(selected => PortableCreditsService.Provide(PortableMaterialService.Extract(export.Project, export.Definitions, selected), export.Handle)).ToArray();
                var knowledge = export.KnowledgeIds.Select(id => export.Project.ChannelKnowledge.Single(item => item.Id == id))
                    .Select(item => item with { Credits = PortableCreditsService.Provide(item.Credits, export.Handle), IsConfidential = item.IsConfidential || export.Project.IsConfidential,
                        Cells = export.TemplateOnly ? [] : item.Cells, Venue = export.TemplateOnly ? null : item.Venue }).ToArray();
                result = ProjectPortableSerializer.Save(export.Name, export.Description, export.Tags.ToArray(), export.IsConfidential, projects, knowledge, materials,
                    export.Fragments.Keys.ToHashSet());
                break;
            case "parse":
                var (document, sources) = ProjectPortableSerializer.Load(request.Json);
                result = WireJson.Write(new PortablePackage(document.Name, document.Description, document.Tags, document.IsConfidential,
                    document.Items.Select((item, index) => new PortableItem(item.Id, item.Name, sources[index]) { Kind = item.Kind }).ToArray())
                    { Knowledge = document.Knowledge ?? [], Materials = document.Materials ?? [] });
                break;
            case "catalog-preview":
                result = WireJson.Write(PortableMaterialService.ApplyCatalog(WireJson.Read<PortableCatalogRequest>(request.Json)));
                break;
            case "metadata":
                var edit = WireJson.Read<PortableMetadataRequest>(request.Json);
                var loaded = ProjectPortableSerializer.Load(edit.Json);
                result = ProjectPortableSerializer.Save(edit.Name, edit.Description, edit.Tags.ToArray(), loaded.Document.IsConfidential,
                    loaded.Projects, loaded.Document.Knowledge, loaded.Document.Materials,
                    loaded.Document.Items.Where(item => item.Kind == "frame-fragment").Select(item => item.Id).ToHashSet());
                break;
            case "genre-table":
                var tableEdit = WireJson.Read<PortableGenreTableUpdate>(request.Json);
                result = ProjectPortableSerializer.UpdateGenreTable(tableEdit.Json, tableEdit.Table,
                    tableEdit.Handle, tableEdit.WorkDate, tableEdit.ChangeLog);
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
                    VenueAction = preview.Package.Materials.Any(item => item.Kind == "venue" && preview.Selection.Any(selected => selected.ItemId == item.Id)) ||
                        chosen.Length > 0 && FrameLayoutImportService.CanAdoptVenue(preview.Project) ? "入力元を採用" : "既存を保持",
                    DefinitionCount = chosen.Sum(item => item.Project.DeskLayouts[0].Definitions!.Types.Count),
                    RequestCount = chosen.Sum(item => item.Project.DeskLayouts[0].Definitions!.Requests.Count),
                    KnowledgeCount = candidate.ChannelKnowledge.Count - preview.Project.ChannelKnowledge.Count,
                    Selection = preview.Selection,
                    TypeMappings = mappings,
                    MaterialCount = preview.Package.Materials.Count(item => preview.Selection.Any(selected => selected.ItemId == item.Id)),
                    CandidatePreview = new(candidate.SchemaVersion, "preview", "適用後", candidate.Venue, candidate.DeskTypes, [], new([], []), [])
                    {
                        DeskLayouts = candidate.DeskLayouts.Where(layout => preview.Selection.Any(selected =>
                            selected.NewId == layout.Id || selected.TargetLayoutId == layout.Id)).ToArray(),
                        IsConfidential = candidate.IsConfidential,
                    },
                    Changes = preview.Selection.Select(selected => selected.Mode switch
                    {
                        _ when preview.Package.Materials.Any(material => material.Id == selected.ItemId &&
                            material.Kind is "genre-styles" or "block-styles") => $"対応表全体を置換：{selected.Name}（変更タグを引継ぎ）",
                        "replace" => $"配置案「{preview.Project.DeskLayouts.Single(layout => layout.Id == selected.TargetLayoutId).Name}」を「{selected.Name}」へ置換（参照サークル配置なし）",
                        "insert" => $"配置案「{preview.Project.DeskLayouts.Single(layout => layout.Id == selected.TargetLayoutId).Name}」へフレームを追加、移動量 ({selected.OffsetX}, {selected.OffsetY})。番号を保持し境界で自動接続する場合があります",
                        _ => $"追加／採用：{selected.Name}" + (selected.TargetLayoutId is not null && preview.Package.Materials.Any(material =>
                            material.Id == selected.ItemId && material.Definitions is not null)
                            ? $" → {preview.Project.DeskLayouts.Single(layout => layout.Id == selected.TargetLayoutId).Name} の定義" : ""),
                    }).ToArray(),
                });
                break;
            default: throw new ArgumentException("未対応のポータブル照会です。");
        }
        return new PortableReply { Json = result };
    });
}
