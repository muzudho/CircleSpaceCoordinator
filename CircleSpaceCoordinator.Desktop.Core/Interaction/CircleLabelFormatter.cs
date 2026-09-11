namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using System.Globalization;
using System.Text.RegularExpressions;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Persistence;

public static class CircleLabelFormatter
{
    public static string Format(Participant participant, int number, CircleLabelDisplaySettings display) => display.DisplayField switch
    {
        "circleId" => FormatId(participant.CircleId, display),
        "displayName" => FormatName(participant.DisplayName),
        "channel" when display.ChannelId is { } id => participant.GetFeatureValue(id).ToString("G", CultureInfo.InvariantCulture),
        _ => number.ToString(CultureInfo.InvariantCulture),
    };

    private static string FormatId(string id, CircleLabelDisplaySettings display)
    {
        if (string.IsNullOrWhiteSpace(display.CircleIdPattern)) return id;
        try
        {
            return Regex.Replace(id, display.CircleIdPattern, display.CircleIdReplacement ?? "",
                RegexOptions.None, TimeSpan.FromMilliseconds(100));
        }
        catch (ArgumentException) { return id; }
        catch (RegexMatchTimeoutException) { return id; }
    }

    private static string FormatName(string name)
    {
        const int charactersPerLine = 4;
        const int maximumCharacters = 12;
        var compact = name.Replace("\r", "").Replace("\n", "").Trim();
        if (compact.Length > maximumCharacters) compact = compact[..(maximumCharacters - 1)] + "…";
        return string.Join("\r\n", Enumerable.Range(0, (compact.Length + charactersPerLine - 1) / charactersPerLine)
            .Select(index => compact.Substring(index * charactersPerLine,
                Math.Min(charactersPerLine, compact.Length - index * charactersPerLine))));
    }
}
