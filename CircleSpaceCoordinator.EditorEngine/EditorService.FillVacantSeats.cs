namespace CircleSpaceCoordinator.EditorEngine;

using CircleSpaceCoordinator.Engine.Contracts.V1;
using CircleSpaceCoordinator.Infrastructure.Json;
using Grpc.Core;

public sealed partial class EditorService
{
    public override async Task<WorkspaceState> FillVacantSeats(FillVacantSeatsRequest request, ServerCallContext context)
    {
        var input = await Run(() =>
        {
            lock (gate)
            {
                var session = Find(request.WorkspaceId);
                CheckRevision(session, request.ExpectedRevision);
                if (!session.Workspace.HasSelectedCircleLayout)
                    throw new InvalidOperationException("Create a circle layout before assigning circles.");
                return new ThinkingRequest { ProjectJson = ProjectJsonSerializer.Save(session.Workspace.Project),
                    PlanId = session.Workspace.SelectedPlanId };
            }
        });
        var result = await thinking.FillVacantSeatsAsync(input, deadline: context.Deadline, cancellationToken: context.CancellationToken);
        return await Run(() =>
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var filled = ProjectJsonSerializer.Load(result.ProjectJson).Plans.Single(p => p.Id == input.PlanId);
            lock (gate)
            {
                var session = Clone(Find(request.WorkspaceId));
                CheckRevision(session, request.ExpectedRevision);
                if (filled.Assignments.Count != session.Workspace.SelectedPlan.Assignments.Count)
                {
                    session.Workspace.ApplySelectedPlanEdit(project => CircleSpaceCoordinator.Application.Workspace.ModificationCreditsService.Apply(project, project with
                    { Plans = project.Plans.Select(p => p.Id == input.PlanId ? filled : p).ToArray() }, request.Handle, ReadWorkDate(request.WorkDate)));
                    session.Revision++;
                }
                return Snapshot(request.WorkspaceId, session);
            }
        });
    }
}
