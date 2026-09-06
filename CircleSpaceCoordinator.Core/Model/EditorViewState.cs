namespace CircleSpaceCoordinator.Core.Model;

/// <summary>Project-wide editor camera state shared by every placement plan.</summary>
public sealed record EditorViewState(double Zoom, double OriginX, double OriginY);
