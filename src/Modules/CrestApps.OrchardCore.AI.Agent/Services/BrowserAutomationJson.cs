using System.Text.Json;

namespace CrestApps.OrchardCore.AI.Agent.Services;

internal static class BrowserAutomationJson
{
    public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    public static string Serialize(object value)
        => JsonSerializer.Serialize(value, SerializerOptions);

    public static JsonElement ParseJson(string json)
        => JsonSerializer.Deserialize<JsonElement>(json, SerializerOptions);
}

