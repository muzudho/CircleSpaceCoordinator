using System.Text.Json;
using System.Text.Json.Serialization;
using CircleSpaceCoordinator.Engine.Contracts.V1;
using CircleSpaceCoordinator.Engine.Model;

internal static class ApiCompatibility
{
    public static SortedDictionary<string, string> Capture()
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var service in EnginesReflection.Descriptor.Services)
        foreach (var method in service.Methods)
            result[$"rpc/{service.FullName}/{method.Name}"] = $"{method.InputType.FullName}:{method.OutputType.FullName}:{method.IsClientStreaming}:{method.IsServerStreaming}";
        foreach (var message in EnginesReflection.Descriptor.MessageTypes)
        foreach (var field in message.Fields.InFieldNumberOrder())
            result[$"field/{message.FullName}/{field.FieldNumber}"] = $"{field.Name}:{field.FieldType}:{field.IsRepeated}:{field.ContainingOneof?.Name}:{(field.FieldType == Google.Protobuf.Reflection.FieldType.Message ? field.MessageType.FullName : "")}";
        foreach (var attribute in typeof(EditorOperation).GetCustomAttributes(typeof(JsonDerivedTypeAttribute), false).Cast<JsonDerivedTypeAttribute>())
        foreach (var property in attribute.DerivedType.GetProperties())
            result[$"operation/{attribute.TypeDiscriminator}/{property.Name}"] = TypeName(property.PropertyType);
        return result;
    }

    public static void Verify()
    {
        var baseline = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "api-v1-baseline.json")))!;
        var current = Capture();
        foreach (var (key, value) in baseline)
            if (!current.TryGetValue(key, out var actual) || actual != value)
                throw new Exception($"Breaking v1 contract change: {key}. Introduce a new API version instead of rewriting the baseline.");
    }
    private static string TypeName(Type type) => type.IsGenericType
        ? type.GetGenericTypeDefinition().Name + "<" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + ">"
        : type.FullName!;
}
