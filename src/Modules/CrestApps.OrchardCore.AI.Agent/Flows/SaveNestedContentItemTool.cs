using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Settings;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.AI.Agent.Flows;

/// <summary>
/// Adds, updates or moves one content item nested in a content item's Flow, Bag or widgets list part, and saves the
/// parent through the content manager, as the content editor does.
/// </summary>
public sealed class SaveNestedContentItemTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveNestedContentItem";

    private const string FlowMetadataName = nameof(FlowMetadata);

    private const string WidgetMetadataName = "WidgetMetadata";

    private static readonly JsonMergeSettings _replaceArrays = new()
    {
        MergeArrayHandling = MergeArrayHandling.Replace,
    };

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
          "description": "The ID of the nested item to update or move, from listNestedContentItems. Leave it out to add a new nested item."
        },
        "contentItem": {
          "type": "object",
          "description": "The nested item's content, in the Orchard Core content item JSON shape. For a new nested item it must include ContentType. For an update, give only the parts and fields to change; arrays are replaced whole. Call getContentItemSchema for the nested content type first when it is available."
        },
        "part": {
          "type": "string",
          "description": "The name of the Flow, Bag or widgets list part that takes the nested item, for example FlowPart. Needed when the target has more than one such part. For a move, leave it out to keep the nested item in its list."
        },
        "zone": {
          "type": "string",
          "description": "For a widgets list part: the zone that takes the widget."
        },
        "parentNestedContentItemId": {
          "type": "string",
          "description": "The ID of the nested item that holds the target part, when the nested item goes inside another nested item. Leave it out when the target part is on the parent content item itself."
        },
        "position": {
          "type": "integer",
          "minimum": 0,
          "description": "The position in the target list, starting at 0. Leave it out to add at the end, or to keep the position of an updated item."
        },
        "isDraft": {
          "type": "boolean",
          "description": "Whether the parent content item is saved as a draft (default) or published at once."
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
    public override string Description => "Adds, updates or moves a single widget or contained item inside a content item's Flow, Bag or widget-zone part without resending the rest of the page. Takes the parent item, the target part (and zone or parent nested item), an optional position, and the nested item's content. The parent is saved as a draft unless isDraft is false.";

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

        if (!AgentToolArguments.TryReadJsonObject(arguments, "contentItem", out var payload))
        {
            return "Nothing was saved: contentItem must be a JSON object.";
        }

        var hasPosition = arguments.TryGetFirst<int>("position", out var position);

        if (hasPosition && position < 0)
        {
            return "Nothing was saved: position must be 0 or more.";
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

        var hasNestedId = arguments.TryGetFirstString("nestedContentItemId", out var nestedContentItemId);
        var hasPart = arguments.TryGetFirstString("part", out var partName);
        var hasZone = arguments.TryGetFirstString("zone", out var zone);
        var hasParent = arguments.TryGetFirstString("parentNestedContentItemId", out var parentNestedContentItemId);

        var contentDefinitionManager = services.GetRequiredService<IContentDefinitionManager>();
        var root = NestedContentItemTree.ToJson(services, contentItem);
        var entries = await NestedContentItemTree.GetEntriesAsync(contentDefinitionManager, root);
        var changedParts = new List<string>();

        NestedContentItemContainer target;
        JsonObject nestedNode;
        string action;
        string error;
        int newPosition;

        if (hasNestedId)
        {
            var entry = NestedContentItemTree.Find(entries, nestedContentItemId);

            if (entry is null)
            {
                return $"Nothing was saved: content item '{contentItemId}' has no nested item with the ID '{nestedContentItemId}'. Use listNestedContentItems to get the IDs.";
            }

            nestedNode = entry.Item;

            var nestedType = NestedContentItemTree.GetString(nestedNode, nameof(ContentItem.ContentType));
            var nestedDefinition = string.IsNullOrEmpty(nestedType)
                ? null
                : await contentDefinitionManager.GetTypeDefinitionAsync(nestedType);

            if (!await NestedContentItemTree.IsNestedItemAuthorizedAsync(services, nestedDefinition, CommonPermissions.EditContent, NestedContentItemTree.ToContentItem(services, nestedNode)))
            {
                return AIToolCallerAuthorization.NotAllowedMessage;
            }

            var updates = payload is not null;

            if (updates)
            {
                var payloadType = TakeString(payload, nameof(ContentItem.ContentType));

                if (payloadType is not null && !string.Equals(payloadType, nestedType, StringComparison.OrdinalIgnoreCase))
                {
                    return $"Nothing was saved: the nested item is a '{nestedType}' and its content type cannot be changed. Remove it and add a '{payloadType}' instead.";
                }

                if (nestedDefinition is null)
                {
                    return $"Nothing was saved: the content type '{nestedType}' of the nested item no longer exists.";
                }

                TakeString(payload, nameof(ContentItem.ContentItemId));
                payload[nameof(ContentItem.ContentType)] = nestedDefinition.Name;

                error = await ValidatePayloadAsync(services, nestedDefinition, payload, cancellationToken);

                if (error is not null)
                {
                    return error;
                }

                nestedNode.Merge(payload, _replaceArrays);
            }

            target = entry.Container;
            newPosition = entry.Position;

            var moves = hasPart || hasZone || hasParent || hasPosition;

            if (moves)
            {
                if (hasPart || hasZone || hasParent)
                {
                    (target, error) = await ResolveTargetAsync(contentDefinitionManager, root, entries, parentNestedContentItemId, partName, zone);

                    if (error is not null)
                    {
                        return error;
                    }

                    if (IsInside(target.Owner, nestedNode))
                    {
                        return "Nothing was saved: a nested item cannot be moved into itself or into an item nested in it.";
                    }

                    if (!ReferenceEquals(target.Items, entry.Container.Items) && nestedDefinition is not null)
                    {
                        error = NestedContentItemTree.GetNotAllowedReason(target, nestedDefinition);

                        if (error is not null)
                        {
                            return $"Nothing was saved: {error}";
                        }
                    }
                }

                var sameList = ReferenceEquals(target.Items, entry.Container.Items);

                entry.Container.Items.RemoveAt(entry.Position);

                newPosition = hasPosition
                    ? Math.Min(position, target.Items.Count)
                    : sameList
                        ? Math.Min(entry.Position, target.Items.Count)
                        : target.Items.Count;

                target.Items.Insert(newPosition, nestedNode);
                changedParts.Add(entry.Container.TopLevelPartName);
            }

            if (!updates && !moves)
            {
                return "Nothing was saved: give contentItem to change the nested item, or position, part, zone or parentNestedContentItemId to move it.";
            }

            changedParts.Add(target.TopLevelPartName);
            action = !moves
                ? "updated"
                : updates
                    ? "updatedAndMoved"
                    : "moved";
        }
        else
        {
            if (payload is null)
            {
                return "Nothing was saved: give contentItem with a ContentType to add a nested item, or nestedContentItemId to update or move one.";
            }

            var nestedType = TakeString(payload, nameof(ContentItem.ContentType));

            if (string.IsNullOrWhiteSpace(nestedType))
            {
                return "Nothing was saved: the contentItem of a new nested item must include ContentType.";
            }

            var nestedDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(nestedType.Trim());

            if (nestedDefinition is null)
            {
                return $"Nothing was saved: there is no content type named '{nestedType}'.";
            }

            (target, error) = await ResolveTargetAsync(contentDefinitionManager, root, entries, parentNestedContentItemId, partName, zone);

            if (error is not null)
            {
                return error;
            }

            error = NestedContentItemTree.GetNotAllowedReason(target, nestedDefinition);

            if (error is not null)
            {
                return $"Nothing was saved: {error}";
            }

            // A new nested item gets its own ID; an ID in the payload would be read as an existing content item.
            TakeString(payload, nameof(ContentItem.ContentItemId));
            payload[nameof(ContentItem.ContentType)] = nestedDefinition.Name;

            error = await ValidatePayloadAsync(services, nestedDefinition, payload, cancellationToken);

            if (error is not null)
            {
                return error;
            }

            var nestedItem = await services.GetRequiredService<ContentItemPreparationService>().PrepareAsync(
                nestedDefinition,
                payload,
                cancellationToken: cancellationToken);

            if (!await NestedContentItemTree.IsNestedItemAuthorizedAsync(services, nestedDefinition, CommonPermissions.EditContent, nestedItem))
            {
                return AIToolCallerAuthorization.NotAllowedMessage;
            }

            nestedNode = NestedContentItemTree.ToJson(services, nestedItem);
            AddMetadata(target, nestedNode);

            newPosition = hasPosition
                ? Math.Min(position, target.Items.Count)
                : target.Items.Count;

            target.Items.Insert(newPosition, nestedNode);
            changedParts.Add(target.TopLevelPartName);
            action = "added";
        }

        var (saved, saveError) = await NestedContentItemTree.SaveAsync(services, contentItem.ContentItemId, root, changedParts, isDraft);

        if (saveError is not null)
        {
            return saveError;
        }

        return AgentToolResults.Json(new
        {
            saved = true,
            action,
            contentItemId = saved.ContentItemId,
            nestedContentItemId = NestedContentItemTree.GetString(nestedNode, nameof(ContentItem.ContentItemId)),
            contentType = NestedContentItemTree.GetString(nestedNode, nameof(ContentItem.ContentType)),
            part = target.PartName,
            zone = target.Zone,
            position = newPosition,
            parentNestedContentItemId = target.OwnerId,
            published = saved.Published,
            note = isDraft
                ? "The change is in a draft of the parent content item. Publish the parent to make it live."
                : null,
        });
    }

    /// <summary>
    /// Finds the one list the arguments name: on the parent content item, or on a nested item when one is given.
    /// </summary>
    private static async Task<(NestedContentItemContainer Container, string Error)> ResolveTargetAsync(
        IContentDefinitionManager contentDefinitionManager,
        JsonObject root,
        List<NestedContentItemEntry> entries,
        string parentNestedContentItemId,
        string partName,
        string zone)
    {
        var owner = root;
        string ownerId = null;
        string topLevelPartName = null;
        var depth = 1;

        if (!string.IsNullOrEmpty(parentNestedContentItemId))
        {
            var parent = NestedContentItemTree.Find(entries, parentNestedContentItemId);

            if (parent is null)
            {
                return (null, $"Nothing was saved: there is no nested item with the ID '{parentNestedContentItemId}'. Use listNestedContentItems to get the IDs.");
            }

            owner = parent.Item;
            ownerId = NestedContentItemTree.GetString(parent.Item, nameof(ContentItem.ContentItemId));
            topLevelPartName = parent.Container.TopLevelPartName;
            depth = parent.Container.Depth + 1;
        }

        var containers = await NestedContentItemTree.GetContainersAsync(contentDefinitionManager, owner, ownerId, topLevelPartName, depth, true);
        var matches = containers
            .Where(container =>
                (string.IsNullOrEmpty(partName) || string.Equals(container.PartName, partName.Trim(), StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrEmpty(zone) || string.Equals(container.Zone, zone.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (matches.Count == 1)
        {
            return (matches[0], null);
        }

        var available = containers.Count == 0
            ? "It has no Flow, Bag or widgets list part."
            : $"Its lists are: {string.Join("; ", containers.Select(Describe))}.";

        return matches.Count == 0
            ? (null, $"Nothing was saved: the target has no list that matches the given part and zone. {available}")
            : (null, $"Nothing was saved: more than one list matches. Give the part, and the zone for a widgets list part. {available}");
    }

    private static string Describe(NestedContentItemContainer container)
    {
        return container.Zone is null
            ? $"part '{container.PartName}'"
            : $"part '{container.PartName}' zone '{container.Zone}'";
    }

    private static async Task<string> ValidatePayloadAsync(
        IServiceProvider services,
        ContentTypeDefinition definition,
        JsonObject payload,
        CancellationToken cancellationToken)
    {
        // The alignment and position parts are welded to a nested item, so the content type does not define them.
        var shape = payload.DeepClone().AsObject();

        shape.Remove(FlowMetadataName);
        shape.Remove(WidgetMetadataName);

        return await ContentItemPayloadCheck.GetErrorAsync(services, definition, shape, "nested content item", "contentItem", cancellationToken);
    }

    /// <summary>
    /// Gives a new nested item the part the editor welds to it: the alignment and size of a Flow widget, or the title
    /// and position settings of a zone widget.
    /// </summary>
    private static void AddMetadata(NestedContentItemContainer container, JsonObject nestedNode)
    {
        if (container.Kind == NestedContentItemTree.FlowKind && nestedNode[FlowMetadataName] is null)
        {
            nestedNode[FlowMetadataName] = JObject.FromObject(new FlowMetadata());
        }

        if (container.Kind == NestedContentItemTree.WidgetZoneKind && nestedNode[WidgetMetadataName] is null)
        {
            nestedNode[WidgetMetadataName] = new JsonObject
            {
                ["RenderTitle"] = false,
            };
        }
    }

    private static bool IsInside(JsonNode node, JsonNode ancestor)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes a property from the payload, whatever its casing, and returns its text.
    /// </summary>
    private static string TakeString(JsonObject payload, string name)
    {
        var key = payload
            .Select(property => property.Key)
            .FirstOrDefault(propertyName => string.Equals(propertyName, name, StringComparison.OrdinalIgnoreCase));

        if (key is null)
        {
            return null;
        }

        var value = NestedContentItemTree.GetString(payload, key);

        payload.Remove(key);

        return value;
    }
}
