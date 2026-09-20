namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

/// <summary>Windows-style selection in one of two shading tables, keyed independently of scrolling.</summary>
public sealed class ShadingRowSelection
{
    private readonly HashSet<string> keys = new(StringComparer.Ordinal);
    public IReadOnlySet<string> Keys => keys;
    public bool Right { get; private set; }
    public string? Anchor { get; private set; }
    public string? Active { get; private set; }
    public bool Multiple => keys.Count > 1;
    public bool IsLocked(bool right) => Multiple && Right != right;
    public void Clear() { keys.Clear(); Anchor = Active = null; }
    public void Set(bool right, IEnumerable<string> selected)
    {
        Clear(); Right = right;
        foreach (var key in selected) { keys.Add(key); Anchor ??= key; }
        Active = Anchor;
    }
    public bool Click(bool right, IReadOnlyList<string> order, string key, bool shift, bool control)
    {
        if (IsLocked(right) || !order.Contains(key)) return false;
        if (Right != right) Clear();
        Right = right;
        if (shift)
        {
            var start = Anchor is null ? -1 : order.ToList().IndexOf(Anchor);
            var end = order.ToList().IndexOf(key);
            if (start < 0) { Anchor = key; start = end; }
            if (!control) keys.Clear();
            for (var index = Math.Min(start, end); index <= Math.Max(start, end); index++) keys.Add(order[index]);
        }
        else
        {
            if (!control) keys.Clear();
            if (!control || !keys.Remove(key)) keys.Add(key);
            Anchor = key;
        }
        Active = keys.Contains(key) ? key : order.FirstOrDefault(keys.Contains);
        return true;
    }
}
