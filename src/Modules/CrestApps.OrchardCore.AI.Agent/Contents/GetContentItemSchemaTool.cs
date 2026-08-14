using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

/// <summary>
/// AI tool that returns content-item JSON schemas for one or more Orchard Core content types.
/// </summary>
public sealed class GetContentItemSchemaTool : AIFunction
{
    /// <summary>
    /// The name constant.
    /// </summary>
    public const string TheName = "getContentItemSchema";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentTypes": {
          "description": "One or more Orchard Core content type names to return JSON schemas for. Provide either a single string value or an array of strings."
        }
      },
      "required": [
        "contentTypes"
      ],
      "additionalProperties": false
    }
    """);

    public override string Name => TheName;

    public override string Description => $"Returns the current content-item JSON schema for one or more Orchard Core content types. Call this immediately before '{CreateOrUpdateContentTool.TheName}' whenever that tool is available so the payload follows the exact schema contract for the parent content type and any nested content types.";

    public override JsonElement JsonSchema => _jsonSchema;

    /// <summary>
    /// Gets the additional properties for the AI function, such as strict mode configuration.
    /// </summary>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<GetContentItemSchemaTool>>();
        var contentDefinitionManager = arguments.Services.GetRequiredService<IContentDefinitionManager>();
        var contentItemSchemaService = arguments.Services.GetRequiredService<IContentItemSchemaService>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", TheName);
        }

        var contentTypes = GetRequestedContentTypes(arguments);

        if (contentTypes.Length == 0)
        {
            logger.LogWarning("AI tool '{ToolName}': Unable to find a contentTypes argument in the function arguments.", TheName);

            return "Unable to find a contentTypes argument in the function arguments.";
        }

        var schemas = new JsonObject();
        var missingContentTypes = new JsonArray();

        foreach (var contentType in contentTypes)
        {
            if (await contentDefinitionManager.GetTypeDefinitionAsync(contentType) is null)
            {
                missingContentTypes.Add(contentType);
                continue;
            }

            var schemaBuilder = await contentItemSchemaService.GetSchemaAsync(contentType, cancellationToken);

            if (schemaBuilder is null)
            {
                missingContentTypes.Add(contentType);
                continue;
            }

            var schemaNode = JsonNode.Parse(JsonSerializer.Serialize(schemaBuilder.Build()));

            schemas[contentType] = schemaNode;
        }

        if (schemas.Count == 0)
        {
            var missingMessage = string.Join(", ", missingContentTypes.Select(node => node?.GetValue<string>()));
            logger.LogWarning("AI tool '{ToolName}': No schema was found for the requested content types: {ContentTypes}.", TheName, missingMessage);

            return $"No schema was found for the requested content types: {missingMessage}.";
        }

        var result = new JsonObject
        {
            ["schemas"] = schemas,
        };

        if (missingContentTypes.Count > 0)
        {
            result["missingContentTypes"] = missingContentTypes;
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", TheName);
        }

        return result.ToJsonString();
    }

    private static string[] GetRequestedContentTypes(AIFunctionArguments arguments)
    {
        if (arguments.TryGetFirstString("contentTypes", out var singleContentType) &&
            !string.IsNullOrWhiteSpace(singleContentType))
        {
            return [singleContentType];
        }

        if (arguments.TryGetFirst("contentTypes", out var rawValue) &&
            rawValue is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.String)
            {
                var contentType = jsonElement.GetString();

                return string.IsNullOrWhiteSpace(contentType)
                    ? []
                    : [contentType];
            }

            if (jsonElement.ValueKind == JsonValueKind.Array)
            {
                return jsonElement
                    .EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }
        }

        if (arguments.TryGetFirstString("contentType", out var fallbackContentType) &&
            !string.IsNullOrWhiteSpace(fallbackContentType))
        {
            return [fallbackContentType];
        }

        return [];
    }
}
