using System.Text.Json;

namespace CrestApps.OrchardCore.DialPad.Services;

/// <summary>
/// Provides JSON serialization options for DialPad provider payloads.
/// </summary>
public static class DialPadJsonSerializerOptions
{
    /// <summary>
    /// Gets the serializer options used for DialPad request and response payloads.
    /// </summary>
    public static JsonSerializerOptions Default { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };
}
