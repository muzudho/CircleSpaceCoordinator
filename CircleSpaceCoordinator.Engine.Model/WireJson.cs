namespace CircleSpaceCoordinator.Engine.Model;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CircleSpaceCoordinator.Core.Geometry;

// Versioned JSON payloads inside protobuf; no runtime type names or arbitrary type loading.
public static class WireJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new JsonException("A payload is required.");

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            IncludeFields = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new SetConverterFactory());
        options.Converters.Add(new GridPositionConverter());
        return options;
    }

    private sealed class SetConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlySet<>);
        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(SetConverter<>).MakeGenericType(type.GetGenericArguments()))!;
    }
    private sealed class SetConverter<T> : JsonConverter<IReadOnlySet<T>>
    {
        public override IReadOnlySet<T> Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            JsonSerializer.Deserialize<HashSet<T>>(ref reader, options) ?? throw new JsonException("A set is required.");
        public override void Write(Utf8JsonWriter writer, IReadOnlySet<T> value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.ToArray(), options);
    }
    private sealed class GridPositionConverter : JsonConverter<GridPosition>
    {
        public override GridPosition Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return new GridPosition(document.RootElement.GetProperty("x").GetInt32(), document.RootElement.GetProperty("y").GetInt32());
        }
        public override void Write(Utf8JsonWriter writer, GridPosition value, JsonSerializerOptions options)
        {
            writer.WriteStartObject(); writer.WriteNumber("x", value.X); writer.WriteNumber("y", value.Y); writer.WriteEndObject();
        }
        public override GridPosition ReadAsPropertyName(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var parts = reader.GetString()!.Split(',');
            if (parts.Length != 2) throw new JsonException("Expected x,y coordinate key.");
            return new GridPosition(int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture));
        }
        public override void WriteAsPropertyName(Utf8JsonWriter writer, GridPosition value, JsonSerializerOptions options) =>
            writer.WritePropertyName(FormattableString.Invariant($"{value.X},{value.Y}"));
    }
}
