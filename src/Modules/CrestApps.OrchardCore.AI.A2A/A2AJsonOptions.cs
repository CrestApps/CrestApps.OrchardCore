using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.AI.A2A;

internal static class A2AJsonOptions
{
    /// <summary>
    /// Gets the default JSON serializer options for A2A protocol communication.
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
