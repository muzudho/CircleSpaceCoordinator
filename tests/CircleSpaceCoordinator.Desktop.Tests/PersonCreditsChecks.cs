namespace CircleSpaceCoordinator.Desktop.Tests;

using CircleSpaceCoordinator.Application.Layouts;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Infrastructure.Json;

internal static partial class Program
{
    private static void PersonCreditsRoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), "csc-worker-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var settings = CreateIsolatedSettings(path);
            settings.SaveHandle(string.Concat(Enumerable.Repeat("あ😀", 8)));
            AssertEqual(settings.Current.Handle, CreateIsolatedSettings(path).Current.Handle);
            var rejected = false;
            try { settings.SaveHandle(new string('a', 17)); } catch (ArgumentException) { rejected = true; }
            AssertEqual(true, rejected);
            settings.SaveHandle(string.Concat(Enumerable.Repeat("😀", 16)));
            AssertEqual(64, System.Text.Encoding.UTF8.GetByteCount(settings.Current.Handle));
            RejectPortable(() => settings.SaveHandle(string.Concat(Enumerable.Repeat("😀", 17))));
            RejectPortable(() => settings.SaveHandle(new string('\ud800', 1)));
            RejectPortable(() => settings.SaveHandle(string.Concat(Enumerable.Repeat("e\u0301", 9))));
            File.WriteAllText(path, "{\"workerName\":\"legacy\"}");
            AssertEqual("legacy", CreateIsolatedSettings(path).Current.Handle);
        }
        finally { File.Delete(path); }

        var firstDate = new DateOnly(2026, 9, 19);
        var dated = new PersonCredits().WrittenBy("A", firstDate);
        AssertEqual("by A since 2026-09-19", dated.AttributionText);
        AssertEqual(dated, dated.WrittenBy("A", firstDate));
        AssertEqual(firstDate.AddDays(1), dated.WrittenBy("A", firstDate.AddDays(1)).ModifiedOn!.Value);
        AssertEqual(firstDate, dated.ProvidedBy("B").ModifiedOn!.Value);
        AssertEqual("by A since 2026-09-19", dated.ProvidedBy("B").AttributionText);

        var source = PortableExample() with { Evaluation = new([new("feature", "Rule", 1, 0, 1)], [new("feature", 0, new Dictionary<CircleSpaceCoordinator.Core.Geometry.GridPosition, double>())]) };
        using var workspace = EditorConnection.Current.Open(ProjectJsonSerializer.Save(source));
        var rule = new ChannelInputRule("Column", "Meaning", "1=yes", true, [0, 1]);
        workspace.Execute(new CaptureChannelKnowledge("feature", "knowledge", "Original", "Purpose", rule, false) { Handle = "A" }, selectedPlanEdit: false);
        var original = workspace.Project.ChannelKnowledge.Single();
        AssertEqual("A", original.Credits!.Modifier!);
        workspace.Execute(new UpdateChannelKnowledge(original with { Description = "Edited" }, "B"), selectedPlanEdit: false);
        var edited = workspace.Project.ChannelKnowledge.Single();
        AssertEqual<string?>(null, edited.Credits!.Author);
        AssertEqual("B", edited.Credits.Modifier!);
        workspace.Execute(new UpdateChannelKnowledge(edited with { Purpose = "Again" }, "B"), selectedPlanEdit: false);
        AssertEqual(edited.Credits, workspace.Project.ChannelKnowledge.Single().Credits);
        var sourceWithKnowledge = ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(workspace.Project));
        var definitions = sourceWithKnowledge.DeskLayouts.Single().Definitions!;
        PortableMaterialSelection[] materials = [new("venue", source.Venue.Id), new("request-definition", definitions.Requests[0].Id, "layout")];
        var package = EditorConnection.Current.ParsePortable(EditorConnection.Current.ExportPortable(
            new(sourceWithKnowledge, ["layout"], definitions, "Credits", "", [], false)
            { Handle = "A", KnowledgeIds = ["knowledge"], Materials = materials }));
        workspace.Execute(new RecordPortableProviders(package, materials, definitions), selectedPlanEdit: false);
        AssertEqual("A", workspace.Project.DeskLayouts.Single().Credits!.FirstProvider!);
        var target = PortableExample() with { DeskTypes = [], DeskLayouts = [], Evaluation = new([], []) };
        var selection = package.Items.Select(item => new PortableImportItem(item.Id, "received", "Received"))
            .Concat(package.Knowledge.Select(item => new PortableImportItem("knowledge:" + item.Id, "received-knowledge", "Received rule")))
            .Concat(package.Materials.Select(item => new PortableImportItem(item.Id, "received-material-" + item.Kind, item.Name) { TargetLayoutId = "received", RequestValue = "received request" })).ToArray();
        var imported = PortableSelectionService.Apply(target, package, selection);
        imported = ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(imported));
        var next = EditorConnection.Current.ParsePortable(EditorConnection.Current.ExportPortable(
            new(imported, ["received"], definitions, "Again", "", [], false)
            { Handle = "B", KnowledgeIds = ["received-knowledge"], Materials = [new("venue", imported.Venue.Id)] }));
        AssertEqual("A", next.Items.Single().Project.DeskLayouts.Single().Credits!.FirstProvider!);
        AssertEqual("B", next.Items.Single().Project.DeskLayouts.Single().Credits!.LatestProvider!);
        AssertEqual("A", next.Knowledge.Single().Credits!.FirstProvider!);
        AssertEqual("B", next.Knowledge.Single().Credits!.LatestProvider!);
        AssertEqual("A", next.Materials.Single().Credits!.FirstProvider!);
        AssertEqual("B", next.Materials.Single().Credits!.LatestProvider!);
        AssertEqual("A", next.Items.Single().Project.DeskLayouts.Single().Definitions!.Requests[0].Credits!.FirstProvider!);
        AssertEqual("B", next.Items.Single().Project.DeskLayouts.Single().Definitions!.Types[0].Credits!.LatestProvider!);
        AssertEqual("A", next.Items.Single().Project.DeskLayouts.Single().DeskPlacements[0].Credits!.FirstProvider!);
        var catalog = PortableMaterialService.ApplyCatalog(new(new([], []), package,
            [new(package.Materials.Single(item => item.Kind == "request-definition").Id, "catalog", "Request")]));
        AssertEqual("A", catalog.Requests.Single().Credits!.FirstProvider!);
        AssertEqual("A", catalog.Types.Single().Credits!.FirstProvider!);
        var old = imported with { ChannelKnowledge = [imported.ChannelKnowledge.Single() with { Credits = null }] };
        var updatedOld = ChannelKnowledgeService.Update(old, old.ChannelKnowledge.Single(), "B");
        AssertEqual<string?>(null, updatedOld.ChannelKnowledge.Single().Credits!.Author);
        AssertEqual("B", updatedOld.ChannelKnowledge.Single().Credits!.Modifier!);
        var invalid = source with { Venue = source.Venue with { Credits = new(FirstProvider: new string('x', 17)) } };
        RejectPortable(() => ProjectJsonSerializer.Save(invalid));

        workspace.HandleProvider = () => "editor-A";
        workspace.Execute(new LayoutCatalogServiceCreateCircleLayout("genre", "Genre", "layout"), selectedPlanEdit: false);
        var circleCredits = workspace.Project.CircleLayouts.Single().Credits!;
        AssertEqual("editor-A", circleCredits.Modifier!);
        AssertEqual(DateOnly.FromDateTime(DateTime.Now), circleCredits.ModifiedOn!.Value);
        workspace.HandleProvider = () => "editor-B";
        workspace.Execute(new LayoutCatalogServiceRenameCircleLayout("genre", "Renamed"), selectedPlanEdit: false);
        AssertEqual("editor-B", workspace.Project.CircleLayouts.Single().Credits!.Modifier!);
        workspace.Undo();
        AssertEqual(circleCredits, workspace.Project.CircleLayouts.Single().Credits!);
        workspace.Redo();
        var roundTrip = ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(workspace.Project));
        AssertEqual("editor-B", roundTrip.CircleLayouts.Single().Credits!.Modifier!);
        AssertEqual(circleCredits.ModifiedOn, roundTrip.CircleLayouts.Single().Credits!.ModifiedOn);

        var workDate = new DateOnly(2001, 2, 3); // Deliberately different from the machine date.
        workspace.WorkDateProvider = () => workDate;
        GenreStyleDefinition[] genreStyles = [new("G", "red", "white", "solid")];
        workspace.Execute(new SetGenreStyles(genreStyles), selectedPlanEdit: false);
        var previousMappingCredits = workspace.Project.GenreStyleCredits!;
        AssertEqual("editor-B", previousMappingCredits.Modifier!);
        AssertEqual(workDate, previousMappingCredits.ModifiedOn!.Value);
        workspace.HandleProvider = () => "editor-C";
        workspace.WorkDateProvider = () => workDate.AddDays(1);
        workspace.Execute(new SetGenreStyles(genreStyles.ToArray()), selectedPlanEdit: false);
        AssertEqual(previousMappingCredits, workspace.Project.GenreStyleCredits);
        workspace.Execute(new SetGenreStyles([genreStyles[0] with { PrimaryColor = "blue" }]), selectedPlanEdit: false);
        AssertEqual("editor-C", workspace.Project.GenreStyleCredits!.Modifier!);
        AssertEqual(workDate.AddDays(1), workspace.Project.GenreStyleCredits!.ModifiedOn!.Value);
        workspace.Undo();
        AssertEqual(previousMappingCredits, workspace.Project.GenreStyleCredits);
        workspace.Redo();
        AssertEqual(workspace.Project.GenreStyleCredits,
            ProjectJsonSerializer.Load(ProjectJsonSerializer.Save(workspace.Project)).GenreStyleCredits);

        var mapping = new CircleSpaceCoordinator.Desktop.Core.Interaction.StyleMappingDraft(["G"], [new("G", "red", "white", "solid")]);
        var oldCredits = new PersonCredits(Modifier: "一郎") { ModifiedOn = new(2026, 9, 18) };
        var today = new DateOnly(2026, 9, 19);
        AssertEqual(false, mapping.HasChanges);
        AssertEqual("by 一郎 since 2026-09-18", mapping.AttributionSummary(oldCredits, "二郎", today));
        mapping.SetColor(0, true, "blue");
        AssertEqual(true, mapping.HasChanges);
        AssertEqual("by 二郎 since 2026-09-19（旧： 一郎 since 2026-09-18）", mapping.AttributionSummary(oldCredits, "二郎", today));
        mapping.SetColor(0, true, "red");
        AssertEqual(false, mapping.HasChanges);
        AssertEqual("by 一郎 since 2026-09-18", mapping.AttributionSummary(oldCredits, "二郎", today));
        var opening = mapping.Build();
        mapping.SetColor(0, true, "blue");
        mapping.SetPattern(0, "grid");
        mapping.RestoreOpeningSnapshot();
        AssertEqual(false, mapping.HasChanges);
        AssertEqual(true, opening.SequenceEqual(mapping.Build()));
        AssertEqual("by 一郎 since 2026-09-18", mapping.AttributionSummary(oldCredits, "二郎", today));
    }
}
