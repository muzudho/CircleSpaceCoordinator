namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

/// <summary>Repeat editing commands while held, without replaying missed ticks after a pause.</summary>
public sealed class EditingKeyRepeat
{
    private double? next;

    public void Reset() => next = null;

    public bool Update(bool down, bool pressed, double seconds, bool enabled = true)
    {
        if (!enabled || !down) { Reset(); return false; }
        if (pressed) { next = seconds + 0.45; return true; }
        // A key held through an IME or focus transition must be released before rearming.
        if (next is not { } due || seconds < due) return false;
        next = seconds + 0.05;
        return true;
    }
}
