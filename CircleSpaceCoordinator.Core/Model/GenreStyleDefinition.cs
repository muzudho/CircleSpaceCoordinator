namespace CircleSpaceCoordinator.Core.Model;

/// <summary>Project-specific visual mapping for one genre.</summary>
public sealed record GenreStyleDefinition(
    string GenreId,
    string PrimaryColor,
    string SecondaryColor,
    string Pattern);
