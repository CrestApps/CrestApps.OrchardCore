using System.Text.Json;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> used to read Plaid's answers. Plaid uses snake_case JSON, so property names
/// map through the snake-case naming policy.
/// </summary>
internal static class PlaidJsonSerializerOptions
{
    /// <summary>
    /// Gets the default Plaid serializer options.
    /// </summary>
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
