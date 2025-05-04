using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.AI.Agent;

internal static class JsonHelpers
{
    public static JsonSerializerOptions ContentDefinitionSerializerOptions = new(JOptions.Default)
    {
        ReferenceHandler = ReferenceHandler.Preserve,
    };

    public static JsonSerializerOptions RecipeSerializerOptions = new(JOptions.Default)
    {
        PropertyNameCaseInsensitive = true,
    };
}
