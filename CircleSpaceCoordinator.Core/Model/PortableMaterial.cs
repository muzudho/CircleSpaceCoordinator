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
    public SpaceDefinitionCatalog? Definitions { get; init; }
    public KnowledgeVenue? Venue { get; init; }

    public void Validate()
    {
        Credits?.Validate();
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name))
            throw new InvalidDataException("素材のIDと名前が必要です。");
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
