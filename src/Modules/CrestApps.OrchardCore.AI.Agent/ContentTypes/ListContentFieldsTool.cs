using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

/// <summary>
/// Represents the list content fields tool.
/// </summary>
public sealed class ListContentFieldsTool : AIFunction
{
    public const string TheName = "listContentFieldDefinitions";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {},
      "additionalProperties": false
    }
    """);

    public override string Name => TheName;

    public override string Description => "Retrieves the available content fields which can be used to create content parts.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<ListContentFieldsTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", TheName);
        }

        var contentMetadataService = arguments.Services.GetRequiredService<ContentMetadataService>();

        var fieldTypes = await contentMetadataService.GetFieldsAsync();

        var result = JsonSerializer.Serialize(fieldTypes.Select(fieldType => fieldType.Name));

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", TheName);
        }

        return result;
    }
}
