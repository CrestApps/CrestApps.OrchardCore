using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.AI.Agents;

internal static class ToolHelpers
{
    public static string GetStringValue(object value)
    {
        if (value is JsonElement jsonElement)
        {
            return jsonElement.GetString();
        }

        return value?.ToString() ?? string.Empty;
    }

    public static IEnumerable<string> GetStringValues(object value)
    {
        if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();

            foreach (var item in jsonElement.EnumerateArray())
            {
                list.Add(item.GetString());
            }

            return list;
        }

        if (value is IEnumerable<object> objArray)
        {
            return objArray.Select(GetStringValue);
        }

        // Fallback for a single string value or invalid input
        var single = GetStringValue(value);

        return string.IsNullOrWhiteSpace(single)
            ? []
            : new List<string> { single };
    }
}

internal static class JsonHelpers
{
    public static JsonSerializerOptions ContentDefinitionSerializerOptions = new(JOptions.Default)
    {
        ReferenceHandler = ReferenceHandler.Preserve,
    };
}
