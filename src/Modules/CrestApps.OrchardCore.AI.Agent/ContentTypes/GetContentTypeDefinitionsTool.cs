using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

public sealed class GetContentTypeDefinitionsTool : AIFunction
{
    public const string TheName = "getContentTypeDefinition";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "name": {
              "type": "string",
              "description": "The name of the content type for which to retrieve the definitions."
            }
          },
          "required": ["name"],
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Retrieves the content type definition for a given content type.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<GetContentTypeDefinitionsTool>>();
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", TheName);
        }

        var contentDefinitionManager = arguments.Services.GetRequiredService<IContentDefinitionManager>();

        if (!arguments.TryGetFirstString("name", out var name))
        {
            logger.LogWarning("AI tool '{ToolName}' failed: missing 'name' argument.", TheName);

            return "Unable to find a name argument in the function arguments.";
        }

        var definition = await contentDefinitionManager.GetTypeDefinitionAsync(name);

        if (definition is null)
        {
            logger.LogWarning("AI tool '{ToolName}' could not find a type definition matching the name '{ContentType}'.", TheName, name);

            return $"Unable to find a type definition that match the name: {name}";
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", TheName);
        }

        return JsonSerializer.Serialize(definition, JsonHelpers.ContentDefinitionSerializerOptions);
    }
}
