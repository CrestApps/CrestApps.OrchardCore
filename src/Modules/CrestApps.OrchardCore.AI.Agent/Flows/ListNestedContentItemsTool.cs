using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.AI.Agent.Flows;

/// <summary>
/// Returns the outline of the content items nested in a content item's Flow, Bag and widgets list parts.
/// </summary>
public sealed class ListNestedContentItemsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listNestedContentItems";

    private const int MaxItems = 200;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The ID of the content item (ContentItemId) whose nested items are listed."
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
    public override string Description => "Returns the outline of the items nested in a content item's Flow, Bag and widget-zone parts: for each, its part, zone, position, nested item ID, content type and title, with children after their parent at a greater depth. Use it to locate a widget or contained item before changing it. It reads the latest version of the content item.";

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

        var contentItem = await services.GetRequiredService<IContentManager>().GetAsync(contentItemId, VersionOptions.Latest);

        if (contentItem is null)
        {
            return $"No content item with the ID '{contentItemId}' was found.";
        }

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.ViewContent, contentItem))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var contentDefinitionManager = services.GetRequiredService<IContentDefinitionManager>();
        var root = NestedContentItemTree.ToJson(services, contentItem);
        var entries = await NestedContentItemTree.GetEntriesAsync(contentDefinitionManager, root);
        var containers = await NestedContentItemTree.GetContainersAsync(contentDefinitionManager, root, null, null, 1, true);

        return AgentToolResults.Json(new
        {
            contentItemId = contentItem.ContentItemId,
            contentType = contentItem.ContentType,
            displayText = contentItem.DisplayText,
            published = contentItem.Published,
            parts = containers.Select(container => new
            {
                part = container.PartName,
                kind = container.Kind,
                zone = container.Zone,
                count = container.Items.Count,
            }),
            items = entries.Take(MaxItems).Select(entry => new
            {
                nestedContentItemId = NestedContentItemTree.GetString(entry.Item, nameof(ContentItem.ContentItemId)),
                contentType = NestedContentItemTree.GetString(entry.Item, nameof(ContentItem.ContentType)),
                displayText = NestedContentItemTree.GetString(entry.Item, nameof(ContentItem.DisplayText)),
                part = entry.Container.PartName,
                kind = entry.Container.Kind,
                zone = entry.Container.Zone,
                position = entry.Position,
                depth = entry.Container.Depth,
                parentNestedContentItemId = entry.Container.OwnerId,
            }),
            total = entries.Count,
            truncated = entries.Count > MaxItems ? true : (bool?)null,
        });
    }
}
