using System.Text.Json;

namespace CrestApps.Core.Data.EntityCore.Services;

internal static class EntityCoreStoreSerializer
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, _jsonSerializerOptions);

    public static T Deserialize<T>(string payload)
        => JsonSerializer.Deserialize<T>(payload, _jsonSerializerOptions);
}
