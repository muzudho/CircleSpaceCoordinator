namespace CircleSpaceCoordinator.Core.Model;

/// <summary>Parses the project color notation #RRGGBB without depending on a UI framework.</summary>
public static class RgbHexColor
{
    public static bool TryParse(string? value, out byte red, out byte green, out byte blue)
    {
        red = green = blue = 0;
        return value is { Length: 7 } && value[0] == '#' &&
               byte.TryParse(value.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber,
                   System.Globalization.CultureInfo.InvariantCulture, out red) &&
               byte.TryParse(value.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber,
                   System.Globalization.CultureInfo.InvariantCulture, out green) &&
               byte.TryParse(value.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber,
                   System.Globalization.CultureInfo.InvariantCulture, out blue);
    }
}
