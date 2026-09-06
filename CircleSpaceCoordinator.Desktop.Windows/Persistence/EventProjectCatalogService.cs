namespace CircleSpaceCoordinator.Desktop.Windows.Persistence;

using CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;

public sealed class EventProjectCatalogService(ApplicationSettingsService settings)
{
    public IReadOnlyList<EventProjectReference> Projects => settings.Current.EventProjects ?? [];

    public EventProjectReference Register(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var project = ProjectFileService.Load(fullPath);
        settings.RegisterProject(fullPath, project.Name);
        return new EventProjectReference(fullPath, project.Name);
    }

    public EventProjectReference Create(string path, string name, bool isConfidential = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath))
            throw new IOException($"The destination file already exists: {fullPath}");
        ProjectFileService.Save(fullPath, CreateEmptyProject(name.Trim()) with { IsConfidential = isConfidential });
        settings.RegisterProject(fullPath, name.Trim());
        return new EventProjectReference(fullPath, name.Trim());
    }

    public EventProjectReference Duplicate(string sourcePath, string destinationPath, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var fullDestinationPath = Path.GetFullPath(destinationPath);
        if (File.Exists(fullDestinationPath))
            throw new IOException($"The destination file already exists: {fullDestinationPath}");
        var source = ProjectFileService.Load(sourcePath);
        var copy = source with
        {
            Id = $"event-{Guid.NewGuid():N}",
            Name = name.Trim(),
        };
        ProjectFileService.Save(fullDestinationPath, copy);
        settings.RegisterProject(fullDestinationPath, copy.Name);
        return new EventProjectReference(fullDestinationPath, copy.Name);
    }

    public void Remove(string path) => settings.RemoveProject(path);

    public bool Move(string path, int offset) => settings.MoveProject(path, offset);

    public bool IsConfidential(string path) => ProjectFileService.Load(path).IsConfidential;

    /// <summary>One-way application operation. Clearing the flag requires direct JSON editing.</summary>
    public void MarkConfidential(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var project = ProjectFileService.Load(fullPath);
        if (!project.IsConfidential)
            ProjectFileService.Save(fullPath, project with { IsConfidential = true });
    }

    private static CircleSpaceProject CreateEmptyProject(string name)
    {
        var deskType = new DeskType(
            "standard-desk",
            "標準机",
            [new(0, 0), new(1, 0)]);
        return new CircleSpaceProject(
            "1.0",
            $"event-{Guid.NewGuid():N}",
            name,
            new Venue("main-venue", "会場", 10, 10, new HashSet<GridPosition>()),
            [deskType],
            [],
            new EvaluationConfiguration([], []),
            [new Plan("plan-1", "配置案1", [], [])])
        {
            Description = "Circle Space Coordinatorで作成したイベントプロジェクト",
        };
    }
}
