namespace CircleSpaceCoordinator.Core.Model;
using System.Text.Json.Serialization;
using CircleSpaceCoordinator.Core.Evaluation;

/// <summary>Standalone reusable data, with no participants or evaluation results.</summary>
public sealed record PortableMaterial(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Kind,
    [property: JsonRequired] string Name,
    [property: JsonRequired] bool IsConfidential)
{
    public PersonCredits? Credits { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OverallComment { get; init; }
    public SpaceDefinitionCatalog? Definitions { get; init; }
    public KnowledgeVenue? Venue { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GenreStyleDefinition[]? GenreStyles { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? GenreCodeOrder { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GenreCodeOrderComment { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BlockStyleDefinition[]? BlockStyles { get; init; }

    public void Validate()
    {
        Credits?.Validate();
        if (OverallComment is not null)
        {
            if (Kind != "genre-styles") throw new InvalidDataException("全体コメントはジャンル対応表の項目です。");
            GenreStyleDefinition.NormalizeKnowledgeComment(OverallComment);
        }
        if (GenreCodeOrderComment is not null)
        {
            if (Kind != "genre-styles") throw new InvalidDataException("並び順コメントはジャンル対応表の項目です。");
            GenreStyleDefinition.NormalizeKnowledgeComment(GenreCodeOrderComment);
        }
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name))
            throw new InvalidDataException("素材のIDと名前が必要です。");
        if (Kind is "genre-styles" or "block-styles")
        {
            if (Kind == "genre-styles") GenreStyleDefinition.NormalizeTableName(Name);
            if (Venue is not null || Definitions is not null ||
                Kind == "genre-styles" && (GenreStyles is null || BlockStyles is not null) ||
                Kind == "block-styles" && (BlockStyles is null || GenreStyles is not null))
                throw new InvalidDataException("対応表素材の内容が不正です。");
            if (Kind != "genre-styles" && (GenreCodeOrder is not null || GenreCodeOrderComment is not null))
                throw new InvalidDataException("ジャンルコード順はジャンル対応表だけに含められます。");
            if (GenreCodeOrder is not null && GenreCodeOrder.Distinct(StringComparer.Ordinal).Count() != GenreCodeOrder.Count)
                throw new InvalidDataException("ジャンルコード順に重複があります。");
            var rows = Kind == "genre-styles"
                ? GenreStyles!.Select(s => s is null ? default : (s.GenreId, s.PrimaryColor, s.SecondaryColor, s.Pattern)).ToArray()
                : BlockStyles!.Select(s => s is null ? default : (s.BlockNumber, s.PrimaryColor, s.SecondaryColor, s.Pattern)).ToArray();
            foreach (var style in GenreStyles ?? [])
                if (style is not null) GenreStyleDefinition.NormalizeKnowledgeComment(style.KnowledgeComment);
            if (rows.Select(s => s.Item1).Distinct(StringComparer.Ordinal).Count() != rows.Length ||
                rows.Any(s => string.IsNullOrWhiteSpace(s.Item1) || string.IsNullOrWhiteSpace(s.Item2) ||
                    string.IsNullOrWhiteSpace(s.Item3) || string.IsNullOrWhiteSpace(s.Item4)))
                throw new InvalidDataException("対応表のキー重複または空欄があります。");
            return;
        }
        if (GenreStyles is not null || BlockStyles is not null) throw new InvalidDataException("対応表を別の素材へ混在できません。");
        if (Kind == "venue")
        {
            if (Venue is null || Definitions is not null) throw new InvalidDataException("会場素材の内容が不正です。");
            new ChannelKnowledge(Id, Name, "", "", new("venue", "", "", true, null), 1, 0, 1,
                IsConfidential, 0, [], Venue).Validate();
        }
        else if (Kind is "frame-definition" or "request-definition")
        {
            if (Definitions is null || Venue is not null) throw new InvalidDataException("定義素材の内容が不正です。");
            Definitions.Validate(requireRepresentativeCell: false);
            if (Kind == "frame-definition" && (Definitions.Types.Count != 1 || Definitions.Requests.Count != 0) ||
                Kind == "request-definition" && (Definitions.Requests.Count != 1 ||
                    !Definitions.Types.Select(type => type.Id).ToHashSet().SetEquals(Definitions.Requests[0].Targets.Select(target => target.TypeId))))
                throw new InvalidDataException("選択した定義とその参照先だけを含めてください。");
            if (Definitions.IsConfidential && !IsConfidential) throw new InvalidDataException("定義素材のマル秘フラグが一致しません。");
        }
        else throw new InvalidDataException("未対応の素材種類です。");
    }
}
