namespace CircleSpaceCoordinator.Desktop.Tests;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core;
using CircleSpaceCoordinator.Desktop.Core.Interaction;
using CircleSpaceCoordinator.Engine.Model;
using CircleSpaceCoordinator.Desktop.Core.Persistence;

internal static partial class Program
{
    private static void DiagonalPatterns()
    {
        string[] ids = ["diagonal-up", "uniform-diagonal-up", "diagonal-down", "uniform-diagonal-down",
            "diagonal-grid", "uniform-diagonal-grid", "diamond"];
        foreach (var id in ids)
        {
            var pattern = DiagonalPattern.FromId(id);
            AssertEqual(true, DiagonalPattern.IsDiagonal(pattern));
            for (var y = -16; y < 16; y++)
            for (var x = -16; x < 16; x++)
            {
                AssertEqual(DiagonalPattern.UsesSecondary(pattern, x, y), DiagonalPattern.UsesSecondary(pattern, x + 16, y));
                AssertEqual(DiagonalPattern.UsesSecondary(pattern, x, y), DiagonalPattern.UsesSecondary(pattern, x, y + 16));
            }
        }
        foreach (var (id, width) in new[] { (ids[0], 3), (ids[1], 8), (ids[2], 3), (ids[3], 8) })
        {
            var pattern = DiagonalPattern.FromId(id);
            for (var y = 0; y < 16; y++)
            {
                AssertEqual(width, Enumerable.Range(0, 16).Count(x => DiagonalPattern.UsesSecondary(pattern, x, y)));
                for (var x = 0; x < 16; x++)
                    AssertEqual(DiagonalPattern.UsesSecondary(pattern, x, y),
                        DiagonalPattern.UsesSecondary(pattern, x + (id.Contains("up") ? 1 : -1), y - 1));
            }
        }
        for (var y = 0; y < 16; y++)
        for (var x = 0; x < 16; x++)
        {
            AssertEqual(DiagonalPattern.UsesSecondary(100, x, y) || DiagonalPattern.UsesSecondary(102, x, y), DiagonalPattern.UsesSecondary(104, x, y));
            AssertEqual(DiagonalPattern.UsesSecondary(101, x, y) || DiagonalPattern.UsesSecondary(103, x, y), DiagonalPattern.UsesSecondary(105, x, y));
            AssertEqual(DiagonalPattern.UsesSecondary(101, x, y) == DiagonalPattern.UsesSecondary(103, x, y), DiagonalPattern.UsesSecondary(106, x, y));
        }
        AssertEqual(128, Enumerable.Range(0, 256).Count(i => DiagonalPattern.UsesSecondary(106, i % 16, i / 16)));
        var project = CreateProject() with { GenreStyles = ids.Select(id => new GenreStyleDefinition(id, "black", "white", id)).ToArray() };
        var draft = new GenreStyleDraft(project);
        foreach (var row in draft.Rows.Select((style, index) => (style, index))) draft.SetPattern(row.index, row.style.Pattern);
        AssertEqual(false, draft.Mapping.HasChanges);
        var directory = Path.Combine(Path.GetTempPath(), $"diagonal-pattern-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "event.json");
            ProjectFileService.Save(path, project);
            using var workspace = DesktopApplication.LoadWorkspace(path);
            workspace.Execute(new SetGenreStyles(draft.Build()), selectedPlanEdit: false);
            ProjectFileService.Save(path, workspace.Project);
            var restored = ProjectFileService.Load(path);
            AssertEqual(true, ids.Order().SequenceEqual(restored.GenreStyles.Select(style => style.Pattern).Order()));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
