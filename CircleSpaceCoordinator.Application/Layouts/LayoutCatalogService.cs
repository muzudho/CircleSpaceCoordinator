namespace CircleSpaceCoordinator.Application.Layouts;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;

/// <summary>Lifecycle operations for the separated physical and circle layouts.</summary>
public static class LayoutCatalogService
{
    public static CircleSpaceProject CreateDeskLayout(CircleSpaceProject project, string id, string name, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        project = LayoutProjection.MigrateLegacyPlans(project);
        if (project.DeskLayouts.Any(item => item.Id == id))
            throw new InvalidOperationException($"Desk layout ID '{id}' already exists.");
        return ValidateProjected(project with { DeskLayouts = [.. project.DeskLayouts, new DeskLayout(id, name.Trim(), []) { Description = description }] });
    }

    public static CircleSpaceProject CreateCircleLayout(CircleSpaceProject project, string id, string name, string deskLayoutId, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(deskLayoutId);
        project = LayoutProjection.MigrateLegacyPlans(project);
        if (!project.DeskLayouts.Any(item => item.Id == deskLayoutId))
            throw new KeyNotFoundException($"Desk layout '{deskLayoutId}' does not exist.");
        if (project.CircleLayouts.Any(item => item.Id == id))
            throw new InvalidOperationException($"Circle layout ID '{id}' already exists.");
        return ValidateProjected(project with { CircleLayouts = [.. project.CircleLayouts, new CircleLayout(id, name.Trim(), deskLayoutId, []) { Description = description }] });
    }

    public static CircleSpaceProject RemoveCircleLayout(CircleSpaceProject project, string circleLayoutId)
    {
        project = LayoutProjection.MigrateLegacyPlans(project);
        var layouts = project.CircleLayouts.Where(item => item.Id != circleLayoutId).ToArray();
        if (layouts.Length == project.CircleLayouts.Count)
            throw new KeyNotFoundException($"Circle layout '{circleLayoutId}' does not exist.");
        return ValidateProjected(project with { CircleLayouts = layouts });
    }

    public static CircleSpaceProject RemoveDeskLayout(CircleSpaceProject project, string deskLayoutId)
    {
        project = LayoutProjection.MigrateLegacyPlans(project);
        var count = project.CircleLayouts.Count(item => item.DeskLayoutId == deskLayoutId);
        if (count != 0)
            throw new InvalidOperationException($"Desk layout '{deskLayoutId}' is used by {count} circle layout(s) and cannot be removed.");
        var layouts = project.DeskLayouts.Where(item => item.Id != deskLayoutId).ToArray();
        if (layouts.Length == project.DeskLayouts.Count)
            throw new KeyNotFoundException($"Desk layout '{deskLayoutId}' does not exist.");
        return ValidateProjected(project with { DeskLayouts = layouts });
    }

    public static CircleSpaceProject ReassignCircleLayout(CircleSpaceProject project, string circleLayoutId, string deskLayoutId)
    {
        project = LayoutProjection.MigrateLegacyPlans(project);
        if (!project.DeskLayouts.Any(item => item.Id == deskLayoutId))
            throw new KeyNotFoundException($"Desk layout '{deskLayoutId}' does not exist.");
        var index = project.CircleLayouts.ToList().FindIndex(item => item.Id == circleLayoutId);
        if (index < 0) throw new KeyNotFoundException($"Circle layout '{circleLayoutId}' does not exist.");
        var circles = project.CircleLayouts.ToArray();
        circles[index] = circles[index] with { DeskLayoutId = deskLayoutId };
        return ValidateProjected(project with { CircleLayouts = circles });
    }

    public static CircleSpaceProject RenameDeskLayout(CircleSpaceProject project, string deskLayoutId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        project = LayoutProjection.MigrateLegacyPlans(project);
        var index = project.DeskLayouts.ToList().FindIndex(item => item.Id == deskLayoutId);
        if (index < 0) throw new KeyNotFoundException($"Desk layout '{deskLayoutId}' does not exist.");
        var desks = project.DeskLayouts.ToArray();
        desks[index] = desks[index] with { Name = name.Trim() };
        return ValidateProjected(project with { DeskLayouts = desks });
    }

    public static CircleSpaceProject RenameCircleLayout(CircleSpaceProject project, string circleLayoutId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        project = LayoutProjection.MigrateLegacyPlans(project);
        var index = project.CircleLayouts.ToList().FindIndex(item => item.Id == circleLayoutId);
        if (index < 0) throw new KeyNotFoundException($"Circle layout '{circleLayoutId}' does not exist.");
        var circles = project.CircleLayouts.ToArray();
        circles[index] = circles[index] with { Name = name.Trim() };
        return ValidateProjected(project with { CircleLayouts = circles });
    }

    private static CircleSpaceProject ValidateProjected(CircleSpaceProject project)
    {
        project = project with { Plans = LayoutProjection.ToPlans(project) };
        var issues = ProjectValidator.Validate(project);
        if (issues.Count > 0) throw new ProjectValidationException(issues);
        return project;
    }
}
