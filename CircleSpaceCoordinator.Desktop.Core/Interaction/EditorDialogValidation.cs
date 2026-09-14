namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using System.Globalization;
using System.Text.RegularExpressions;

public static class EditorDialogValidation
{
    public static string? Name(string value, bool allowEmpty = false) => !allowEmpty && string.IsNullOrWhiteSpace(value)
        ? "名前を入力してください。空白だけの名前は使用できません。" : null;
    public static string? ChannelName(string value) => string.IsNullOrWhiteSpace(value) || value.Trim() == "番地"
        ? "番地以外のチャンネル名を入力してください。" : null;
    public static string? Weight(string value) => decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
        CultureInfo.CurrentCulture, out var parsed) && parsed >= 0 && parsed <= 1 && decimal.Round(parsed, 6) == parsed
        ? null : "0～1、小数点以下6桁までの数値を入力してください。";
    public static string? CircleIdPattern(string value)
    {
        try { _ = new Regex(value, RegexOptions.None, TimeSpan.FromMilliseconds(100)); return null; }
        catch (ArgumentException exception) { return "正規表現が正しくありません。\n" + exception.Message; }
    }
}
