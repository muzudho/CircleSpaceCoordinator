namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

/// <summary>Shared pixel mask for rectangular, round-token and pie-chart appearances.</summary>
public static class DiagonalPattern
{
    // Keep separate from the legacy automatically generated density variants.
    public static int FromId(string id) => id switch
    {
        "diagonal-up" => 100,
        "uniform-diagonal-up" => 101,
        "diagonal-down" => 102,
        "uniform-diagonal-down" => 103,
        "diagonal-grid" => 104,
        "uniform-diagonal-grid" => 105,
        "diamond" => 106,
        _ => 0,
    };

    public static bool IsDiagonal(int pattern) => pattern is >= 100 and <= 106;

    public static bool UsesSecondary(int pattern, int x, int y)
    {
        const int period = 16;
        static int Mod(int value) => (value % period + period) % period;
        var up = Mod(x + y);
        var down = Mod(x - y);
        return pattern switch
        {
            100 => up < 3,
            101 => up < 8,
            102 => down < 3,
            103 => down < 8,
            104 => up < 3 || down < 3,
            105 => up < 8 || down < 8,
            106 => (up < 8) == (down < 8),
            _ => false,
        };
    }
}
