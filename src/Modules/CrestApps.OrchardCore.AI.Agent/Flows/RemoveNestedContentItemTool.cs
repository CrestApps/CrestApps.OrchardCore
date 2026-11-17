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
/// Deletes one content item nested in a content item's Flow, Bag or widgets list part, and saves the parent through the
/// content manager, as the content editor does.
/// </summary>
public sealed class RemoveNestedContentItemTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "removeNestedContentItem";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The ID of the parent content item (ContentItemId), the page that holds the nested item."
        },
        "nestedContentItemId": {
          "type": "string",
          "description": "The ID of the nested item to delete, from listNestedContentItems."
        },
        "isDraft": {
          "type": "boolean",
          "description": "Whether the parent content item is saved as a draft (default) or published at once."
        }
      },
      "required": [
        "contentItemId",
        "nestedContentItemId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes one widget or contained item, and everything nested inside it, from a content item's Flow, Bag or widget-zone part. The parent is saved as a draft unless isDraft is false.";

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

        if (!arguments.TryGetFirstString("nestedContentItemId", out var nestedContentItemId))
        {
            return "The nestedContentItemId argument is required.";
        }

        var isDraft = arguments.GetFirstValueOrDefault("isDraft", true);
        var contentItem = await services.GetRequiredService<IContentManager>().GetAsync(contentItemId, VersionOptions.Latest);

        if (contentItem is null)
        {
            return $"No content item with the ID '{contentItemId}' was found.";
        }

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.EditContent, contentItem) ||
            (!isDraft && !await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.PublishContent, contentItem)))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var contentDefinitionManager = services.GetRequiredService<IContentDefinitionManager>();
        var root = NestedContentItemTree.ToJson(services, contentItem);
        var entries = await NestedContentItemTree.GetEntriesAsync(contentDefinitionManager, root);
        var entry = NestedContentItemTree.Find(entries, nestedContentItemId);

        if (entry is null)
        {
            return $"Nothing was removed: content item '{contentItemId}' has no nested item with the ID '{nestedContentItemId}'. Use listNestedContentItems to get the IDs.";
        }

        var nestedType = NestedContentItemTree.GetString(entry.Item, nameof(ContentItem.ContentType));
        var nestedDefinition = string.IsNullOrEmpty(nestedType)
            ? null
            : await contentDefinitionManager.GetTypeDefinitionAsync(nestedType);

        if (!await NestedContentItemTree.IsNestedItemAuthorizedAsync(services, nestedDefinition, CommonPermissions.DeleteContent, NestedContentItemTree.ToContentItem(services, entry.Item)))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        // The items nested in the removed item go with it; they follow it in the outline at a greater depth.
        var removedChildren = entries
            .SkipWhile(candidate => !ReferenceEquals(candidate, entry))
            .Skip(1)
            .TakeWhile(candidate => candidate.Container.Depth > entry.Container.Depth)
            .Count();

        entry.Container.Items.RemoveAt(entry.Position);

        var (saved, saveError) = await NestedContentItemTree.SaveAsync(services, contentItem.ContentItemId, root, [entry.Container.TopLevelPartName], isDraft);

        if (saveError is not null)
        {
            return saveError.Replace("Nothing was saved", "Nothing was removed", StringComparison.Ordinal);
        }

        return AgentToolResults.Json(new
        {
            removed = true,
            contentItemId = saved.ContentItemId,
            nestedContentItemId = NestedContentItemTree.GetString(entry.Item, nameof(ContentItem.ContentItemId)),
            contentType = nestedType,
            displayText = NestedContentItemTree.GetString(entry.Item, nameof(ContentItem.DisplayText)),
            part = entry.Container.PartName,
            zone = entry.Container.Zone,
            removedNestedItems = removedChildren,
            published = saved.Published,
            note = isDraft
                ? "The change is in a draft of the parent content item. Publish the parent to make it live."
                : null,
        });
    }
}
