using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Flows.Models;
using OrchardCore.Json;
using OrchardCore.Security.Permissions;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Agent.Flows;

/// <summary>
/// Finds and changes the content items nested in a content item's Flow, Bag and widgets list parts. The nested items
/// are stored inside the parent item, so the tools work on the parent's JSON and save the parent through the content
/// manager, as the content editor does.
/// </summary>
internal static class NestedContentItemTree
{
    /// <summary>
    /// The kind of a list of widgets in a Flow part.
    /// </summary>
    public const string FlowKind = "flow";

    /// <summary>
    /// The kind of a list of items in a Bag part.
    /// </summary>
    public const string BagKind = "bag";

    /// <summary>
    /// The kind of a list of widgets in one zone of a widgets list part.
    /// </summary>
    public const string WidgetZoneKind = "widgetZone";

    private const int MaxDepth = 10;

    private const string WidgetStereotype = "Widget";

    private static readonly JsonMergeSettings _replaceArrays = new()
    {
        MergeArrayHandling = MergeArrayHandling.Replace,
    };

    /// <summary>
    /// Gets the JSON of a content item, as a copy the caller can change.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="contentItem">The content item.</param>
    /// <returns>The JSON of the content item.</returns>
    public static JsonObject ToJson(IServiceProvider services, ContentItem contentItem)
        => JsonSerializer.SerializeToNode(contentItem, GetSerializerOptions(services)).AsObject();

    /// <summary>
    /// Reads a content item from its JSON.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="node">The JSON of the content item.</param>
    /// <returns>The content item.</returns>
    public static ContentItem ToContentItem(IServiceProvider services, JsonObject node)
        => node.Deserialize<ContentItem>(GetSerializerOptions(services));

    /// <summary>
    /// Gets a string property of a JSON object.
    /// </summary>
    /// <param name="node">The JSON object.</param>
    /// <param name="name">The name of the property.</param>
    /// <returns>The value, or <see langword="null"/> when the property is missing or is not a string.</returns>
    public static string GetString(JsonObject node, string name)
    {
        return node.TryGetPropertyValue(name, out var value) && value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
            ? text
            : null;
    }

    /// <summary>
    /// Lists every nested content item of a content item, in outline order: each item is followed by the items nested in it.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="root">The JSON of the top content item.</param>
    /// <returns>The nested content items.</returns>
    public static async Task<List<NestedContentItemEntry>> GetEntriesAsync(IContentDefinitionManager contentDefinitionManager, JsonObject root)
    {
        var entries = new List<NestedContentItemEntry>();

        await AddEntriesAsync(contentDefinitionManager, root, null, null, 1, entries);

        return entries;
    }

    /// <summary>
    /// Gets the lists of nested content items that one content item owns.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="owner">The JSON of the content item that owns the lists.</param>
    /// <param name="ownerId">The ID of the owner when it is a nested content item, otherwise <see langword="null"/>.</param>
    /// <param name="topLevelPartName">The part of the top content item the owner is in, or <see langword="null"/> for the top content item.</param>
    /// <param name="depth">The depth of the items of the lists.</param>
    /// <param name="create">Whether the lists the content type allows but the JSON does not have yet are added to the JSON.</param>
    /// <returns>The lists.</returns>
    public static async Task<List<NestedContentItemContainer>> GetContainersAsync(
        IContentDefinitionManager contentDefinitionManager,
        JsonObject owner,
        string ownerId,
        string topLevelPartName,
        int depth,
        bool create)
    {
        var containers = new List<NestedContentItemContainer>();
        var contentType = GetString(owner, nameof(ContentItem.ContentType));
        var definition = string.IsNullOrEmpty(contentType)
            ? null
            : await contentDefinitionManager.GetTypeDefinitionAsync(contentType);

        foreach (var typePart in definition?.Parts ?? [])
        {
            var (kind, propertyName) = typePart.PartDefinition?.Name switch
            {
                "FlowPart" => (FlowKind, "Widgets"),
                "BagPart" => (BagKind, "ContentItems"),
                "WidgetsListPart" => (WidgetZoneKind, "Widgets"),
                _ => (null, null),
            };

            if (kind is null)
            {
                continue;
            }

            if (owner[typePart.Name] is not JsonObject part)
            {
                if (!create)
                {
                    continue;
                }

                part = [];
                owner[typePart.Name] = part;
            }

            if (kind == WidgetZoneKind)
            {
                if (part[propertyName] is not JsonObject zones)
                {
                    if (!create)
                    {
                        continue;
                    }

                    zones = [];
                    part[propertyName] = zones;
                }

                if (create)
                {
                    foreach (var zoneName in GetZones(typePart).Where(name => zones[name] is not JsonArray))
                    {
                        zones[zoneName] = new JsonArray();
                    }
                }

                foreach (var zone in zones)
                {
                    if (zone.Value is JsonArray widgets)
                    {
                        containers.Add(CreateContainer(kind, typePart, zone.Key, widgets, owner, ownerId, topLevelPartName, depth));
                    }
                }

                continue;
            }

            if (part[propertyName] is not JsonArray items)
            {
                if (!create)
                {
                    continue;
                }

                items = [];
                part[propertyName] = items;
            }

            containers.Add(CreateContainer(kind, typePart, null, items, owner, ownerId, topLevelPartName, depth));
        }

        return containers;
    }

    /// <summary>
    /// Finds a nested content item by its ID.
    /// </summary>
    /// <param name="entries">The nested content items.</param>
    /// <param name="nestedContentItemId">The ID of the nested content item.</param>
    /// <returns>The nested content item, or <see langword="null"/> when there is none with that ID.</returns>
    public static NestedContentItemEntry Find(IEnumerable<NestedContentItemEntry> entries, string nestedContentItemId)
    {
        return entries.FirstOrDefault(entry => string.Equals(
            GetString(entry.Item, nameof(ContentItem.ContentItemId)),
            nestedContentItemId,
            StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Says why a content type cannot be put in a list, from the settings of the part that holds the list.
    /// </summary>
    /// <param name="container">The list.</param>
    /// <param name="definition">The content type of the nested content item.</param>
    /// <returns>The reason, or <see langword="null"/> when the content type is allowed.</returns>
    public static string GetNotAllowedReason(NestedContentItemContainer container, ContentTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(definition);

        if (container.Kind == BagKind)
        {
            var settings = container.TypePart.GetSettings<BagPartSettings>();

            if (settings.ContainedStereotypes is { Length: > 0 })
            {
                return settings.ContainedStereotypes.Any(stereotype => definition.StereotypeEquals(stereotype, StringComparison.OrdinalIgnoreCase))
                    ? null
                    : $"The part '{container.PartName}' takes content types of these stereotypes only: {string.Join(", ", settings.ContainedStereotypes)}.";
            }

            if (settings.ContainedContentTypes is not { Length: > 0 })
            {
                return $"The part '{container.PartName}' has no contained content types or stereotypes in its settings, so nothing can be added to it.";
            }

            return settings.ContainedContentTypes.Contains(definition.Name, StringComparer.OrdinalIgnoreCase)
                ? null
                : $"The part '{container.PartName}' takes these content types only: {string.Join(", ", settings.ContainedContentTypes)}.";
        }

        if (container.Kind == FlowKind)
        {
            var settings = container.TypePart.GetSettings<FlowPartSettings>();

            if (settings.ContainedContentTypes is { Length: > 0 })
            {
                return settings.ContainedContentTypes.Contains(definition.Name, StringComparer.OrdinalIgnoreCase)
                    ? null
                    : $"The part '{container.PartName}' takes these content types only: {string.Join(", ", settings.ContainedContentTypes)}.";
            }
        }

        return definition.StereotypeEquals(WidgetStereotype, StringComparison.OrdinalIgnoreCase)
            ? null
            : $"The part '{container.PartName}' takes widgets only, and the content type '{definition.Name}' does not have the Widget stereotype.";
    }

    /// <summary>
    /// Whether the caller may change a nested content item. As in the content editor, only a securable content type is
    /// checked on its own; any other nested item follows the permission on the parent.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="definition">The content type of the nested content item.</param>
    /// <param name="permission">The permission the change needs.</param>
    /// <param name="nestedContentItem">The nested content item.</param>
    /// <returns><see langword="true"/> when the caller may change the nested content item.</returns>
    public static async Task<bool> IsNestedItemAuthorizedAsync(IServiceProvider services, ContentTypeDefinition definition, Permission permission, ContentItem nestedContentItem)
    {
        return definition is null
            || !definition.IsSecurable()
            || await AgentToolAuthorization.IsAuthorizedAsync(services, permission, nestedContentItem);
    }

    /// <summary>
    /// Saves the changed parts of the working JSON into a draft of the content item, validates it, and saves the draft or
    /// publishes it.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="contentItemId">The ID of the top content item.</param>
    /// <param name="root">The working JSON of the top content item, with the changes.</param>
    /// <param name="partNames">The names of the parts of the top content item that changed.</param>
    /// <param name="isDraft">Whether the content item is saved as a draft; otherwise it is published.</param>
    /// <param name="versionOptions">
    /// The version that takes the change. The default is a draft, which the content editor asks for. The menu and
    /// taxonomy editors change the latest version in place when the content type cannot have a draft.
    /// </param>
    /// <returns>The saved content item, or the error that stopped it.</returns>
    public static async Task<(ContentItem ContentItem, string Error)> SaveAsync(
        IServiceProvider services,
        string contentItemId,
        JsonObject root,
        IEnumerable<string> partNames,
        bool isDraft,
        VersionOptions versionOptions = null)
    {
        var contentManager = services.GetRequiredService<IContentManager>();
        var contentItem = await contentManager.GetAsync(contentItemId, versionOptions ?? VersionOptions.DraftRequired);

        if (contentItem is null)
        {
            return (null, $"No content item with the ID '{contentItemId}' was found.");
        }

        var patch = new JsonObject();

        foreach (var partName in partNames.Distinct(StringComparer.Ordinal))
        {
            patch[partName] = root[partName]?.DeepClone();
        }

        // Arrays are replaced, so each changed list is written whole and the other parts are left as they are.
        contentItem.Merge(patch, _replaceArrays);

        await contentManager.UpdateAsync(contentItem);

        var result = await contentManager.ValidateAsync(contentItem);

        if (!result.Succeeded)
        {
            await services.GetRequiredService<YSession>().CancelAsync();

            return (null, $"Nothing was saved: the content item is not valid. {string.Join(" ", result.Errors.Select(error => error.ErrorMessage))}".TrimEnd());
        }

        if (isDraft)
        {
            await contentManager.SaveDraftAsync(contentItem);
        }
        else
        {
            await contentManager.PublishAsync(contentItem);
        }

        return (contentItem, null);
    }

    private static async Task AddEntriesAsync(
        IContentDefinitionManager contentDefinitionManager,
        JsonObject owner,
        string ownerId,
        string topLevelPartName,
        int depth,
        List<NestedContentItemEntry> entries)
    {
        if (depth > MaxDepth)
        {
            return;
        }

        foreach (var container in await GetContainersAsync(contentDefinitionManager, owner, ownerId, topLevelPartName, depth, false))
        {
            for (var position = 0; position < container.Items.Count; position++)
            {
                if (container.Items[position] is not JsonObject item)
                {
                    continue;
                }

                entries.Add(new NestedContentItemEntry
                {
                    Container = container,
                    Position = position,
                    Item = item,
                });

                await AddEntriesAsync(
                    contentDefinitionManager,
                    item,
                    GetString(item, nameof(ContentItem.ContentItemId)),
                    container.TopLevelPartName,
                    depth + 1,
                    entries);
            }
        }
    }

    private static NestedContentItemContainer CreateContainer(
        string kind,
        ContentTypePartDefinition typePart,
        string zone,
        JsonArray items,
        JsonObject owner,
        string ownerId,
        string topLevelPartName,
        int depth)
    {
        return new NestedContentItemContainer
        {
            Kind = kind,
            PartName = typePart.Name,
            Zone = zone,
            Items = items,
            Owner = owner,
            OwnerId = ownerId,
            TypePart = typePart,
            TopLevelPartName = topLevelPartName ?? typePart.Name,
            Depth = depth,
        };
    }

    /// <summary>
    /// Reads the zones of a widgets list part from its settings. The settings class is in the Widgets module, so the
    /// stored JSON is read by name.
    /// </summary>
    private static IEnumerable<string> GetZones(ContentTypePartDefinition typePart)
    {
        if (typePart.Settings?["WidgetsListPartSettings"]?["Zones"] is not JsonArray zones)
        {
            return [];
        }

        return zones
            .Select(zone => zone is JsonValue value && value.TryGetValue<string>(out var name) ? name : null)
            .Where(zone => !string.IsNullOrWhiteSpace(zone));
    }

    private static JsonSerializerOptions GetSerializerOptions(IServiceProvider services)
        => services.GetRequiredService<IOptions<DocumentJsonSerializerOptions>>().Value.SerializerOptions;
}
