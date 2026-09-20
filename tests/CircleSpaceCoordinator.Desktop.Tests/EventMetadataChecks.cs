namespace CircleSpaceCoordinator.Desktop.Tests;

using CircleSpaceCoordinator.Desktop.Core.Persistence;

internal static partial class Program
{
    private static void LazyEventMetadata()
    {
        var reads = 0;
        var project = PortableExample() with { IsConfidential = true };
        var fail = false;
        var path = Path.Combine(Path.GetTempPath(), "lazy-event.event-project-csc.json");
        var other = Path.Combine(Path.GetTempPath(), "other-event.event-project-csc.json");
        CircleSpaceCoordinator.Core.Model.CircleSpaceProject Load(string _)
        {
            reads++;
            if (fail) throw new FileNotFoundException("Missing project");
            return project;
        }
        var cache = new EventProjectMetadataCache(Load);
        AssertEqual(true, cache.Peek(path) is null);
        AssertEqual(true, cache.Peek(other) is null);
        AssertEqual(0, reads);
        AssertEqual(true, cache.Inspect(path).Confidential);
        AssertEqual(true, cache.Inspect(path).Confidential);
        AssertEqual(1, reads);
        AssertEqual(true, cache.Peek(other) is null);
        project = project with { IsConfidential = false, DeskLayouts = project.DeskLayouts.Select(item => item with { IsConfidential = false }).ToArray() };
        AssertEqual(true, cache.Inspect(path).Confidential);
        var nextSession = new EventProjectMetadataCache(Load);
        AssertEqual(true, nextSession.Peek(path) is null);
        AssertEqual(false, nextSession.Inspect(path).Confidential);
        AssertEqual(2, reads);
        cache.Invalidate(path);
        AssertEqual(false, cache.Inspect(path).Confidential);
        AssertEqual(3, reads);
        project = project with { DeskLayouts = project.DeskLayouts.Select(item => item with { IsConfidential = true }).ToArray() };
        cache.Invalidate(path);
        AssertEqual(true, cache.Inspect(path).Confidential);
        fail = true;
        AssertEqual(false, cache.Inspect(other).Exists);
        AssertEqual(true, cache.Peek(other)?.Error is not null);
        fail = false;
        AssertEqual(true, cache.Inspect(other).Exists);
        AssertEqual(true, cache.Peek(other)?.Error is null);
        AssertEqual(6, reads);
    }
}
