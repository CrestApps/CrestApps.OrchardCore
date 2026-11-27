using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Settings;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

internal sealed class ContentItemPreparationService
{
    private static readonly JsonMergeSettings _updateJsonMergeSettings = new()
    {
        MergeArrayHandling = MergeArrayHandling.Replace,
    };

    private static readonly string[] _fallbackNestedItemPropertyNames =
    [
        "ContentItems",
        "Widgets",
    ];

    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly DocumentJsonSerializerOptions _jsonSerializerOptions;
    private readonly IEnumerable<IContentSchemaDefinition> _schemaDefinitions;

    private ILookup<string, IContainedContentPartSchemaDefinition> _containedPartSchemaDefinitions;

    public ContentItemPreparationService(
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        IEnumerable<IContentSchemaDefinition> schemaDefinitions,
        IOptions<DocumentJsonSerializerOptions> jsonSerializerOptions)
    {
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _jsonSerializerOptions = jsonSerializerOptions.Value;
        _schemaDefinitions = schemaDefinitions;
    }

    public async ValueTask<ContentItem> PrepareAsync(
        ContentTypeDefinition contentDefinition,
        JsonObject contentItemNode,
        ContentItem existingContentItem = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentDefinition);
        ArgumentNullException.ThrowIfNull(contentItemNode);

        var contentType = contentDefinition.Name;

        // Only the item the caller was authorized for is updated. Any other item, a nested one included, is built new:
        // loading one by the id the payload names would add a draft to someone else's item and merge the payload into it.
        var contentItem = existingContentItem ?? await _contentManager.NewAsync(contentType);

        await PrepareNestedContentItemsAsync(contentDefinition, contentItemNode, cancellationToken);

        var payloadModel = contentItemNode.Deserialize<ContentItem>(_jsonSerializerOptions.SerializerOptions);

        if (payloadModel is null)
        {
            throw new InvalidOperationException($"The content item payload for '{contentType}' could not be mapped to a content item.");
        }

        if (existingContentItem is not null)
        {
            contentItem.Merge(payloadModel, _updateJsonMergeSettings);
        }
        else
        {
            contentItem.Merge(payloadModel);
        }

        return contentItem;
    }

    private async ValueTask PrepareNestedContentItemsAsync(
        ContentTypeDefinition contentDefinition,
        JsonObject contentItemNode,
        CancellationToken cancellationToken)
    {
        foreach (var container in GetContainedContentContainers(contentDefinition, contentItemNode))
        {
            if (!TryGetPropertyValue(contentItemNode, container.PartName, out var partNode) ||
                partNode is not JsonObject partObject ||
                !TryGetPropertyValue(partObject, container.NestedItemsPropertyName, out var nestedItemsNode) ||
                nestedItemsNode is not JsonArray nestedItems)
            {
                continue;
            }

            for (var index = 0; index < nestedItems.Count; index++)
            {
                if (nestedItems[index] is not JsonObject nestedItemNode)
                {
                    continue;
                }

                var nestedContentDefinition = await ResolveContentDefinitionAsync(nestedItemNode);
                var nestedContentItem = await PrepareAsync(
                    nestedContentDefinition,
                    nestedItemNode,
                    cancellationToken: cancellationToken);

                nestedItems[index] = JsonSerializer.SerializeToNode(
                    nestedContentItem,
                    _jsonSerializerOptions.SerializerOptions);
            }
        }
    }

    private List<ContainedContentContainer> GetContainedContentContainers(
        ContentTypeDefinition contentDefinition,
        JsonObject contentItemNode)
    {
        var containers = new List<ContainedContentContainer>();

        _containedPartSchemaDefinitions = _schemaDefinitions
            .Where(definition => definition is IContainedContentPartSchemaDefinition && !string.IsNullOrWhiteSpace(definition.Name))
            .ToLookup(
                definition => definition.Name,
                definition => (IContainedContentPartSchemaDefinition)definition,
                StringComparer.OrdinalIgnoreCase);

        foreach (var part in contentDefinition.Parts ?? [])
        {
            if (string.IsNullOrWhiteSpace(part.Name) || string.IsNullOrWhiteSpace(part.PartDefinition?.Name))
            {
                continue;
            }

            var recipeSchemaDefinitions = _containedPartSchemaDefinitions[part.PartDefinition.Name];

            if (recipeSchemaDefinitions.Any())
            {
                foreach (var definition in recipeSchemaDefinitions)
                {
                    containers.Add(new ContainedContentContainer(part.Name, definition.NestedItemsPropertyName));
                }

                continue;
            }

            if (!TryGetPropertyValue(contentItemNode, part.Name, out var partNode) ||
                partNode is not JsonObject partObject ||
                !IsFallbackContainerPart(part.PartDefinition.Name))
            {
                continue;
            }

            foreach (var nestedItemPropertyName in _fallbackNestedItemPropertyNames)
            {
                if (TryGetPropertyValue(partObject, nestedItemPropertyName, out var nestedItemsNode) &&
                    nestedItemsNode is JsonArray)
                {
                    containers.Add(new ContainedContentContainer(part.Name, nestedItemPropertyName));
                }
            }
        }

        return containers;
    }

    private async ValueTask<ContentTypeDefinition> ResolveContentDefinitionAsync(JsonObject contentItemNode)
    {
        // A nested item names its type: it is built new, so it is never read from the item an id names.
        var contentType = GetStringPropertyValue(contentItemNode, nameof(ContentItem.ContentType));

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new InvalidOperationException("Nested content items must include a valid 'ContentType' value.");
        }

        var contentDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

        if (contentDefinition is null)
        {
            throw new InvalidOperationException($"Invalid nested content type '{contentType}'.");
        }

        return contentDefinition;
    }

    private static bool IsFallbackContainerPart(string partDefinitionName) =>
        string.Equals(partDefinitionName, "BagPart", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(partDefinitionName, "FlowPart", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(partDefinitionName, "ListPart", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(partDefinitionName, "WidgetsListPart", StringComparison.OrdinalIgnoreCase);

    private static string GetStringPropertyValue(JsonObject jsonObject, string propertyName)
    {
        if (!TryGetPropertyValue(jsonObject, propertyName, out var valueNode))
        {
            return null;
        }

        return valueNode?.GetValue<string>();
    }

    private static bool TryGetPropertyValue(JsonObject jsonObject, string propertyName, out JsonNode value)
    {
        foreach (var property in jsonObject)
        {
            if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;

                return true;
            }
        }

        value = null;

        return false;
    }

    private sealed record ContainedContentContainer(string PartName, string NestedItemsPropertyName);
}
