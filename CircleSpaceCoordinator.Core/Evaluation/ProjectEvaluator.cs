namespace CircleSpaceCoordinator.Core.Evaluation;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;

public static class ProjectEvaluator
{
    public static IReadOnlyList<EvaluationResult> Evaluate(CircleSpaceProject project)
    {
        var issues = ProjectValidator.Validate(project);
        if (issues.Count > 0)
            throw new ProjectValidationException(issues);

        var participants = project.Participants.ToDictionary(item => item.Id);
        var weightMaps = project.Evaluation.WeightMaps.ToDictionary(item => item.FeatureId);

        return project.Plans.Select(plan => EvaluatePlan(plan, project.Evaluation, participants, weightMaps)).ToArray();
    }

    private static EvaluationResult EvaluatePlan(
        Plan plan,
        EvaluationConfiguration configuration,
        IReadOnlyDictionary<string, Participant> participants,
        IReadOnlyDictionary<string, WeightMap> weightMaps)
    {
        var featureResults = new List<FeatureEvaluationResult>(configuration.Features.Count);
        foreach (var feature in configuration.Features)
        {
            var weightMap = weightMaps[feature.Id];
            var rawSum = plan.Assignments.Sum(assignment =>
                participants[assignment.ParticipantId].GetFeatureValue(feature.Id) *
                weightMap.GetWeight(assignment.ScoringPosition));
            var adjustedScore = rawSum * feature.Scale + feature.Offset;
            var weightedScore = adjustedScore * feature.OverallWeight;
            featureResults.Add(new FeatureEvaluationResult(feature.Id, rawSum, adjustedScore, weightedScore));
        }

        return new EvaluationResult(plan.Id, featureResults.Sum(item => item.WeightedScore), featureResults);
    }
}
