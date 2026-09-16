namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Core.Model;

/// <summary>Appearance associated with an arbitrary genre code, block number, or other string key.</summary>
public sealed record StyleMappingEntry(string Key, string PrimaryColor, string SecondaryColor, string Pattern);

/// <summary>Edits keyed appearances without changing the project until explicitly applied.</summary>
public sealed class StyleMappingDraft
{
    public static IReadOnlyList<(string Id, string Label)> Colors { get; } = Array.AsReadOnly(new (string, string)[]
    {
        ("red", "赤"), ("yellow", "黄"), ("yellow-green", "黄緑"), ("green", "緑"),
        ("cyan", "水色"), ("blue-green", "青緑"), ("blue", "青"), ("indigo", "藍色"),
        ("blue-violet", "青紫"), ("red-violet", "赤紫"), ("pink", "桃色"), ("brown", "茶色"),
        ("white", "白"), ("black", "黒"), ("gray", "灰色"),
    });
    public static IReadOnlyList<(string Id, string Label)> Patterns { get; } = Array.AsReadOnly(new (string, string)[]
    {
        ("solid", "単色"), ("horizontal", "（太細）横縞"), ("vertical", "（太細）縦縞"),
        ("thick-grid", "（太細）格子"), ("checkerboard", "市松模様"), ("grid", "格子"),
        ("uniform-horizontal", "（均等）横縞"), ("dots", "水玉"),
    });
    private readonly List<StyleMappingEntry> rows;
    private readonly StyleMappingEntry[] otherStyles;
    public IReadOnlyList<StyleMappingEntry> Rows { get; }

    public StyleMappingDraft(IEnumerable<string> keys, IEnumerable<StyleMappingEntry> styles)
    {
        var keysToEdit = keys.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var existing = styles.ToArray();
        var configured = existing.ToDictionary(style => style.Key, StringComparer.Ordinal);
        rows = keysToEdit.Select((id, index) =>
        {
            var patternIndex = index / 12;
            var style = configured.GetValueOrDefault(id) ?? new StyleMappingEntry(id, Colors[index % 12].Id, "white",
                patternIndex == 0 ? "solid" : Patterns[1 + (patternIndex - 1) % (Patterns.Count - 1)].Id);
            return style with { Pattern = NormalizePattern(style.Pattern) };
        }).ToList();
        Rows = rows.AsReadOnly();
        otherStyles = existing.Where(style => !keysToEdit.Contains(style.Key, StringComparer.Ordinal)).ToArray();
    }

    public static string NormalizePattern(string pattern) => pattern switch
    {
        "checker" => "thick-grid", "thick-horizontal" => "uniform-horizontal", _ => pattern,
    };

    public void SetColor(int index, bool primary, string value)
    {
        value = value.Trim();
        if (!Colors.Any(choice => choice.Id == value) && !RgbHexColor.TryParse(value, out _, out _, out _))
            throw new ArgumentException("色見本から選ぶか、#RRGGBB 形式で入力してください。", nameof(value));
        if (!primary && rows[index].Pattern == "solid") throw new InvalidOperationException("単色では副色を使用しません。");
        value = value.StartsWith('#') ? value.ToUpperInvariant() : value;
        rows[index] = primary ? rows[index] with { PrimaryColor = value } : rows[index] with { SecondaryColor = value };
    }

    public void SetPattern(int index, string pattern)
    {
        pattern = NormalizePattern(pattern);
        if (!Patterns.Any(choice => choice.Id == pattern)) throw new ArgumentException("網掛けパターンを選択してください。", nameof(pattern));
        rows[index] = rows[index] with { Pattern = pattern };
    }

    public StyleMappingEntry[] Build()
    {
        foreach (var style in rows)
        {
            foreach (var color in new[] { style.PrimaryColor, style.SecondaryColor })
                if (!Colors.Any(choice => choice.Id == color) && !RgbHexColor.TryParse(color, out _, out _, out _))
                    throw new InvalidDataException($"{style.Key} の色「{color}」を色名または #RRGGBB で指定してください。");
            if (!Patterns.Any(choice => choice.Id == style.Pattern))
                throw new InvalidDataException($"{style.Key} の網掛けパターンを選択してください。");
        }
        return [.. rows, .. otherStyles];
    }
}
