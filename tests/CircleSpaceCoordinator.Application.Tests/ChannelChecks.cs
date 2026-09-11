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
        Equal(1.75, evaluation.TotalScore);
        workspace.Undo(); Equal(1, workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore);
        workspace.Redo(); Equal(1.75, workspace.GetSelectedPlanSnapshot().Evaluation.TotalScore);
        Reject(new SetChannelWeights("plan", "books", [new(0, 0)], -0.1));
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
    }
}
