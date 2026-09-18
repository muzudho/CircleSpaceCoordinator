namespace CircleSpaceCoordinator.Desktop.Tests;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Infrastructure.Json;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.Desktop.Core.Interaction;

internal static partial class Program
{
    private static CircleSpaceProject PortableExample()
    {
        var catalog = SpaceDefinitionCatalog.CreateDefault();
        var type = SpaceTypeFactory.Create(catalog.Types[0]) with { Id = "physical" };
        return new("1.0", "portable-tests", "Fictional", new("venue", "Fictional hall", 20, 20, new HashSet<GridPosition>()),
            [type], [], new([], []), [])
        {
            DeskLayouts = [new("layout", "Original", [new("a", type.Id, new(1, 1), QuarterTurn.North), new("b", type.Id, new(4, 1), QuarterTurn.North)])
            {
                Definitions = catalog, SeatLabels = [new("a", new(0, 0), "A", "1"), new("b", new(0, 0), "B", "2")],
                IslandConnectors = [new("bridge", "a", "b")], IslandStarts = [new("a", new(0, 0), QuarterTurn.North)],
            }],
        };
    }

    private static void PortableMaterials()
    {
        var connection = EditorConnection.Current;
        var source = PortableExample() with { IsConfidential = true, Participants = CreateProject().Participants.Select(item => item with
            { Features = new Dictionary<string, double>(), SourceValues = new Dictionary<string, string> { ["Private"] = "NEVER EXPORT ME" } }).ToArray() };
        var definitions = source.DeskLayouts[0].Definitions!;
        var json = connection.ExportPortable(new(source, [], new([], []), "Materials", "Definitions and venue", ["library"], false)
        {
            Materials = [new("frame-definition", definitions.Types[0].Id, "layout"), new("request-definition", definitions.Requests[0].Id, "layout"), new("venue", source.Venue.Id)],
        });
        AssertEqual(true, json.Contains("\"formatVersion\": 2"));
        AssertEqual(false, json.Contains("NEVER EXPORT ME"));
        AssertEqual(false, json.Contains("participants"));
        var package = connection.ParsePortable(json);
        AssertEqual(3, package.Materials.Count);
        AssertEqual(1, package.Materials[1].Definitions!.Types.Count);
        var blank = source with { IsConfidential = false, Participants = [], DeskTypes = [],
            DeskLayouts = [new("target", "Target", []) { Definitions = new([], []) }],
            Venue = source.Venue with { Width = 2, Height = 2, Name = "Before" } };
        using var target = connection.Open(ProjectJsonSerializer.Save(blank));
        var before = ProjectJsonSerializer.Save(target.Project);
        var selection = package.Materials.Select((material, index) => new PortableImportItem(material.Id, "material-" + index, material.Name)
            { TargetLayoutId = "target" }).ToArray();
        var preview = connection.PreviewPortable(new(target.Project, package, selection));
        AssertEqual(3, preview.MaterialCount);
        AssertEqual(0, preview.CandidatePreview!.Participants.Count);
        target.Execute(new ImportPortableSelection(package, selection), selectedPlanEdit: false);
        AssertEqual(1, target.Project.DeskLayouts.Count);
        AssertEqual(1, target.Project.DeskLayouts[0].Definitions!.Types.Count);
        AssertEqual(1, target.Project.DeskLayouts[0].Definitions!.Requests.Count);
        AssertEqual(20, target.Project.Venue.Width);
        AssertEqual(true, target.Project.IsConfidential);
        var saved = ProjectJsonSerializer.Save(target.Project);
        target.Undo(); AssertEqual(before, ProjectJsonSerializer.Save(target.Project));
        target.Redo(); AssertEqual(saved, ProjectJsonSerializer.Save(target.Project));
        var modifiedType = definitions.Types[0] with { Width = 3, Cells = [new(0, 0, 1), new(1, 0, 1), new(2, 0, 1)] };
        var conflicting = package with { Materials = [package.Materials[1] with { Definitions = new([modifiedType], [definitions.Requests[0]]) }] };
        var revision = target.Revision;
        RejectPortable(() => target.Execute(new ImportPortableSelection(conflicting, [new(conflicting.Materials[0].Id, "conflict", "1") { TargetLayoutId = "target" }]), selectedPlanEdit: false));
        AssertEqual(revision, target.Revision); AssertEqual(saved, ProjectJsonSerializer.Save(target.Project));
        target.Execute(new ImportPortableSelection(conflicting, [new(conflicting.Materials[0].Id, "copy", "1B") { TargetLayoutId = "target", RequestValue = "1B" }]), selectedPlanEdit: false);
        AssertEqual(2, target.Project.DeskLayouts[0].Definitions!.Types.Count);
        AssertEqual(2, target.Project.DeskLayouts[0].Definitions!.Requests.Count);
        var newRequest = target.Project.DeskLayouts[0].Definitions!.Requests.Single(item => item.Value == "1B");
        AssertEqual(true, newRequest.Targets[0].TypeId != definitions.Types[0].Id);
        target.Undo(); AssertEqual(saved, ProjectJsonSerializer.Save(target.Project));
        var badSelection = selection.Select((item, index) => index == 1 ? item with { TargetLayoutId = "missing" } : item).ToArray();
        RejectPortable(() => target.Execute(new ImportPortableSelection(package, badSelection), selectedPlanEdit: false));
        AssertEqual(saved, ProjectJsonSerializer.Save(target.Project));
        using var placed = connection.Open(ProjectJsonSerializer.Save(PortableExample()));
        var differentVenue = package with { Materials = [package.Materials[2] with { Venue = package.Materials[2].Venue! with { Width = 30 } }] };
        RejectPortable(() => placed.Execute(new ImportPortableSelection(differentVenue, [new(differentVenue.Materials[0].Id, "venue", "Different")]), selectedPlanEdit: false));
        RejectPortable(() => connection.ParsePortable(json.Replace("\"formatVersion\": 2", "\"formatVersion\": 1")));
    }

    private static void PortableFragments()
    {
        var connection = EditorConnection.Current;
        var source = PortableExample();
        var json = connection.ExportPortable(new(source, ["layout"], new([], []), "Fragment", "One frame", [], false)
            { Fragments = new Dictionary<string, IReadOnlyList<string>> { ["layout"] = ["a"] } });
        var package = connection.ParsePortable(json);
        AssertEqual("frame-fragment", package.Items[0].Kind);
        var fragment = package.Items[0].Project.DeskLayouts[0];
        AssertEqual(1, fragment.DeskPlacements.Count);
        AssertEqual(0, fragment.IslandConnectors.Count);
        AssertEqual(1, fragment.IslandStarts.Count);
        AssertEqual(1, fragment.SeatLabels.Count);
        AssertEqual(0, fragment.Definitions!.Requests.Count);
        using var target = connection.Open(ProjectJsonSerializer.Save(source));
        var before = ProjectJsonSerializer.Save(target.Project);
        var insert = new PortableImportItem("layout", "insert", "Fragment") { Mode = "insert", TargetLayoutId = "layout", OffsetY = 4 };
        var preview = connection.PreviewPortable(new(target.Project, package, [insert]));
        AssertEqual(3, preview.CandidatePreview!.DeskLayouts[0].DeskPlacements.Count);
        target.Execute(new ImportPortableSelection(package, [insert]), selectedPlanEdit: false);
        var merged = target.Project.DeskLayouts.Single();
        AssertEqual(3, merged.DeskPlacements.Count);
        AssertEqual(2, merged.IslandStarts.Count);
        var newFrame = merged.DeskPlacements.Single(item => item.Id != "a" && item.Id != "b");
        AssertEqual(new GridPosition(1, 5), newFrame.Anchor);
        AssertEqual(newFrame.Id, merged.IslandStarts.Last().DeskPlacementId);
        AssertEqual(newFrame.Id, merged.SeatLabels.Last().DeskPlacementId);
        var saved = ProjectJsonSerializer.Save(target.Project);
        target.Undo(); AssertEqual(before, ProjectJsonSerializer.Save(target.Project));
        target.Redo(); AssertEqual(saved, ProjectJsonSerializer.Save(target.Project));
        target.Undo();
        var revision = target.Revision;
        RejectPortable(() => target.Execute(new ImportPortableSelection(package, [insert with { OffsetY = 0 }]), selectedPlanEdit: false));
        AssertEqual(revision, target.Revision); AssertEqual(before, ProjectJsonSerializer.Save(target.Project));
        var replace = insert with { Mode = "replace", OffsetY = 0, Name = "Replaced" };
        var duplicate = package.Items[0] with { Id = "second" };
        RejectPortable(() => target.Execute(new ImportPortableSelection(package with { Items = [package.Items[0], duplicate] },
            [replace, replace with { ItemId = "second", NewId = "second" }]), selectedPlanEdit: false));
        AssertEqual(revision, target.Revision); AssertEqual(before, ProjectJsonSerializer.Save(target.Project));
        target.Execute(new ImportPortableSelection(package, [replace]), selectedPlanEdit: false);
        AssertEqual("layout", target.Project.DeskLayouts[0].Id);
        AssertEqual(1, target.Project.DeskLayouts[0].DeskPlacements.Count);
        AssertEqual("Replaced", target.Project.DeskLayouts[0].Name);
        target.Undo(); AssertEqual(before, ProjectJsonSerializer.Save(target.Project));
        target.Execute(new LayoutCatalogServiceCreateCircleLayout("dependent", "Dependent", "layout"), selectedPlanEdit: false);
        RejectPortable(() => target.Execute(new ImportPortableSelection(package, [replace]), selectedPlanEdit: false));
        AssertEqual(2, target.Project.DeskLayouts[0].DeskPlacements.Count);
    }

    private static void PortableLibraryAndCatalog()
    {
        var connection = EditorConnection.Current;
        var source = PortableExample() with { IsConfidential = true };
        var json = connection.ExportPortable(new(source, [], new([], []), "Searchable", "Original memo", ["tag-one"], true)
            { Materials = [new("frame-definition", source.DeskLayouts[0].Definitions!.Types[0].Id, "layout")] });
        var package = connection.ParsePortable(json);
        var directory = Path.Combine(Path.GetTempPath(), "portable-library-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "sample.package-csc.json");
            File.WriteAllText(path, json);
            File.WriteAllText(Path.Combine(directory, "legacy.project-portable.json"), json);
            File.WriteAllText(Path.Combine(directory, "legacy.frame-layout.json"), FrameLayoutPortableService.Export(source, "layout", source.DeskLayouts[0].Definitions!));
            ProjectFileService.Save(Path.Combine(directory, "sample.event-project-csc.json"), source);
            File.WriteAllText(Path.Combine(directory, "bad.project-portable.json"), "{}");
            var entries = PortableLibraryService.Read(directory, connection.ParsePortable);
            AssertEqual(4, entries.Count);
            AssertEqual(1, entries.Count(item => item.Error is not null));
            var entry = entries.Single(item => item.Path == path);
            AssertEqual(true, entry.Matches("TAG-ONE"));
            AssertEqual(true, entry.Matches("Original memo"));
            var edited = connection.UpdatePortableMetadata(new(entry.Json, "New name", "New memo", ["tag-two"]));
            PortableLibraryService.SaveMetadata(entry, edited);
            AssertEqual("New memo", connection.ParsePortable(File.ReadAllText(path)).Description);
            AssertEqual(true, connection.ParsePortable(File.ReadAllText(path)).IsConfidential);
            RejectPortable(() => PortableLibraryService.SaveMetadata(entry, json));
            AssertEqual(edited, File.ReadAllText(path));
            var store = new SpaceDefinitionStore(Path.Combine(directory, "catalog.json"));
            store.Save(new([], []));
            var before = File.ReadAllText(store.Path);
            var catalog = connection.PreviewPortableCatalog(new(store.Current, package, [new(package.Materials[0].Id, "common", package.Materials[0].Name)]));
            store.ImportPortable(catalog);
            AssertEqual(1, store.Current.Types.Count);
            AssertEqual(true, store.Current.IsConfidential);
            AssertEqual(2, store.Current.SchemaVersion);
            AssertEqual(true, store.CanUndoPortableImport);
            store.UndoPortableImport();
            AssertEqual(before, File.ReadAllText(store.Path));
            AssertEqual(false, store.CanUndoPortableImport);
            store.ImportPortable(catalog);
            File.WriteAllText(store.Path, before);
            RejectPortable(store.UndoPortableImport);
            AssertEqual(before, File.ReadAllText(store.Path));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void RejectPortable(Action action)
    {
        var rejected = false;
        try { action(); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException or CircleSpaceCoordinator.Core.Validation.ProjectValidationException)
        { rejected = true; }
        AssertEqual(true, rejected);
    }
}
