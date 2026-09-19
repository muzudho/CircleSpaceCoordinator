namespace CircleSpaceCoordinator.Core.Model;

/// <summary>Attribution travels with data; receiving data never changes its providers.</summary>
public sealed record PersonCredits(string? Author = null, string? Modifier = null,
    string? FirstProvider = null, string? LatestProvider = null)
{
    public DateOnly? ModifiedOn { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ChangeLog { get; init; }
    public const int MaximumChangeLogLength = 1000;

    public static string NormalizeChangeLog(string value)
    {
        // Validate before trimming so pasted newlines are not silently accepted.
        var remaining = value.AsSpan();
        while (!remaining.IsEmpty)
        {
            if (System.Text.Rune.DecodeFromUtf16(remaining, out var rune, out var used) != System.Buffers.OperationStatus.Done ||
                System.Text.Rune.IsControl(rune) || rune.Value is 0x2028 or 0x2029)
                throw new ArgumentException("改行・制御文字を含まない１行で入力してください。");
            remaining = remaining[used..];
        }
        value = value.Trim();
        if (value.Length == 0 || value.EnumerateRunes().Count() > MaximumChangeLogLength)
            throw new ArgumentException("変更内容を1〜1000文字で入力してください。");
        return value;
    }
    public const int MaximumHandleLength = 16;

    public static string NormalizeHandle(string handle)
    {
        handle = handle.Trim();
        var remaining = handle.AsSpan();
        var count = 0;
        while (!remaining.IsEmpty)
        {
            var status = System.Text.Rune.DecodeFromUtf16(remaining, out var rune, out var consumed);
            if (status != System.Buffers.OperationStatus.Done || System.Text.Rune.IsControl(rune) || ++count > MaximumHandleLength)
                throw new ArgumentException("Handleは改行を含まないUnicode 16文字以内で入力してください。");
            remaining = remaining[consumed..];
        }
        return handle;
    }

    public void Validate()
    {
        if (ChangeLog is not null)
        {
            NormalizeChangeLog(ChangeLog);
            if (string.IsNullOrWhiteSpace(Modifier) || ModifiedOn is null)
                throw new ArgumentException("変更タグには変更者名・変更日・チェンジログを揃えてください。");
        }
        foreach (var name in new[] { Author, Modifier, FirstProvider, LatestProvider })
            if (name is not null) NormalizeHandle(name);
    }

    public PersonCredits WrittenBy(string handle, DateOnly? modifiedOn = null, string? changeLog = null)
    {
        handle = NormalizeHandle(handle);
        if (handle.Length == 0) throw new ArgumentException("作業者のHandleを設定してください。");
        return this with { Modifier = handle, ModifiedOn = modifiedOn ?? DateOnly.FromDateTime(DateTime.Now),
            ChangeLog = changeLog is null ? null : NormalizeChangeLog(changeLog) };
    }

    public PersonCredits ProvidedBy(string name)
    {
        name = NormalizeHandle(name);
        if (name.Length == 0) return this;
        return this with { FirstProvider = FirstProvider ?? name, LatestProvider = name };
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public string AttributionText => $"by {Modifier ?? Author ?? "変更者不明"} since {ModifiedOn?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "日付不明"}";
}
