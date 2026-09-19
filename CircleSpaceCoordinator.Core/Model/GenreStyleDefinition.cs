namespace CircleSpaceCoordinator.Core.Model;

/// <summary>Project-specific visual mapping for one genre.</summary>
public sealed record GenreStyleDefinition(
    string GenreId,
    string PrimaryColor,
    string SecondaryColor,
    string Pattern)
{
    public string? KnowledgeComment { get; init; }

    public static string? NormalizeKnowledgeComment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return PersonCredits.NormalizeChangeLog(value); }
        catch (ArgumentException)
        { throw new ArgumentException("知見コメントは改行を含まない1000文字以内で入力してください。"); }
    }
}
