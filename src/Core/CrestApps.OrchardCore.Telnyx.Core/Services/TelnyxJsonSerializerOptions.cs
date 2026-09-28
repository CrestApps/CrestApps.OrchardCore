using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> used to (de)serialize Telnyx REST payloads. Telnyx uses
/// snake_case JSON, so property names map through the snake-case naming policy.
/// </summary>
internal static class TelnyxJsonSerializerOptions
{
    /// <summary>
    /// Gets the default Telnyx serializer options.
    /// </summary>
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };
}
