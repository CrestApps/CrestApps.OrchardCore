using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

public sealed class PublishContentTool : AIFunction
{
    public const string TheName = "publishContentItem";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "contentItemId": {
              "type": "string",
              "description": "The unique identifier of the content item, represented as a string (ContentItemId)."
            }
          },
          "required": ["contentItemId"],
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Publishes the draft version of a content item, making it the active published version.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<PublishContentTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", TheName);
        }

        var contentManager = arguments.Services.GetRequiredService<IContentManager>();

        if (!arguments.TryGetFirstString("contentItemId", out var contentItemId))
        {
            logger.LogWarning("AI tool '{ToolName}': Unable to find a contentItemId argument in the function arguments.", TheName);

            return "Unable to find a contentItemId argument in the function arguments.";
        }

        var contentItem = await contentManager.GetAsync(contentItemId, VersionOptions.Draft);

        if (contentItem is null)
        {
            logger.LogWarning("AI tool '{ToolName}': Unable to find a content item with ContentItemId '{ContentItemId}'.", TheName, contentItemId);

            return $"Unable to find a content item that match the ContentItemId: {contentItemId}";
        }

        await contentManager.PublishAsync(contentItem);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", TheName);
        }

        return "Content item was successfully published";
    }
}
