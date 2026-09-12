namespace CircleSpaceCoordinator.Desktop.Core.Interaction;

using CircleSpaceCoordinator.Core.Geometry;
using CircleSpaceCoordinator.Core.Model;
using CircleSpaceCoordinator.Desktop.Core.Persistence;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public static class SpaceTypeFactory
{
    public static DeskType Create(SpaceTypeDefinition definition)
    {
        new SpaceDefinitionCatalog([definition], []).Validate();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(definition)))).ToLowerInvariant();
        return new DeskType($"space-{definition.Id}-{hash}", definition.Name,
            definition.Cells.Select(c => new GridPosition(c.X, c.Y)).ToArray())
        {
            Space = new SpaceTypeDetails(definition.Id, definition.Kind, definition.Width, definition.Height,
                definition.Cells.Select(c => new SpaceAreaCell(c.X, c.Y, c.Area)).ToArray(), definition.Edges.ToArray()),
        };
    }
}
