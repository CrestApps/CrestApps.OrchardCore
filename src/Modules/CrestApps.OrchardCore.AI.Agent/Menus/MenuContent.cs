using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Flows;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.AI.Agent.Menus;

/// <summary>
/// Reads a menu and its items. The menu parts are classes of the Menu module, so the stored JSON is read by name.
/// </summary>
internal static class MenuContent
{
    /// <summary>
    /// The stereotype of the content types that can be menu items.
    /// </summary>
    public const string MenuItemStereotype = "MenuItem";

    /// <summary>
    /// The part of a link menu item.
    /// </summary>
    public const string LinkPart = "LinkMenuItemPart";

    /// <summary>
    /// The part of an HTML menu item.
    /// </summary>
    public const string HtmlPart = "HtmlMenuItemPart";

    /// <summary>
    /// The part of a menu item that links to a content item.
    /// </summary>
    public const string ContentPart = "ContentMenuItemPart";

    /// <summary>
    /// The part that shows a menu item only to the users who hold some permissions.
    /// </summary>
    public const string PermissionPart = "MenuItemPermissionPart";

    /// <summary>
    /// The content picker field of the part of a menu item that links to a content item.
    /// </summary>
    public const string SelectedContentItemField = "SelectedContentItem";

    private const string MenuPart = "MenuPart";

    /// <summary>
    /// Whether a content item is a menu: its content type has the menu part or the menu items list part.
    /// </summary>
    /// <param name="definition">The content type of the content item.</param>
    /// <returns><see langword="true"/> when the content item is a menu.</returns>
    public static bool IsMenu(ContentTypeDefinition definition)
        => HasPart(definition, MenuPart) || HasPart(definition, ContentItemListTree.Menu.PartName);

    /// <summary>
    /// Whether a content type has a part.
    /// </summary>
    /// <param name="definition">The content type.</param>
    /// <param name="partName">The name of the part.</param>
    /// <returns><see langword="true"/> when the content type has the part.</returns>
    public static bool HasPart(ContentTypeDefinition definition, string partName)
        => definition?.Parts.Any(part => string.Equals(part.PartDefinition?.Name, partName, StringComparison.Ordinal)) == true;

    /// <summary>
    /// Gets the alias of a content item, when it has one.
    /// </summary>
    /// <param name="contentItem">The JSON of the content item.</param>
    /// <returns>The alias, or <see langword="null"/>.</returns>
    public static string GetAlias(JsonObject contentItem)
        => GetString(contentItem["AliasPart"], "Alias");

    /// <summary>
    /// Gets the fields a menu tool returns for one menu item.
    /// </summary>
    /// <param name="entry">The menu item and where it is.</param>
    /// <returns>The fields of the menu item.</returns>
    public static object Describe(NestedContentItemEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var item = entry.Item;
        var html = item[HtmlPart] as JsonObject;
        var content = item[ContentPart] as JsonObject;
        var urlPart = item[LinkPart] as JsonObject ?? html;
        var permissionNames = GetStrings(Child(item[PermissionPart], "PermissionNames"));

        return new
        {
            menuItemId = GetString(item, nameof(ContentItem.ContentItemId)),
            contentType = GetString(item, nameof(ContentItem.ContentType)),
            title = GetString(item, nameof(ContentItem.DisplayText)),
            url = GetString(urlPart, "Url"),
            target = GetString(urlPart, "Target"),
            html = GetString(html, "Html"),
            linkedContentItemId = GetStrings(Child(Child(content, SelectedContentItemField), "ContentItemIds")).FirstOrDefault(),
            checkContentPermissions = Child(content, "CheckContentPermissions") is JsonValue value && value.TryGetValue<bool>(out var checks)
                ? checks
                : (bool?)null,
            permissionNames = permissionNames.Count > 0
                ? permissionNames
                : null,
            parentMenuItemId = entry.Container.OwnerId,
            depth = entry.Container.Depth,
            position = entry.Position,
        };
    }

    private static JsonNode Child(JsonNode node, string name)
    {
        return node is JsonObject jsonObject
            ? jsonObject[name]
            : null;
    }

    private static string GetString(JsonNode node, string name)
    {
        return node is JsonObject jsonObject
            ? NestedContentItemTree.GetString(jsonObject, name)
            : null;
    }

    private static List<string> GetStrings(JsonNode node)
    {
        if (node is not JsonArray array)
        {
            return [];
        }

        return array
            .Select(value => value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text) ? text : null)
            .Where(text => !string.IsNullOrEmpty(text))
            .ToList();
    }
}
