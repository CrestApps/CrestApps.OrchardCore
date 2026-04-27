using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

/// <summary>
/// Represents the list content parts definitions tool.
/// </summary>
public sealed class ListContentPartsDefinitionsTool : AIFunction
{
    public const string TheName = "listContentPartsDefinitions";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {},
      "additionalProperties": false
    }
    """);

    public override string Name => TheName;

    public override string Description => "Retrieves the available content parts definitions which can be used to create content types.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<ListContentPartsDefinitionsTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", TheName);
        }

        var contentDefinitionManager = arguments.Services.GetRequiredService<IContentDefinitionManager>();

        var result = JsonSerializer.Serialize(await contentDefinitionManager.ListPartDefinitionsAsync(), JsonHelpers.ContentDefinitionSerializerOptions);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", TheName);
        }

        return result;
    }
}
