using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.OpenAI.Services;

internal sealed class OpenAIConnectionOptionsConfiguration : IConfigureOptions<OpenAIConnectionOptions>
{
    private readonly IShellConfiguration _shellConfiguration;

    public OpenAIConnectionOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public void Configure(OpenAIConnectionOptions options)
    {
        var jsonNode = _shellConfiguration.GetSection("CrestApps_OpenAI__Connections").AsJsonNode();

        var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonNode);

        var connectionElements = JsonObject.Create(jsonElement, new JsonNodeOptions()
        {
            PropertyNameCaseInsensitive = true,
        });

        if (connectionElements == null)
        {
            return;
        }

        foreach (var connectionElement in connectionElements)
        {
            if (connectionElement.Value is JsonArray items)
            {
                // If the value is an array, deserialize it to a list of JsonObjects.
                options.Connections[connectionElement.Key] = JsonSerializer.Deserialize<OpenAIConnectionEntry[]>(items);
            }
            else if (connectionElement.Value is JsonObject jObject)
            {
                // If the value is a single object, create a list with that single object.
                options.Connections[connectionElement.Key] =
                [
                    JsonSerializer.Deserialize<OpenAIConnectionEntry>(jObject),
                ];
            }
        }
    }
}
