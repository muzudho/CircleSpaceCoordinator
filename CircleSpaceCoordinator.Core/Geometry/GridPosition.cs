namespace CircleSpaceCoordinator.Core.Geometry;

public readonly record struct GridPosition(int X, int Y)
{
    public GridPosition Rotate(QuarterTurn orientation) => orientation switch
    {
        QuarterTurn.North => this,
        QuarterTurn.East => new GridPosition(-Y, X),
        QuarterTurn.South => new GridPosition(-X, -Y),
        QuarterTurn.West => new GridPosition(Y, -X),
        _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, null),
    };

    public static GridPosition operator +(GridPosition left, GridPosition right) =>
        new(left.X + right.X, left.Y + right.Y);
}

public enum QuarterTurn
{
    North,
    East,
    South,
    West,
}
