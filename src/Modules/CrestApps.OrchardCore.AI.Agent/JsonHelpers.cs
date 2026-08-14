using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.AI.Agent;

internal static class JsonHelpers
{
    /// <summary>
    /// Gets the JSON serializer options configured for content definition serialization.
    /// </summary>
    public static JsonSerializerOptions ContentDefinitionSerializerOptions = new(JOptions.Default)
    {
        ReferenceHandler = ReferenceHandler.Preserve,
    };
}
