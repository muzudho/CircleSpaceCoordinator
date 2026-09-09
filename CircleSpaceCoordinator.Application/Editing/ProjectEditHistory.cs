namespace CircleSpaceCoordinator.Application.Editing;

using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Core.Validation;
using CircleSpaceCoordinator.Engine.Model;

public sealed class ProjectEditHistory
{
    private readonly Stack<CircleSpaceProject> undoStack = new();
    private readonly Stack<CircleSpaceProject> redoStack = new();

    public ProjectEditHistory(CircleSpaceProject initialProject)
    {
        ArgumentNullException.ThrowIfNull(initialProject);
        EnsureValid(initialProject);
        Current = initialProject;
    }

    public CircleSpaceProject Current { get; private set; }

    public EditHistoryState Capture() => new(Current, undoStack.ToArray(), redoStack.ToArray());
    public void Restore(EditHistoryState state)
    {
        EnsureValid(state.Current);
        Current = state.Current;
        undoStack.Clear(); redoStack.Clear();
        foreach (var project in state.Undo.Reverse()) undoStack.Push(project);
        foreach (var project in state.Redo.Reverse()) redoStack.Push(project);
    }

    public bool CanUndo => undoStack.Count > 0;

    public bool CanRedo => redoStack.Count > 0;

    public void Reset(CircleSpaceProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        EnsureValid(project);
        Current = project;
        undoStack.Clear();
        redoStack.Clear();
    }

    public void Apply(Func<CircleSpaceProject, CircleSpaceProject> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        var edited = edit(Current)
            ?? throw new InvalidOperationException("An edit must return a project.");
        EnsureValid(edited);

        undoStack.Push(Current);
        Current = edited;
        redoStack.Clear();
    }

    public CircleSpaceProject Undo()
    {
        if (!CanUndo)
            throw new InvalidOperationException("There is no edit to undo.");

        redoStack.Push(Current);
        Current = undoStack.Pop();
        return Current;
    }

    public CircleSpaceProject Redo()
    {
        if (!CanRedo)
            throw new InvalidOperationException("There is no edit to redo.");

        undoStack.Push(Current);
        Current = redoStack.Pop();
        return Current;
    }

    private static void EnsureValid(CircleSpaceProject project)
    {
        var issues = ProjectValidator.Validate(project);
        if (issues.Count > 0)
            throw new ProjectValidationException(issues);
    }
}
