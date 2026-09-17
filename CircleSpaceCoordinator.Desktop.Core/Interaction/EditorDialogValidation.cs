namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using System.Globalization;
using System.Text.RegularExpressions;

public static class EditorDialogValidation
{
    public static string? Name(string value, bool allowEmpty = false) => !allowEmpty && string.IsNullOrWhiteSpace(value)
        ? "名前を入力してください。空白だけの名前は使用できません。" : null;
    public static string? ChannelName(string value) => string.IsNullOrWhiteSpace(value) || value.Trim() == "番地"
        ? "番地以外のチャンネル名を入力してください。" : null;
    public static bool TryParseWeight(string value, out decimal parsed)
    {
        parsed = 0;
        value = value.Trim();
        if (value.Length == 0) return true;
        var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var point = value.IndexOf(separator, StringComparison.Ordinal);
        return (point < 0 || value.Length - point - separator.Length <= 3)
            && decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.CurrentCulture, out parsed) && parsed >= -1 && parsed <= 1;
    }

    public static string? Weight(string value) => TryParseWeight(value, out _)
        ? null : "-1.000～1.000、小数点以下3桁までの数値を入力してください。空欄は0になります。";
    public static string? CircleIdPattern(string value)
    {
        try { _ = new Regex(value, RegexOptions.None, TimeSpan.FromMilliseconds(100)); return null; }
        catch (ArgumentException exception) { return "正規表現が正しくありません。\n" + exception.Message; }
    }
}
