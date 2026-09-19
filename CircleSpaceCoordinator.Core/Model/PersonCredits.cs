namespace CircleSpaceCoordinator.Core.Model;

/// <summary>Attribution travels with data; receiving data never changes its providers.</summary>
public sealed record PersonCredits(string? Author = null, string? Modifier = null,
    string? FirstProvider = null, string? LatestProvider = null)
{
    public DateOnly? ModifiedOn { get; init; }
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
        foreach (var name in new[] { Author, Modifier, FirstProvider, LatestProvider })
            if (name is not null) NormalizeHandle(name);
    }

    public PersonCredits WrittenBy(string handle, DateOnly? modifiedOn = null)
    {
        handle = NormalizeHandle(handle);
        if (handle.Length == 0) throw new ArgumentException("作業者のHandleを設定してください。");
        return this with { Modifier = handle, ModifiedOn = modifiedOn ?? DateOnly.FromDateTime(DateTime.Now) };
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
