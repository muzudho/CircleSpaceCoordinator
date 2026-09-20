using System.Diagnostics;
using CircleSpaceCoordinator.EditorClient;
using CircleSpaceCoordinator.Engine.Contracts.V1;
using Grpc.Core;

internal static class LazyThinkingChecks
{
    public static HashSet<int> ProcessIds(string name = "CircleSpaceCoordinator.ThinkingEngine")
    {
        var ids = new HashSet<int>();
        foreach (var process in Process.GetProcessesByName(name))
        { using (process) ids.Add(process.Id); }
        return ids;
    }

    public static async Task CheckParentExit(EngineRuntime runtime, RemoteWorkspace workspace, HashSet<int> baseline, int ownedEditorId)
    {
        workspace.Refresh();
        var handle = await runtime.Connection.Client.StartJobAsync(new EditorOptimizeRequest
        {
            WorkspaceId = workspace.Id, ExpectedRevision = workspace.Revision,
            SourcePlanId = workspace.SelectedPlanId, NewPlanId = "parent-exit", NewPlanName = "Parent exit check",
            Options = new OptimizationOptions { MaximumIterations = int.MaxValue, TimeLimitMs = 30000 },
        });
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var children = ProcessIds().Except(baseline).ToArray();
            if (children.Length == 0) { await Task.Delay(10); continue; }
            var thinkerId = children.Single();
            using var thinker = Process.GetProcessById(thinkerId);
            using var editor = Process.GetProcessById(ownedEditorId);
            editor.Kill(); // Deliberately leave the child alive to exercise its early parent watcher.
            await editor.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await thinker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Console.WriteLine("PASS: thinking process exits when its owning editor dies during startup, without a tree kill.");
            return;
        }
        throw new Exception("Parent-exit check never observed a thinking process.");
    }

    public static async Task Run(EngineRuntime runtime, RemoteWorkspace workspace, HashSet<int> baseline)
    {
        void AssertNoThinking()
        {
            if (ProcessIds().Except(baseline).Any()) throw new Exception("An owned thinking process survived outside an optimization.");
        }
        AssertNoThinking();
        var observedPids = new HashSet<int>();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            workspace.Refresh();
            var handle = await runtime.Connection.Client.StartJobAsync(new EditorOptimizeRequest
            {
                WorkspaceId = workspace.Id, ExpectedRevision = workspace.Revision,
                SourcePlanId = workspace.SelectedPlanId, NewPlanId = "lazy-" + Guid.NewGuid().ToString("N"),
                NewPlanName = "Lazy startup check", OnlyIfImproved = true,
                Options = new OptimizationOptions { MaximumIterations = attempt == 0 ? int.MaxValue : 10,
                    TimeLimitMs = attempt == 0 ? 30000 : 200 },
            }, deadline: DateTime.UtcNow.AddSeconds(10));
            if (attempt == 2) await runtime.Connection.Client.StopJobAsync(handle);
            using var watch = runtime.Connection.Client.WatchJob(handle, deadline: DateTime.UtcNow.AddSeconds(40));
            var stopped = false;
            var completed = false;
            await foreach (var item in watch.ResponseStream.ReadAllAsync())
            {
                foreach (var pid in ProcessIds().Except(baseline)) observedPids.Add(pid);
                if (attempt == 0 && !stopped && item.ProgressJson.Length > 0)
                {
                    if (observedPids.Count == 0) throw new Exception("Optimization did not start a thinking process.");
                    await runtime.Connection.Client.StopJobAsync(handle);
                    stopped = true;
                }
                if (!item.Completed) continue;
                if (item.ErrorCode.Length > 0 && !(attempt == 2 && item.ErrorCode == "Cancelled"))
                    throw new Exception("Lazy optimization failed: " + item.ErrorCode + ": " + item.ErrorMessage);
                completed = true;
                break;
            }
            if (!completed) throw new Exception("Optimization did not finish.");
            AssertNoThinking();
            await runtime.Connection.Client.ReleaseJobAsync(handle);
        }
        workspace.Refresh();
        workspace.Accept(await workspace.FillVacantSeatsAsync());
        AssertNoThinking();
        Console.WriteLine("PASS: no thinking process during editing; on-demand optimization, stop, restart, startup cancellation and vacant-seat fill leave no thinking process.");
    }
}
