namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Desktop.Core.Persistence;

/// <summary>Isolated editing state. Resizing hides cells until save; cancelling leaves the source untouched.</summary>
public sealed class SpaceDefinitionDraft
{
    private readonly SpaceTypeDefinition? type;
    private readonly SpaceRequestDefinition? request;
    private readonly Dictionary<(int X, int Y), int> cells = [];
    public bool IsRequest => request is not null;
    public string Name { get; set; }
    public string Description { get; set; } = "";
    public string Kind { get; set; } = "机";
    public int Width { get; private set; } = 1;
    public int Height { get; private set; } = 1;
    public string[] Edges { get; } = ["開放", "開放", "開放", "開放"];
    public HashSet<SpaceTarget> Targets { get; } = [];

    public SpaceDefinitionDraft(SpaceTypeDefinition source)
    {
        type = source;
        Name = source.Name;
        Kind = source.Kind;
        Width = source.Width;
        Height = source.Height;
        Edges = source.Edges.ToArray();
        foreach (var cell in source.Cells) cells[(cell.X, cell.Y)] = cell.Area;
    }

    public SpaceDefinitionDraft(SpaceRequestDefinition source)
    {
        request = source;
        Name = source.Value;
        Description = source.Description;
        Targets.UnionWith(source.Targets);
    }

    public void Resize(int width, int height)
    {
        if (width is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(width));
        if (height is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
    }

    public int? AreaAt(int x, int y) => cells.TryGetValue((x, y), out var area) ? area : null;

    public void Paint(int x, int y, int? area)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(x));
        if (area is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(area));
        if (area is null) cells.Remove((x, y));
        else cells[(x, y)] = area.Value;
    }

    public SpaceTypeDefinition BuildType() => (type ?? throw new InvalidOperationException("Not a type draft.")) with
    {
        Name = Name.Trim(), Kind = Kind, Width = Width, Height = Height,
        Cells = cells.Where(pair => pair.Key.X < Width && pair.Key.Y < Height)
            .OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X)
            .Select(pair => new SpaceCell(pair.Key.X, pair.Key.Y, pair.Value)).ToArray(),
        Edges = Edges.ToArray(),
    };

    public SpaceRequestDefinition BuildRequest() => (request ?? throw new InvalidOperationException("Not a request draft.")) with
    {
        Value = Name.Trim(), Description = Description.Trim(),
        Targets = Targets.OrderBy(target => target.TypeId, StringComparer.Ordinal).ThenBy(target => target.Area).ToArray(),
    };
}
