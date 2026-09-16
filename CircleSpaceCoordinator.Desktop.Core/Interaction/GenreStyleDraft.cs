namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Core.Model;

/// <summary>Adapts project genre appearances to the shared mapping editor.</summary>
public sealed class GenreStyleDraft
{
    public static IReadOnlyList<(string Id, string Label)> Colors => StyleMappingDraft.Colors;
    public static IReadOnlyList<(string Id, string Label)> Patterns => StyleMappingDraft.Patterns;
    public StyleMappingDraft Mapping { get; }
    public IReadOnlyList<GenreStyleDefinition> Rows => Mapping.Rows.Select(ToGenreStyle).ToArray();

    public GenreStyleDraft(CircleSpaceProject project)
    {
        Mapping = new StyleMappingDraft(
            project.Participants.Select(item => item.GenreId).OfType<string>(),
            project.GenreStyles.Select(style => new StyleMappingEntry(style.GenreId,
                style.PrimaryColor, style.SecondaryColor, style.Pattern)));
    }

    public static string NormalizePattern(string pattern) => StyleMappingDraft.NormalizePattern(pattern);
    public void SetColor(int index, bool primary, string value) => Mapping.SetColor(index, primary, value);
    public void SetPattern(int index, string pattern) => Mapping.SetPattern(index, pattern);
    public GenreStyleDefinition[] Build() => Mapping.Build().Select(ToGenreStyle).ToArray();
    private static GenreStyleDefinition ToGenreStyle(StyleMappingEntry style) =>
        new(style.Key, style.PrimaryColor, style.SecondaryColor, style.Pattern);
}
