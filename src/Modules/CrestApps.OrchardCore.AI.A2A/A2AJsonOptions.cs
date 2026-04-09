using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.AI.A2A;

internal static class A2AJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
