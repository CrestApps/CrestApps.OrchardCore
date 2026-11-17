using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

/// <summary>
/// Discards the unpublished draft of a content item, as the Discard Draft action of the admin does.
/// </summary>
public sealed class DiscardContentItemDraftTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "discardContentItemDraft";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The ID of the content item (ContentItemId) whose draft is discarded."
        }
      },
      "required": [
        "contentItemId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Discards the unpublished draft of a content item so the published version becomes the latest again. Fails when the latest version is published; when the item was never published the item disappears from the lists.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (AgentToolAuthorization.GetCaller(services) is null)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("contentItemId", out var contentItemId))
        {
            return "The contentItemId argument is required.";
        }

        var contentManager = services.GetRequiredService<IContentManager>();
        var contentItem = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);

        if (contentItem is null)
        {
            return $"No content item with the ID '{contentItemId}' was found.";
        }

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.DeleteContent, contentItem))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (contentItem.Published)
        {
            return $"Nothing was discarded: the latest version of content item '{contentItemId}' is published, so it has no draft.";
        }

        var hasPublishedVersion = await contentManager.GetAsync(contentItemId, VersionOptions.Published) is not null;
        var discardedVersionId = contentItem.ContentItemVersionId;

        await contentManager.DiscardDraftAsync(contentItem);

        return AgentToolResults.Json(new
        {
            discarded = true,
            contentItemId,
            discardedVersionId,
            displayText = contentItem.DisplayText,
            publishedVersionIsLatest = hasPublishedVersion,
            note = hasPublishedVersion
                ? "The published version is the latest version again."
                : "The content item was never published, so it no longer shows in the lists.",
        });
    }
}
