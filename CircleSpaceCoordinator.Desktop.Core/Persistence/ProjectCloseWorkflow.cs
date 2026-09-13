namespace CircleSpaceCoordinator.Desktop.Core.Persistence;

public enum ProjectCloseDecision { Cancel, Save, Discard }

/// <summary>Closes only after an explicit decision and, when requested, a successful save.</summary>
public static class ProjectCloseWorkflow
{
    public static bool TryClose(ProjectCloseDecision decision, Func<bool> save, Action close)
    {
        if (decision == ProjectCloseDecision.Cancel) return false;
        if (decision == ProjectCloseDecision.Save && !save()) return false;
        if (decision is not (ProjectCloseDecision.Save or ProjectCloseDecision.Discard))
            throw new ArgumentOutOfRangeException(nameof(decision));
        close();
        return true;
    }
}
