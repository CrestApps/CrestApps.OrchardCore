using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

/// <summary>
/// Restores an earlier version of a content item as a new draft, with the service call the audit trail's restore action
/// uses.
/// </summary>
public sealed class RestoreContentItemVersionTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "restoreContentItemVersion";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentItemVersionId": {
          "type": "string",
          "description": "The ID of the version to restore (ContentItemVersionId), as listed by listContentItemVersions."
        }
      },
      "required": [
        "contentItemVersionId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Restores an earlier version of a content item as a new draft, keeping the published version live. Takes a content item version ID from listContentItemVersions; the current draft, if any, stops being the latest. Publish the item afterwards to make it live.";

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

        if (!arguments.TryGetFirstString("contentItemVersionId", out var contentItemVersionId))
        {
            return "The contentItemVersionId argument is required.";
        }

        var contentManager = services.GetRequiredService<IContentManager>();
        var version = await contentManager.GetVersionAsync(contentItemVersionId);

        if (version is null)
        {
            return $"No content item version with the ID '{contentItemVersionId}' was found. Use listContentItemVersions to get the version IDs of a content item.";
        }

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.PublishContent, version))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (version.Latest)
        {
            return $"Nothing was restored: version '{contentItemVersionId}' is already the latest version of content item '{version.ContentItemId}'.";
        }

        // The restore turns the item it is given into the new draft. Give it a copy, as the audit trail gives it a
        // snapshot, so the stored version stays in the history and a published version stays published.
        var snapshot = await contentManager.LoadAsync(version.Clone());
        var result = await contentManager.RestoreAsync(snapshot);

        if (!result.Succeeded)
        {
            var errors = string.Join(" ", result.Errors.Select(error => error.ErrorMessage));

            return $"Nothing was restored: version '{contentItemVersionId}' is not valid. {errors}".TrimEnd();
        }

        return AgentToolResults.Json(new
        {
            restored = true,
            contentItemId = snapshot.ContentItemId,
            restoredFromVersionId = contentItemVersionId,
            draftVersionId = snapshot.ContentItemVersionId,
            displayText = snapshot.DisplayText,
            note = "The restored content is a draft. Publish the content item to make it live.",
        });
    }
}
