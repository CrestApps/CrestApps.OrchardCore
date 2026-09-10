using System.Text.Json;

namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Provides JSON serialization options for Dialpad provider payloads.
/// </summary>
public static class DialpadJsonSerializerOptions
{
    /// <summary>
    /// Gets the serializer options used for Dialpad request and response payloads.
    /// </summary>
    public static JsonSerializerOptions Default { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new DialpadCallEventJsonConverter(),
        },
    };
}
