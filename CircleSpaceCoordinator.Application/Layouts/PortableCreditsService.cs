namespace CircleSpaceCoordinator.Application.Layouts;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Engine.Model;

public static class PortableCreditsService
{
    public static CircleSpaceProject Record(CircleSpaceProject project, PortablePackage package,
        IReadOnlyList<PortableMaterialSelection> selections, SpaceDefinitionCatalog fallback)
    {
        project = project with { DeskLayouts = project.DeskLayouts.Select(layout => layout.Definitions is null &&
            (package.Items.Any(item => item.Id == layout.Id) || selections.Any(item => item.LayoutId == layout.Id))
            ? layout with { Definitions = fallback } : layout).ToArray() };
        SpaceDefinitionCatalog MergeCredits(SpaceDefinitionCatalog target, SpaceDefinitionCatalog incoming) => target with
        {
            Types = target.Types.Select(item => incoming.Types.FirstOrDefault(source => source.Id == item.Id) is { } source
                ? item with { Credits = source.Credits } : item).ToArray(),
            Requests = target.Requests.Select(item => incoming.Requests.FirstOrDefault(source => source.Id == item.Id) is { } source
                ? item with { Credits = source.Credits } : item).ToArray(),
        };
        foreach (var item in package.Items)
        {
            var source = item.Project.DeskLayouts.Single();
            project = project with { Venue = project.Venue with { Credits = item.Project.Venue.Credits },
                DeskLayouts = project.DeskLayouts.Select(layout => layout.Id == item.Id ? layout with
                { Credits = source.Credits,
                    DeskPlacements = layout.DeskPlacements.Select(desk => source.DeskPlacements.FirstOrDefault(item => item.Id == desk.Id) is { } provided
                        ? desk with { Credits = provided.Credits } : desk).ToArray(),
                    Definitions = layout.Definitions is null ? source.Definitions
                    : MergeCredits(layout.Definitions, source.Definitions!) } : layout).ToArray() };
        }
        foreach (var selection in selections)
        {
            var id = selection.Kind == "venue" ? "venue:" + selection.SourceId
                : selection.Kind + ":" + (selection.LayoutId ?? "common") + ":" + selection.SourceId;
            var source = package.Materials.Single(item => item.Id == id);
            if (source.Kind == "venue") project = project with { Venue = project.Venue with { Credits = source.Credits } };
            else project = project with { DeskLayouts = project.DeskLayouts.Select(layout => layout.Id == selection.LayoutId
                ? layout with { Definitions = layout.Definitions is null ? source.Definitions : MergeCredits(layout.Definitions, source.Definitions!) }
                : layout).ToArray() };
        }
        return project with { ChannelKnowledge = project.ChannelKnowledge.Select(item =>
            package.Knowledge.FirstOrDefault(source => source.Id == item.Id) is { } source ? item with { Credits = source.Credits } : item).ToArray() };
    }

    public static SpaceDefinitionCatalog Provide(SpaceDefinitionCatalog catalog, string worker) => catalog with
    {
        Types = catalog.Types.Select(item => item with { Credits = Provide(item.Credits, worker) }).ToArray(),
        Requests = catalog.Requests.Select(item => item with { Credits = Provide(item.Credits, worker) }).ToArray(),
    };

    public static PersonCredits Provide(PersonCredits? credits, string worker) =>
        (credits ?? new()).ProvidedBy(worker);

    public static CircleSpaceProject Provide(CircleSpaceProject project, string worker) => project with
    {
        Venue = project.Venue with { Credits = Provide(project.Venue.Credits, worker) },
        DeskLayouts = project.DeskLayouts.Select(layout => layout with
        {
            Credits = Provide(layout.Credits, worker),
            DeskPlacements = layout.DeskPlacements.Select(desk => desk with { Credits = Provide(desk.Credits, worker) }).ToArray(),
            Definitions = layout.Definitions is null ? null : Provide(layout.Definitions, worker),
        }).ToArray(),
    };

    public static PortableMaterial Provide(PortableMaterial material, string worker) => material with
    {
        Credits = Provide(material.Credits, worker),
        Definitions = material.Definitions is null ? null : Provide(material.Definitions, worker),
    };
}
