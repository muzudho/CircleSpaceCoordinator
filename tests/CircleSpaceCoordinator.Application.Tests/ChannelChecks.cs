namespace CircleSpaceCoordinator.Application.Tests;

using CircleSpaceCoordinator.Application.Participants;
using CircleSpaceCoordinator.Application.Workspace;
using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Engine.Model;

internal static class ChannelChecks
{
    public static void Run()
    {
        var project = new CircleSpaceProject("1.0", "channels", "Channels",
            new Venue("venue", "Venue", 8, 4, new HashSet<GridPosition>()),
            [new DeskType("desk", "Desk", [new(0, 0), new(1, 0)])],
            [new Participant("a", "A", 1, new Dictionary<string, double>()),
             new Participant("b", "B", 1, new Dictionary<string, double>())],
            new EvaluationConfiguration([], []),
            [new Plan("plan", "Plan", [new DeskPlacement("d", "desk", new(0, 0), QuarterTurn.North)],
                [new ParticipantAssignment("a", new HashSet<GridPosition> { new(0, 0) }, new(0, 0)),
                 new ParticipantAssignment("b", new HashSet<GridPosition> { new(1, 0) }, new(1, 0))])]);
        var workspace = new ProjectWorkspace(project);
        void Execute(EditorOperation operation) => workspace.Execute(WireJson.Read<EditorOperation>(WireJson.Write(operation)));
        void Equal(double expected, double actual)
        {
            if (Math.Abs(expected - actual) > 1e-10) throw new Exception($"Expected {expected}, got {actual}.");
        }
        void Reject(EditorOperation operation)
        {
            var before = WireJson.Write(workspace.Project);
            try { Execute(operation); throw new Exception("Invalid channel edit accepted."); }
            catch (ArgumentException) { }
            if (before != WireJson.Write(workspace.Project)) throw new Exception("Rejected edit mutated project.");
        }
        ParticipantImportRow Row(string id, string books, string other) => new(id, id, 1)
        { SourceValues = new Dictionary<string, string> { ["書籍の有無"] = books, ["その他"] = other } };
        Execute(new ParticipantCatalogServiceReplaceParticipants([Row("a", "1", "0.5"), Row("b", "0", "1")]));
        Execute(new UpsertChannel("books", "書籍", "書籍の有無"));
        Execute(new UpsertChannel("other", "その他", "その他"));
        Execute(new SetChannelWeights("plan", "books", [new(0, 0)], 1));
        Execute(new SetChannelWeights("plan", "other", [new(0, 0), new(1, 0)], 0.5));
        var evaluation = workspace.GetSelectedPlanSnapshot().Evaluation;
        Equal(1, evaluation.Features.Single(item => item.FeatureId == "books").WeightedScore);
        Equal(0.75, evaluation.Features.Single(item => item.FeatureId == "other").WeightedScore);
        Execute(new UpsertChannel("books", "書籍", "書籍の有無") { OverallWeight = 10 });
        Equal(10.75, workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore);
        Execute(new UpsertChannel("other", "その他", "その他") { OverallWeight = 0.001 });
        Equal(10.00075, workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore);
        workspace.Undo(); Equal(10.75, workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore);
        workspace.Redo(); Equal(10.00075, workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore);
        workspace.Undo(); workspace.Undo();
        Equal(1.75, workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore);
        Equal(1.75, evaluation.TotalScore);
        workspace.Undo(); Equal(1, workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore);
        workspace.Redo(); Equal(1.75, workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore);
        Execute(new SetChannelWeights("plan", "books", [new(0, 0)], -1));
        Equal(-1, workspace.GetSelectedPlanSnapshot().Evaluation.Features.Single(item => item.FeatureId == "books").WeightedScore);
        Equal(-0.25, workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore);
        workspace.Undo(); Equal(1.75, workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore);
        workspace.Redo(); Equal(-0.25, workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore);
        workspace.Undo();
        Reject(new SetChannelWeights("plan", "books", [new(0, 0)], -1.001));
        Reject(new SetChannelWeights("plan", "books", [new(0, 0)], 1.01));
        Reject(new SetChannelWeights("plan", "books", [new(7, 3)], 1));
        Reject(new UpsertChannel("address", "番地", null));
        Reject(new UpsertChannel("duplicate", "書籍", null));
        Reject(new UpsertChannel("books", "書籍", "missing"));
        Reject(new ParticipantCatalogServiceReplaceParticipants([Row("a", "text", "0"), Row("b", "0", "0")]));
        Reject(new ParticipantCatalogServiceReplaceParticipants([Row("a", "NaN", "0"), Row("b", "0", "0")]));
        Execute(new UpsertChannel("books", "書籍", "その他"));
        Equal(1.25, workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore);
        Execute(new UpsertChannel("books", "書籍", null));
        Equal(0.75, workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore);
        Execute(new ParticipantCatalogServiceReplaceParticipants([Row("b", "1", ""), Row("a", "0", "1")]));
        Equal(0.5, workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore);
        Execute(new RemoveChannel("other"));
        Equal(0, workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore);
        workspace.Undo(); Equal(0.5, workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore);
        Execute(new LayoutCatalogServiceCreateDeskLayout("empty", "Unused desk layout"));
        workspace.SelectDeskLayout("empty");
        Execute(new PlanDeskEditorAddDesk(workspace.SelectedPlanId, new DeskPlacement("unused", "desk", new(2, 0), QuarterTurn.North)));
        Execute(new SetChannelWeights(workspace.SelectedPlanId, "books", [new(2, 0)], 0.25));
        Equal(0.25, workspace.Project.Evaluation.WeightMaps.Single(item => item.FeatureId == "books").GetWeight(new(2, 0)));
        Equal(1, workspace.Project.CircleLayouts.Count);
        Execute(new UpsertChannel("books", "書籍", "書籍の有無"));
        var newSource = new ParticipantTableSource("changed.csv", "CSV", ["新しい列"], ["新しい列"]);
        Execute(new ParticipantCatalogServiceReplaceParticipants(
            [new ParticipantImportRow("a", "a", 1) { SourceValues = new Dictionary<string, string> { ["新しい列"] = "2" } },
             new ParticipantImportRow("b", "b", 1) { SourceValues = new Dictionary<string, string> { ["新しい列"] = "3" } }], newSource));
        if (workspace.Project.Evaluation.Features.Any(item => (item.Id == "books" || item.Id == "other") && item.SourceColumn is not null))
            throw new Exception("Removed import columns kept their old channel mappings.");
        Execute(new UpsertChannel("other", "その他", "新しい列"));
        Equal(2, workspace.Project.Participants.Single(item => item.CircleId == "a").Features["other"]);
        CheckParticipantFieldColumns(project);
        CheckPlaceableCells(project);
    }

    private static void CheckParticipantFieldColumns(CircleSpaceProject original)
    {
        var keys = new[] { "ID", "名称", "別名", "セル数" };
        var source = new ParticipantTableSource("sample.csv", "CSV", keys, keys)
        { FieldColumns = new ParticipantFieldColumns("ID", "名称", "セル数", null, null) };
        var workspace = new ProjectWorkspace(original);
        void Execute(EditorOperation operation) => workspace.Execute(WireJson.Read<EditorOperation>(WireJson.Write(operation)));
        Execute(new ParticipantCatalogServiceReplaceParticipants(
            [new ParticipantImportRow("a", "A", 1) { SourceValues = new Dictionary<string, string>
                { ["ID"] = "a", ["名称"] = "A", ["別名"] = "Alias A", ["セル数"] = "1" } },
             new ParticipantImportRow("b", "B", 1) { SourceValues = new Dictionary<string, string>
                { ["ID"] = "b", ["名称"] = "B", ["別名"] = "Alias B", ["セル数"] = "1" } }], source));
        Execute(new SetParticipantFieldColumns(source.FieldColumns! with { DisplayName = "別名" }));
        if (workspace.Project.Participants.Single(item => item.CircleId == "a").DisplayName != "Alias A" ||
            workspace.Project.ParticipantTableSource?.FieldColumns?.DisplayName != "別名")
            throw new Exception("A default channel did not follow its remapped column.");
        Execute(new SetParticipantMappingOrder(["field:displayName", "column:ID", "field:circleId"]));
        if (workspace.Project.ParticipantTableSource?.MappingOrder?.FirstOrDefault() != "field:displayName")
            throw new Exception("Mapping row order was not saved.");
        var restored = WireJson.Read<CircleSpaceProject>(WireJson.Write(workspace.Project));
        if (restored.ParticipantTableSource?.FieldColumns?.DisplayName != "別名" ||
            restored.ParticipantTableSource.MappingOrder?.FirstOrDefault() != "field:displayName")
            throw new Exception("Mapping metadata did not survive project serialization.");
        workspace.Undo();
        if (workspace.Project.ParticipantTableSource?.MappingOrder is not null)
            throw new Exception("Undo did not restore the mapping order.");
        try
        {
            Execute(new SetParticipantFieldColumns(source.FieldColumns! with { RequiredCellCount = "別名" }));
            throw new Exception("Invalid default channel mapping was accepted.");
        }
        catch (ArgumentException) { }
        if (workspace.Project.ParticipantTableSource?.FieldColumns?.DisplayName != "別名")
            throw new Exception("Rejected mapping modified the event.");
    }

    private static void CheckPlaceableCells(CircleSpaceProject original)
    {
        var type = new DeskType("three", "Three cells", [new(0, 0), new(1, 0), new(2, 0)])
        {
            Space = new SpaceTypeDetails("three", "frame", 3, 1,
                [new(0, 0, 1), new(1, 0, 0), new(2, 0, 2)], []),
        };
        foreach (var orientation in Enum.GetValues<QuarterTurn>())
        {
            var desk = new DeskPlacement("three", type.Id, new(3, 2), orientation);
            var plan = new Plan("plan", "Plan", [desk], []);
            var project = CircleSpaceCoordinator.Application.Editing.ChannelEditor.Upsert(
                original with { DeskTypes = [type], Plans = [plan] }, "weight", "重み", null);
            var placeable = desk.GetSeatCells(type).ToArray();
            var updated = CircleSpaceCoordinator.Application.Editing.ChannelEditor.SetWeights(project, plan.Id, "weight", placeable, 0.375);
            var map = updated.Evaluation.WeightMaps.Single(item => item.FeatureId == "weight");
            if (placeable.Any(cell => map.GetWeight(cell) != 0.375)) throw new Exception("Placeable cells were not updated.");
            var blocked = desk.Anchor + new GridPosition(1, 0).Rotate(orientation);
            if (map.Cells.ContainsKey(blocked)) throw new Exception("Non-placeable cell received a weight.");
            foreach (var targets in new[] { new[] { blocked }, new[] { placeable[0], blocked } })
            {
                var before = WireJson.Write(updated);
                try
                {
                    CircleSpaceCoordinator.Application.Editing.ChannelEditor.SetWeights(updated, plan.Id, "weight", targets, 1);
                    throw new Exception("Non-placeable cell was accepted.");
                }
                catch (ArgumentException) { }
                if (before != WireJson.Write(updated)) throw new Exception("Rejected range modified weights.");
            }
        }
    }
}
