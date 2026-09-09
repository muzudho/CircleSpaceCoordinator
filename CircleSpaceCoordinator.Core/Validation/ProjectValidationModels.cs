namespace CircleSpaceCoordinator.Core.Validation;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;

public sealed record ValidationIssue(string Code, string Path, string Message);

public sealed class ProjectValidationException : Exception
{
    public ProjectValidationException(IReadOnlyList<ValidationIssue> issues)
        : base($"The project has {issues.Count} validation issue(s).")
    {
        Issues = issues;
    }

    public IReadOnlyList<ValidationIssue> Issues { get; }
}

