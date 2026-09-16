namespace CircleSpaceCoordinator.Core.Model;

/// <summary>Event-wide appearance for a block number, shared across frame layouts.</summary>
public sealed record BlockStyleDefinition(string BlockNumber, string PrimaryColor, string SecondaryColor, string Pattern);
