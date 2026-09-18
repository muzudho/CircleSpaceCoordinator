namespace CircleSpaceCoordinator.Core.Evaluation;
using CircleSpaceCoordinator.Core.Geometry;

public sealed record KnowledgeCell(
    [property: System.Text.Json.Serialization.JsonRequired] int X,
    [property: System.Text.Json.Serialization.JsonRequired] int Y,
    [property: System.Text.Json.Serialization.JsonRequired] double Weight);
public sealed record KnowledgeZone(
    [property: System.Text.Json.Serialization.JsonRequired] string Name,
    [property: System.Text.Json.Serialization.JsonRequired] GridPosition[] Cells);
public sealed record KnowledgeVenue(
    [property: System.Text.Json.Serialization.JsonRequired] string Name,
    [property: System.Text.Json.Serialization.JsonRequired] int Width,
    [property: System.Text.Json.Serialization.JsonRequired] int Height,
    [property: System.Text.Json.Serialization.JsonRequired] GridPosition[] BlockedCells,
    [property: System.Text.Json.Serialization.JsonRequired] KnowledgeZone[] Zones);
public sealed record ChannelInputRule(
    [property: System.Text.Json.Serialization.JsonRequired] string RecommendedColumn,
    [property: System.Text.Json.Serialization.JsonRequired] string Meaning,
    [property: System.Text.Json.Serialization.JsonRequired] string ValueMeanings,
    [property: System.Text.Json.Serialization.JsonRequired] bool BlankIsZero, double[]? AllowedValues);

/// <summary>An unbound, reusable channel; it does not participate in evaluation.</summary>
public sealed record ChannelKnowledge(
    [property: System.Text.Json.Serialization.JsonRequired] string Id,
    [property: System.Text.Json.Serialization.JsonRequired] string Name,
    [property: System.Text.Json.Serialization.JsonRequired] string Description,
    [property: System.Text.Json.Serialization.JsonRequired] string Purpose,
    [property: System.Text.Json.Serialization.JsonRequired] ChannelInputRule InputRule,
    [property: System.Text.Json.Serialization.JsonRequired] double Scale,
    [property: System.Text.Json.Serialization.JsonRequired] double Offset,
    [property: System.Text.Json.Serialization.JsonRequired] double OverallWeight,
    [property: System.Text.Json.Serialization.JsonRequired] bool IsConfidential,
    [property: System.Text.Json.Serialization.JsonRequired] double DefaultWeight,
    [property: System.Text.Json.Serialization.JsonRequired] KnowledgeCell[] Cells, KnowledgeVenue? Venue)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [System.Text.Json.Serialization.JsonPropertyName("comment-for-channel")]
    public string? CommentForChannel { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [System.Text.Json.Serialization.JsonPropertyName("comment-for-weight")]
    public string? CommentForWeight { get; init; }
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name) || Name == "番地" ||
            Description is null || Purpose is null || InputRule is null ||
            string.IsNullOrWhiteSpace(InputRule.RecommendedColumn) || InputRule.Meaning is null || InputRule.ValueMeanings is null ||
            !double.IsFinite(Scale) || !double.IsFinite(Offset) || !double.IsFinite(OverallWeight) || !double.IsFinite(DefaultWeight) ||
            Cells is null || InputRule.AllowedValues?.Any(value => !double.IsFinite(value)) == true)
            throw new ArgumentException("知見の名前・入力ルール・有限の係数が必要です。");
        if (Venue is null && Cells.Length != 0)
            throw new ArgumentException("座標別の重みには会場の情報が必要です。");
        if (Venue is not null && (Venue.Width < 1 || Venue.Height < 1 || Venue.BlockedCells is null || Venue.Zones is null ||
            Venue.BlockedCells.Any(cell => !Inside(cell.X, cell.Y)) ||
            Venue.Zones.Any(zone => zone is null || zone.Cells is null || zone.Cells.Any(cell => !Inside(cell.X, cell.Y)))))
            throw new ArgumentException("知見の会場情報が不正です。");
        if (Cells.Any(cell => cell is null || !double.IsFinite(cell.Weight) || !Inside(cell.X, cell.Y)) ||
            Cells.Select(cell => (cell.X, cell.Y)).Distinct().Count() != Cells.Length)
            throw new ArgumentException("知見の座標別重みが不正または重複しています。");
        bool Inside(int x, int y) => Venue is not null && x >= 0 && y >= 0 && x < Venue.Width && y < Venue.Height;
    }
}
